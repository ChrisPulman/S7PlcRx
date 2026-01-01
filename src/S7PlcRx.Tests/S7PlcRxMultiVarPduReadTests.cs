// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for multi-variable PDU read batching.
/// </summary>
public class S7PlcRxMultiVarPduReadTests
{
    /// <summary>
    /// Ensures `ValueBatch` returns values for multiple tags.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Ignore("MockS7Plc does not currently emulate S7 ReadVar multi-item PDUs reliably; enable for manual PLC verification.")]
    public async Task ValueBatch_ShouldReadMultipleTagsInOneCall()
    {
        using var server = new MockServer();
        var rc = server.Start();
        Assert.That(rc, Is.EqualTo(0));

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 1);

        plc.AddUpdateTagItem<ushort>("T0", "DB1.DBW0").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("T1", "DB1.DBW2").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("T2", "DB1.DBW4").SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x);

        plc.Value("T0", (ushort)10);
        plc.Value("T1", (ushort)20);
        plc.Value("T2", (ushort)30);

        var values = await plc.ValueBatch<ushort>("T0", "T1", "T2");

        Assert.That(values["T0"], Is.EqualTo((ushort)10));
        Assert.That(values["T1"], Is.EqualTo((ushort)20));
        Assert.That(values["T2"], Is.EqualTo((ushort)30));
    }
}
