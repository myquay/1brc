using System.Globalization;
using System.Runtime.InteropServices;

// Deliberately independent: StreamReader, strings, decimal.Parse and Dictionary.
var data = new Dictionary<string, (int Min, int Max, long Sum, long Count)>(StringComparer.Ordinal);
long rows = 0;
foreach (var line in File.ReadLines(args[0]))
{
    var split = line.IndexOf(';');
    var name = line[..split];
    var value = checked((int)(decimal.Parse(line.AsSpan(split + 1), CultureInfo.InvariantCulture) * 10));
    ref var item = ref CollectionsMarshal.GetValueRefOrAddDefault(data, name, out var found);
    if (!found) item = (value, value, value, 1);
    else item = (Math.Min(item.Min, value), Math.Max(item.Max, value), item.Sum + value, item.Count + 1);
    rows++;
}
static string Format(decimal value) => value.ToString("0.0", CultureInfo.InvariantCulture);
Console.Write("{");
Console.Write(string.Join(", ", data.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x =>
    $"{x.Key}={Format(x.Value.Min / 10m)}/{Format(decimal.Floor(x.Value.Sum / (decimal)x.Value.Count + 0.5m) / 10m)}/{Format(x.Value.Max / 10m)}")));
Console.WriteLine("}");
Console.Error.WriteLine($"Rows: {rows}; stations: {data.Count}");
