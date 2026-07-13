// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests cancellation-aware APIs.
/// </summary>
public class S7PlcRxCancellationTests
{
    /// <summary>
    /// Ensures pre-canceled tokens cancel ValueAsync immediately.
    /// </summary>
    [Test]
    public void ValueAsync_WhenCanceled_ShouldThrowOperationCanceledException()
    {
        using var plc = new RxS7(new(new(CpuType.S71500, MockServer.Localhost, 0, 1)));
        plc.AddUpdateTagItem<ushort>("T0", "DB1.DBW0").SetTagPollIng(false);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await plc.ValueAsync<ushort>("T0", cts.Token));
    }
}
