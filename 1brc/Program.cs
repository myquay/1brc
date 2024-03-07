using brc.Attempts;
using System.Diagnostics;

var size = "1e6"; //Options: 1e5, 1e6, 1e9

string[] enabled = ["01"];

var solvers = new Dictionary<string, IAttempt>
{
    { "01", new Attempt01(new BrcOptions($"C:\\\\1brc\\measurements_{size}.txt", false)) }
};

var sw = Stopwatch.StartNew();

foreach (var kvp in solvers)
{
    if (enabled.Contains(kvp.Key))
    {
        await kvp.Value.Solve();
        Console.WriteLine($"\n\nAttempt {kvp.Key} total: {sw.ElapsedMilliseconds}ms\n");
    }
}

sw.Stop();
Console.WriteLine($"\n\nTotal: {sw.ElapsedMilliseconds}ms\n");