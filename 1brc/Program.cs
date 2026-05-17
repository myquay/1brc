using brc.Attempts;
using System.Diagnostics;

string[] enabled = ["05", "06"];

var solvers = new Dictionary<string, IAttempt>
{
    { "01", new Attempt01(new BrcOptions("measurements.txt", false)) },
    { "02", new Attempt02(new BrcOptions("measurements.txt", false)) },
    { "03", new Attempt03(new BrcOptions("measurements.txt", false)) },
    { "04", new Attempt04(new BrcOptions("measurements.txt", false)) },
    { "05", new Attempt05(new BrcOptions("measurements.txt", true)) },
    { "06", new Attempt06(new BrcOptions("measurements.txt", true)) }
};

var timings = new Dictionary<string, long>();

foreach (var kvp in solvers)
{
    if (enabled.Contains(kvp.Key))
    {
        var sw = Stopwatch.StartNew();
        await kvp.Value.Solve();
        sw.Stop();
        timings.Add(kvp.Key, sw.ElapsedMilliseconds);
    }
}

Console.WriteLine("\n\n");
foreach (var kvp in timings)
    Console.WriteLine($"Attempt {kvp.Key} total: {kvp.Value}ms");
