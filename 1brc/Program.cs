using brc.Attempts;
using System.Diagnostics;

//var options = new BrcOptions("C:\\\\1brc\\measurements_1e9.txt", false);
var options = new BrcOptions("C:\\\\1brc\\measurements_1e5.txt", false);

var solvers = new Dictionary<string, IAttempt>
{
    { "01", new Attempt01(options) }
};


do
{
    Console.Write("Which Attempt? ");
    var attempt = Console.ReadLine() ?? "None";
    Console.WriteLine();


    var sw = Stopwatch.StartNew();

    if (solvers.TryGetValue(attempt, out var solver))
    {
        await solver.Solve();
    }
    else if (attempt == "all")
    {
        foreach(var kvp in solvers)
        {
            await kvp.Value.Solve();
            Console.WriteLine($"\n\nAttempt {kvp.Key} total: {sw.ElapsedMilliseconds}ms\n");
        }
    }
    else if(attempt == "exit")
    {
        break;
    }
    else
    { 
        Console.WriteLine("Invalid Attempt");
        Console.WriteLine();
    }

    sw.Stop();
    Console.WriteLine($"\n\nTotal: {sw.ElapsedMilliseconds}ms\n");

} while (true);