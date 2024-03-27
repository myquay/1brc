using brc.Attempts.Lib03;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace brc.Attempts
{
    internal class Attempt03(BrcOptions Options) : IAttempt
    {

        /// <summary>
        /// Meausrement struct to hold the sum, min, max and count (to calculate the mean)
        /// </summary>
        private struct Measurement
        {
            public int Sum { get; set; }
            public int Min { get; set; }
            public int Max { get; set; }
            public int Count { get; set; }
        }

        //Our special characters
        const byte seperator = (byte)';';
        const byte newLine = (byte)'\n';

        public Task Solve()
        {
            var file = new FileInfo(Options.File);

            Span<byte> buffer = new byte[1024 * 512];
            var data = new Dictionary<string, Measurement>();

            using var reader = file.OpenRead();

            int bufferOffsetStart = 0;

            while (reader.Read(buffer) is int numberRead)
            {
                if (numberRead == 0)
                    break;

                if (numberRead < buffer.Length) //If bytes read is maller than buffer, truncate the buffer
                    buffer = buffer[..numberRead];

                if (buffer[bufferOffsetStart] == 239)
                    bufferOffsetStart += 3; //SKIP BOM

                //Iterate through all the lines
                while (buffer.Slice(bufferOffsetStart).IndexOf(newLine) is int newLineIndex and > -1)
                {
                    var line = buffer.Slice(bufferOffsetStart, newLineIndex);
                    bufferOffsetStart += newLineIndex + 1; //Skip the newline

                    var seperatorIndex = line.IndexOf(seperator);

                    var name = Encoding.UTF8.GetString(line[..seperatorIndex]);
                    var measurement = data.TryGetValue(name, out var m) ? m : new Measurement();

                    var value = FastParser.TempAsInt(line[(seperatorIndex + 1)..]);

                    measurement.Sum += value;
                    measurement.Min = measurement.Min < value ? measurement.Min : value;
                    measurement.Max = measurement.Max > value ? measurement.Max : value;
                    measurement.Count++;

                    data[name] = measurement;
                }

                //Backtrack to the start of the line
                reader.Seek(bufferOffsetStart - buffer.Length, SeekOrigin.Current);
                bufferOffsetStart = 0;
            }

            //Calculate and sort the measurements
            var measurements = data.Select(d => new
            {
                Station = d.Key,
                d.Value.Min,
                d.Value.Max,
                Mean = d.Value.Sum / d.Value.Count
            })
            .OrderBy(s => s.Station)
            .ToArray();

            //Output data
            Console.Write("{");
            for (int i = 0; i < measurements.Length - 2; i++)
            {
                Console.Write($"{measurements[i].Station}={measurements[i].Min}/{measurements[i].Mean:#.0}/{measurements[i].Max}, ");
            }
            Console.Write($"{measurements[^1].Station}={measurements[^1].Min / 10f}/{measurements[^1].Mean / 10f:#.0}/{measurements[^1].Max / 10f}}}");


            return Task.CompletedTask;

        }
    }
}
