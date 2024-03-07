using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brc.Attempts
{
    internal class Attempt01(BrcOptions Options) : IAttempt
    {

        /// <summary>
        /// Meausrement struct to hold the sum, min, max and count (to calculate the mean)
        /// </summary>
        private struct Measurement
        {
            public double Sum { get; set; }
            public double Min { get; set; }
            public double Max { get; set; }
            public int Count { get; set; }
        }

        public async Task Solve()
        {
            Console.WriteLine("Solving using Attempt 01");
            var file = new FileInfo(Options.File);
            Console.WriteLine($"File: {file.FullName}");

            using var reader = new StreamReader(file.FullName);

            var data = new Dictionary<string, Measurement>();
            while (!reader.EndOfStream)
            {
                var stationParts = (await reader.ReadLineAsync())?.Split(';');

                var measurement = data.TryGetValue(stationParts[0], out var m) ? m : new Measurement();

                //Update the measurement
                var value = double.Parse(stationParts[1]);
                measurement.Sum += value;
                measurement.Min = Math.Min(measurement.Min, value);
                measurement.Max = Math.Max(measurement.Max, value);
                measurement.Count++;

                data[stationParts[0]] = measurement;
            }

            //Calculate and sort the measurements
            var measurements = data.Select(d => new
            {
                Station = d.Key,
                Min = d.Value.Min,
                Max = d.Value.Max,
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
            Console.Write($"{measurements[^1].Station}={measurements[^1].Min}/{measurements[^1].Mean:#.0}/{measurements[^1].Max}}}");
        }
    }
}
