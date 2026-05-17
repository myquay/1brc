using v4 = brc.Attempts.Lib04;
using System.Runtime.InteropServices;
using System.Text;

namespace brc.Attempts
{
    internal class Attempt05(BrcOptions Options) : IAttempt
    {

        /// <summary>
        /// Meausrement struct to hold the sum, min, max and count (to calculate the mean)
        /// </summary>
        private struct Measurement
        {
            public string Name { get; set; }
            public long Sum { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public int Count { get; set; }
        }

        //Our special characters
        const byte seperator = (byte)';';
        const byte newLine = (byte)'\n';

        public async Task Solve()
        {
            var file = new FileInfo(Options.File);
            var workerCount = Math.Min(Environment.ProcessorCount, Math.Max(1, (int)(file.Length / (1024 * 1024))));
            var ranges = CreateRanges(file, workerCount);

            var tasks = new Task<Dictionary<long, Measurement>>[ranges.Length];

            for (int i = 0; i < ranges.Length; i++)
            {
                var range = ranges[i];
                tasks[i] = Task.Run(() => ReadRange(file.FullName, range.Start, range.End));
            }

            var workerResults = await Task.WhenAll(tasks);
            var data = MergeResults(workerResults);

            //Calculate and sort the measurements
            var measurements = data.Select(d => new
            {
                Station = d.Value.Name,
                Min = d.Value.Min / 10.0,
                Max = d.Value.Max / 10.0,
                Mean = d.Value.Sum / (double)d.Value.Count / 10.0
            })
            .OrderBy(s => s.Station)
            .ToArray();

            if (!Options.Quiet)
            {
                //Output data
                Console.Write("{");
                for (int i = 0; i < measurements.Length; i++)
                {
                    if (i > 0)
                        Console.Write(", ");

                    Console.Write($"{measurements[i].Station}={measurements[i].Min:0.0}/{measurements[i].Mean:0.0}/{measurements[i].Max:0.0}");
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

        private static Dictionary<long, Measurement> ReadRange(string fileName, long start, long end)
        {
            const int bufferSize = 1024 * 1024 * 4;
            var data = new Dictionary<long, Measurement>(512);
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

                position += bytesRead;
                var available = carry + bytesRead;
                var consumed = ParseCompleteLines(buffer.AsSpan(0, available), data);
                carry = available - consumed;

                if (carry > 0)
                    buffer.AsSpan(consumed, carry).CopyTo(buffer);
            }

            if (carry > 0)
                ParseLine(buffer.AsSpan(0, carry), data);

            return data;
        }

        private static int ParseCompleteLines(Span<byte> buffer, Dictionary<long, Measurement> data)
        {
            var offset = 0;
            if (buffer.Length >= 3 && buffer[0] == 239)
                offset = 3; // Skip UTF-8 BOM.

            while (buffer[offset..].IndexOf(newLine) is int newLineIndex and >= 0)
            {
                ParseLine(buffer.Slice(offset, newLineIndex), data);
                offset += newLineIndex + 1;
            }

            return offset;
        }

        private static void ParseLine(ReadOnlySpan<byte> line, Dictionary<long, Measurement> data)
        {
            var seperatorIndex = line.IndexOf(seperator);
            var name = line[..seperatorIndex];
            var dictKey = v4.Utilities.GenerateKey(name);

            ref var measurement = ref CollectionsMarshal.GetValueRefOrAddDefault(data, dictKey, out var exists);
            var value = v4.Utilities.FastParseTemp(line[(seperatorIndex + 1)..]);

            if (!exists)
            {
                measurement.Name = Encoding.UTF8.GetString(name);
                measurement.Sum = value;
                measurement.Min = value;
                measurement.Max = value;
                measurement.Count = 1;
                return;
            }

            measurement.Sum += value;
            measurement.Min = measurement.Min < value ? measurement.Min : value;
            measurement.Max = measurement.Max > value ? measurement.Max : value;
            measurement.Count++;
        }

        private static Dictionary<long, Measurement> MergeResults(Dictionary<long, Measurement>[] workerResults)
        {
            var merged = new Dictionary<long, Measurement>(512);

            foreach (var result in workerResults)
            {
                foreach (var kvp in result)
                {
                    ref var measurement = ref CollectionsMarshal.GetValueRefOrAddDefault(merged, kvp.Key, out var exists);
                    if (!exists)
                    {
                        measurement = kvp.Value;
                        continue;
                    }

                    measurement.Sum += kvp.Value.Sum;
                    measurement.Min = measurement.Min < kvp.Value.Min ? measurement.Min : kvp.Value.Min;
                    measurement.Max = measurement.Max > kvp.Value.Max ? measurement.Max : kvp.Value.Max;
                    measurement.Count += kvp.Value.Count;
                }
            }

            return merged;
        }
    }
}
