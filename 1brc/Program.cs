using brc.Attempts;
using System.Diagnostics;

var arguments = args.ToList();
var file = "measurements.txt";
var fileIndex = arguments.IndexOf("--file");
if (fileIndex >= 0)
{
    if (fileIndex + 1 >= arguments.Count) throw new ArgumentException("--file requires a path");
    file = arguments[fileIndex + 1];
    arguments.RemoveRange(fileIndex, 2);
}
var quiet = arguments.Remove("--quiet");
string[] enabled = arguments.Count == 0 ? ["07"] : arguments.ToArray();

var solvers = new Dictionary<string, IAttempt>
{
    { "01", new Attempt01(new BrcOptions(file, quiet)) },
    { "02", new Attempt02(new BrcOptions(file, quiet)) },
    { "03", new Attempt03(new BrcOptions(file, quiet)) },
    { "04", new Attempt04(new BrcOptions(file, quiet)) },
    { "05", new Attempt05(new BrcOptions(file, quiet)) },
    { "06", new Attempt06(new BrcOptions(file, quiet)) },
    { "07", new Attempt07(new BrcOptions(file, quiet)) }
};

if (enabled.Any(attempt => !solvers.ContainsKey(attempt)))
{
    Console.Error.WriteLine("Usage: 1brc [01 02 03 04 05 06 07] [--file path] [--quiet]");
    Environment.ExitCode = 1;
    return;
}

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
