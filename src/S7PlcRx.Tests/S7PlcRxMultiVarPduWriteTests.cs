// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for multi-variable PDU write batching.
/// </summary>
public class S7PlcRxMultiVarPduWriteTests
{
    /// <summary>
    /// Ensures `ValueBatch(Dictionary)` can write multiple tags.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [NonParallelizable]
    public async Task ValueBatch_ShouldWriteMultipleTagsInOneCall()
    {
        using var server = new MockServer();
        var rc = server.Start();
        Assert.That(rc, Is.EqualTo(0));

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 1);

        plc.AddUpdateTagItem<ushort>("W0", "DB1.DBW0").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("W1", "DB1.DBW2").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("W2", "DB1.DBW4").SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x);

        await plc.ValueBatch(new Dictionary<string, ushort>
        {
            ["W0"] = 10,
            ["W1"] = 20,
            ["W2"] = 30
        });

        var values = await plc.ValueBatch<ushort>("W0", "W1", "W2");

        Assert.That(values["W0"], Is.EqualTo((ushort)10));
        Assert.That(values["W1"], Is.EqualTo((ushort)20));
        Assert.That(values["W2"], Is.EqualTo((ushort)30));
    }
}
