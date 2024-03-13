using brc.Attempts;
using System.Diagnostics;

var size = "1e6"; //Options: 1e5, 1e6, 1e9

string[] enabled = ["02"];

var solvers = new Dictionary<string, IAttempt>
{
    { "01", new Attempt01(new BrcOptions($"C:\\\\1brc\\measurements_{size}.txt", false)) },
    { "02", new Attempt02(new BrcOptions($"C:\\\\1brc\\measurements_{size}.txt", false)) }
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