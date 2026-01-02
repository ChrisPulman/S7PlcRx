// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for multi-variable batching against non-DB areas (I/Q/M) and bit addressing.
/// </summary>
[NonParallelizable]
public class S7PlcRxMultiVarNonDbAreaTests
{
    /// <summary>
    /// Ensures MultiVar can read values from memory areas and bit addresses.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValueBatch_ShouldRead_MemoryAreasAndBits()
    {
        using var server = new MockServer();
        var rc = server.Start();
        Assert.That(rc, Is.EqualTo(0));

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 1);

        plc.AddUpdateTagItem<byte>("MB0", "MB0").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("MW2", "MW2").SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x);

        static async Task EventuallyAsync(Func<Task<bool>> predicate, TimeSpan timeout, TimeSpan interval)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (await predicate())
                {
                    return;
                }

                await Task.Delay(interval);
            }

            Assert.Fail($"Condition not met within {timeout}.");
        }

        // Write values (single path). Readback should use MultiVar path.
        plc.Value("MB0", (byte)0xAA);
        await EventuallyAsync(async () => (await plc.Value<byte>("MB0")) == (byte)0xAA, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20));

        plc.Value("MW2", (ushort)0x1234);
        await EventuallyAsync(async () => (await plc.Value<ushort>("MW2")) == (ushort)0x1234, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20));

        var byteRes = await plc.ValueBatch<byte>("MB0");
        var wordRes = await plc.ValueBatch<ushort>("MW2");

        Assert.That(byteRes["MB0"], Is.EqualTo((byte)0xAA));
        Assert.That(wordRes["MW2"], Is.EqualTo((ushort)0x1234));
    }
}
