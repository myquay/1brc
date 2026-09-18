using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace brc.Attempts
{
    internal class Attempt07(BrcOptions Options) : IAttempt
    {
        private struct Measurement
        {
            public long Key { get; set; }
            public string Name { get; set; }
            public byte[] Bytes { get; set; }
            public long Sum { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public int Count { get; set; }
        }

        private sealed class MeasurementTable
        {
            private const int TableSize = 1024;
            private int TableMask => measurements.Length - 1;
            private int count;

            private Measurement[] measurements = new Measurement[TableSize];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(ReadOnlySpan<byte> name, long key, int value)
            {
                var index = Index(key);
                while (true)
                {
                    ref var measurement = ref measurements[index];
                    if (measurement.Key == key && name.SequenceEqual(measurement.Bytes))
                    {
                        measurement.Sum += value;
                        measurement.Min = Math.Min(measurement.Min, value);
                        measurement.Max = Math.Max(measurement.Max, value);
                        measurement.Count++;
                        return;
                    }
                    if (measurement.Count == 0)
                    {
                        Insert(name, key, value, index);
                        return;
                    }
                    index = (index + 1) & TableMask;
                }
            }

            private void Insert(ReadOnlySpan<byte> name, long key, int value, int index)
            {
                if (count * 2 >= measurements.Length)
                {
                    Grow();
                    Add(name, key, value);
                    return;
                }
                measurements[index] = new Measurement
                {
                    Key = key, Bytes = name.ToArray(), Name = Encoding.UTF8.GetString(name),
                    Sum = value, Min = value, Max = value, Count = 1
                };
                count++;
            }

            public void Add(Measurement value)
            {
                if (count * 2 >= measurements.Length) Grow();
                var index = Index(value.Key);

                while (true)
                {
                    ref var measurement = ref measurements[index];

                    if (measurement.Count == 0)
                    {
                        count++;
                        measurement = value;
                        return;
                    }

                    if (measurement.Key == value.Key && measurement.Name == value.Name)
                    {
                        measurement.Sum += value.Sum;
                        measurement.Min = measurement.Min < value.Min ? measurement.Min : value.Min;
                        measurement.Max = measurement.Max > value.Max ? measurement.Max : value.Max;
                        measurement.Count += value.Count;
                        return;
                    }

                    index = (index + 1) & TableMask;
                }
            }

            private int Index(long key) => (int)(((ulong)key * 11400714819323198485ul) >> (64 - System.Numerics.BitOperations.Log2((uint)measurements.Length))) & TableMask;

            private void Grow()
            {
                var old = measurements;
                measurements = new Measurement[old.Length * 2];
                count = 0;
                foreach (var value in old)
                    if (value.Count != 0) Add(value);
            }

            public IEnumerable<Measurement> Values
            {
                get
                {
                    foreach (var measurement in measurements)
                    {
                        if (measurement.Count > 0)
                            yield return measurement;
                    }
                }
            }
        }

        const byte seperator = (byte)';';
        const byte newLine = (byte)'\n';
        const byte sign = (byte)'-';
        const byte dot = (byte)'.';
        const byte digitOffset = (byte)'0';

        public async Task Solve()
        {
            var file = new FileInfo(Options.File);
            var workerCount = Math.Min(Environment.ProcessorCount, Math.Max(1, (int)(file.Length / (1024 * 1024))));
            var ranges = CreateRanges(file, workerCount);

            var tasks = new Task<MeasurementTable>[ranges.Length];

            for (int i = 0; i < ranges.Length; i++)
            {
                var range = ranges[i];
                tasks[i] = Task.Run(() => ReadRange(file.FullName, range.Start, range.End));
            }

            var workerResults = await Task.WhenAll(tasks);
            var data = MergeResults(workerResults);

            var measurements = data.Values.Select(m => new
            {
                Station = m.Name,
                Min = m.Min / 10.0,
                Max = m.Max / 10.0,
                Mean = Math.Floor(m.Sum / (double)m.Count + 0.5) / 10.0
            })
            .OrderBy(s => s.Station, StringComparer.Ordinal)
            .ToArray();

            if (!Options.Quiet)
            {
                Console.Write("{");
                for (int i = 0; i < measurements.Length; i++)
                {
                    if (i > 0)
                        Console.Write(", ");

                    Console.Write(FormattableString.Invariant($"{measurements[i].Station}={measurements[i].Min:0.0}/{measurements[i].Mean:0.0}/{measurements[i].Max:0.0}"));
                }
                Console.Write("}");
            }
        }

        private static (long Start, long End)[] CreateRanges(FileInfo file, int workerCount)
        {
            var ranges = new (long Start, long End)[workerCount];
            var chunkSize = file.Length / workerCount;

            using var reader = file.OpenRead();

            for (int i = 0; i < workerCount; i++)
            {
                var start = i == 0 ? 0 : FindNextNewLine(reader, i * chunkSize) + 1;
                var end = i == workerCount - 1 ? file.Length : FindNextNewLine(reader, (i + 1) * chunkSize) + 1;
                ranges[i] = (start, end);
            }

            return ranges.Where(r => r.Start < r.End).ToArray();
        }

        private static long FindNextNewLine(FileStream reader, long offset)
        {
            Span<byte> buffer = stackalloc byte[8192];
            reader.Position = offset;

            while (reader.Position < reader.Length)
            {
                var bufferStart = reader.Position;
                var bytesRead = reader.Read(buffer);
                if (bytesRead == 0)
                    return reader.Length - 1;

                var index = buffer[..bytesRead].IndexOf(newLine);
                if (index >= 0)
                    return bufferStart + index;
            }

            return reader.Length - 1;
        }

        private static MeasurementTable ReadRange(string fileName, long start, long end)
        {
            const int bufferSize = 1024 * 1024 * 4;
            var data = new MeasurementTable();
            var buffer = new byte[bufferSize];
            var carry = 0;
            var position = start;

            using var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
            reader.Position = start;

            while (position < end)
            {
                var bytesToRead = (int)Math.Min(buffer.Length - carry, end - position);
                var bytesRead = reader.Read(buffer, carry, bytesToRead);
                if (bytesRead == 0)
                    break;

                if (position == 0 && bytesRead >= 3 && buffer.AsSpan(0, 3).SequenceEqual("\uFEFF"u8))
                {
                    buffer.AsSpan(3, bytesRead - 3).CopyTo(buffer);
                    position += 3;
                    bytesRead -= 3;
                }
                position += bytesRead;
                var available = carry + bytesRead;
                var consumed = ParseCompleteLines(buffer.AsSpan(0, available), data);
                carry = available - consumed;

                if (carry > 0)
                    buffer.AsSpan(consumed, carry).CopyTo(buffer);
            }

            if (carry > 0)
                ParseFinalLine(buffer.AsSpan(0, carry), data);

            return data;
        }

        private static int ParseCompleteLines(ReadOnlySpan<byte> buffer, MeasurementTable data)
        {
            var offset = 0;
            var completeEnd = buffer.LastIndexOf(newLine) + 1;
            while (offset < completeEnd)
            {
                var nameLength = ScanName(buffer[offset..completeEnd], out var key);
                if (nameLength < 1) throw new FormatException("Missing station or separator");
                var name = buffer.Slice(offset, nameLength);
                var position = offset + nameLength + 1;
                var negative = buffer[position] == sign;
                if (negative) position++;
                var value = buffer[position++] - digitOffset;
                if (buffer[position] != dot)
                    value = value * 10 + buffer[position++] - digitOffset;
                position++; // Decimal point; input has exactly one fractional digit.
                value = value * 10 + buffer[position++] - digitOffset;
                if (buffer[position] == (byte)'\r') position++;
                if (buffer[position] != newLine) throw new FormatException("Invalid temperature");
                data.Add(name, key, negative ? -value : value);
                offset = position + 1;
            }
            return offset;
        }

        private static void ParseFinalLine(ReadOnlySpan<byte> line, MeasurementTable data)
        {
            var nameLength = line.IndexOf(seperator);
            if (nameLength < 1) throw new FormatException("Missing station or separator");
            var name = line[..nameLength];
            var temperature = line[(nameLength + 1)..];
            var negative = temperature[0] == sign;
            var value = 0;
            foreach (var current in temperature[(negative ? 1 : 0)..])
                if (current != dot && current != (byte)'\r')
                    value = value * 10 + current - digitOffset;
            data.Add(name, GetKey(name), negative ? -value : value);
        }

        // Detect a separator in eight bytes while hashing those same bytes.
        // All loads are span-bounded; zero-byte detection finds the first match.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ScanName(ReadOnlySpan<byte> bytes, out long key)
        {
            var length = 0;
            ulong hash = 0;
            while (bytes.Length >= 8)
            {
                var word = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
                var match = word ^ 0x3b3b3b3b3b3b3b3bUL;
                var mask = (match - 0x0101010101010101UL) & ~match & 0x8080808080808080UL;
                if (mask != 0)
                {
                    var count = BitOperations.TrailingZeroCount(mask) / 8;
                    if (count != 0) hash = BitOperations.RotateLeft(hash, 5) ^ (word & (ulong.MaxValue >> (64 - count * 8)));
                    length += count;
                    key = (long)(hash ^ (uint)length);
                    return length;
                }
                hash = BitOperations.RotateLeft(hash, 5) ^ word;
                length += 8;
                bytes = bytes[8..];
            }
            ulong tail = 0;
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == seperator)
                {
                    if (i != 0) hash = BitOperations.RotateLeft(hash, 5) ^ tail;
                    key = (long)(hash ^ (uint)(length + i));
                    return length + i;
                }
                tail |= (ulong)bytes[i] << (i * 8);
            }
            throw new FormatException("Missing separator");
        }

        private static long GetKey(ReadOnlySpan<byte> name)
        {
            var length = name.Length;
            ulong hash = 0;
            while (name.Length >= 8)
            {
                hash = BitOperations.RotateLeft(hash, 5) ^ BinaryPrimitives.ReadUInt64LittleEndian(name);
                name = name[8..];
            }
            ulong tail = 0;
            for (var i = 0; i < name.Length; i++) tail |= (ulong)name[i] << (i * 8);
            if (name.Length != 0) hash = BitOperations.RotateLeft(hash, 5) ^ tail;
            return (long)(hash ^ (uint)length);
        }

        private static MeasurementTable MergeResults(MeasurementTable[] workerResults)
        {
            var merged = new MeasurementTable();

            foreach (var result in workerResults)
            {
                foreach (var measurement in result.Values)
                    merged.Add(measurement);
            }

            return merged;
        }
    }
}
