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

            public void Add(ReadOnlySpan<byte> name, long key, int value)
            {
                if (count * 2 >= measurements.Length) Grow();
                var index = Index(key);

                while (true)
                {
                    ref var measurement = ref measurements[index];

                    if (measurement.Count == 0)
                    {
                        count++;
                        measurement.Bytes = name.ToArray();
                        measurement.Key = key;
                        measurement.Name = Encoding.UTF8.GetString(name);
                        measurement.Sum = value;
                        measurement.Min = value;
                        measurement.Max = value;
                        measurement.Count = 1;
                        return;
                    }

                    if (measurement.Key == key && name.SequenceEqual(measurement.Bytes))
                    {
                        measurement.Sum += value;
                        measurement.Min = measurement.Min < value ? measurement.Min : value;
                        measurement.Max = measurement.Max > value ? measurement.Max : value;
                        measurement.Count++;
                        return;
                    }

                    index = (index + 1) & TableMask;
                }
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

            private int Index(long key) => (int)(((ulong)key * 11400714819323198485ul) >> 32) & TableMask;

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

            using var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
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

            var lineStart = offset;
            var nameStart = offset;
            var nameLength = 0;
            var keyBytes = 0L;
            var value = 0;
            var negative = false;
            var readingName = true;

            for (int i = offset; i < buffer.Length; i++)
            {
                var current = buffer[i];

                if (readingName)
                {
                    if (current == seperator)
                    {
                        readingName = false;
                        nameLength = i - nameStart;
                        continue;
                    }

                    var keyIndex = i - nameStart;
                    if (keyIndex < 7)
                        keyBytes |= (long)current << (48 - (keyIndex * 8));

                    continue;
                }

                if (current == newLine)
                {
                    var key = ((long)nameLength << 56) | keyBytes;
                    data.Add(buffer.Slice(nameStart, nameLength), key, negative ? -value : value);

                    lineStart = i + 1;
                    nameStart = lineStart;
                    nameLength = 0;
                    keyBytes = 0;
                    value = 0;
                    negative = false;
                    readingName = true;
                    continue;
                }

                if (current == sign)
                {
                    negative = true;
                    continue;
                }

                if (current != dot && current != (byte)'\r')
                    value = (value * 10) + current - digitOffset;
            }

            return lineStart;
        }

        private static void ParseFinalLine(ReadOnlySpan<byte> line, MeasurementTable data)
        {
            var nameLength = 0;
            var keyBytes = 0L;
            var value = 0;
            var negative = false;
            var readingName = true;

            for (int i = 0; i < line.Length; i++)
            {
                var current = line[i];

                if (readingName)
                {
                    if (current == seperator)
                    {
                        readingName = false;
                        nameLength = i;
                        continue;
                    }

                    if (i < 7)
                        keyBytes |= (long)current << (48 - (i * 8));

                    continue;
                }

                if (current == sign)
                    negative = true;
                else if (current != dot && current != (byte)'\r')
                    value = (value * 10) + current - digitOffset;
            }

            var key = ((long)nameLength << 56) | keyBytes;
            data.Add(line[..nameLength], key, negative ? -value : value);
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
