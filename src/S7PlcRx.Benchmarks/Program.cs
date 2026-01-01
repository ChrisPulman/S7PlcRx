using System.Reflection;
using S7PlcRx.Benchmarks;

try
{
    Console.WriteLine($"AppBase: {AppContext.BaseDirectory}");

    // Ensure MockS7Plc is loadable before running harness
    var mockAsm = Assembly.Load("MockS7Plc");
    Console.WriteLine($"Loaded MockS7Plc: {mockAsm.Location}");

    return await PerfHarness.RunAsync(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Console.Error.WriteLine("Files in AppBase:");
    foreach (var f in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.*"))
    {
        var name = Path.GetFileName(f);
        if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || name.Equals("snap7.dll", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("  " + name);
        }
    }

    return 1;
}
