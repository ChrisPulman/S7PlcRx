using System.Diagnostics;
using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Benchmarks;

internal static class PerfHarness
{
    internal static async Task<int> RunAsync(string[] args)
    {
        using var server = new MockServer();

        var rc = server.Start();
        if (rc != 0)
        {
            Console.Error.WriteLine($"MockServer.Start failed: {rc}");
            return rc;
        }

        var localhost = MockServer.Localhost;

        using var plc = new RxS7(CpuType.S71500, localhost, 0, 1, null, interval: 1);
        plc.AddUpdateTagItem<ushort>("BenchWord", "DB1.DBW0").SetTagPollIng(false);

        var connectSw = Stopwatch.StartNew();
        await plc.IsConnected.FirstAsync(x => x);
        connectSw.Stop();

        Console.WriteLine($"Connect time: {connectSw.ElapsedMilliseconds} ms");

        const int iterations = 500;
        const int tagCount = 8;
        var tagNames = new string[tagCount];
        for (var i = 0; i < tagCount; i++)
        {
            tagNames[i] = $"BenchWord{i}";
            plc.AddUpdateTagItem<ushort>(tagNames[i], $"DB1.DBW{i}").SetTagPollIng(false);
        }

        // Warm-up
        for (var j = 0; j < 50; j++)
        {
            plc.Value("BenchWord", (ushort)j);
            _ = await plc.Value<ushort>("BenchWord");
        }

        // Read
        var readSw = Stopwatch.StartNew();
        for (var j = 0; j < iterations; j++)
        {
            _ = await plc.Value<ushort>("BenchWord");
        }

        readSw.Stop();

        // Write
        var writeSw = Stopwatch.StartNew();
        for (var j = 0; j < iterations; j++)
        {
            plc.Value("BenchWord", (ushort)j);
        }

        writeSw.Stop();

        // Read+Write cycle
        var rwSw = Stopwatch.StartNew();
        for (var j = 0; j < iterations; j++)
        {
            plc.Value("BenchWord", (ushort)j);
            _ = await plc.Value<ushort>("BenchWord");
        }

        rwSw.Stop();

        // N tags per cycle (single-tag write)
        var singleWriteSw = Stopwatch.StartNew();
        for (var j = 0; j < iterations; j++)
        {
            for (var i = 0; i < tagCount; i++)
            {
                plc.Value(tagNames[i], (ushort)(j + i));
            }

            for (var i = 0; i < tagCount; i++)
            {
                _ = await plc.Value<ushort>(tagNames[i]);
            }
        }

        singleWriteSw.Stop();

        // N tags per cycle (batch write)
        var batchWriteSw = Stopwatch.StartNew();
        for (var j = 0; j < iterations; j++)
        {
            var dict = new Dictionary<string, ushort>(tagCount);
            for (var i = 0; i < tagCount; i++)
            {
                dict[tagNames[i]] = (ushort)(j + i);
            }

            await plc.ValueBatch(dict);
        }

        batchWriteSw.Stop();

        static double PerOpMs(Stopwatch sw, int iters) => sw.Elapsed.TotalMilliseconds / iters;
        Console.WriteLine($"Read avg:  {PerOpMs(readSw, iterations):F3} ms/op");
        Console.WriteLine($"Write avg: {PerOpMs(writeSw, iterations):F3} ms/op");
        Console.WriteLine($"R+W avg:   {PerOpMs(rwSw, iterations):F3} ms/op");
        static double PerCycleMs(Stopwatch sw, int iters) => sw.Elapsed.TotalMilliseconds / iters;
        static double PerTagUs(Stopwatch sw, int iters, int tagCount) => sw.Elapsed.TotalMilliseconds / iters * 1000 / tagCount;
        Console.WriteLine($"Single-tag write (x{tagCount} per cycle) avg: {PerCycleMs(singleWriteSw, iterations):F3} ms/cycle ({PerTagUs(singleWriteSw, iterations, tagCount):F1} us/tag)");
        Console.WriteLine($"Batch write ({tagCount} tags per cycle) avg:          {PerCycleMs(batchWriteSw, iterations):F3} ms/cycle ({PerTagUs(batchWriteSw, iterations, tagCount):F1} us/tag)");

        return 0;
    }
}
