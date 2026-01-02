// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for PLC factory helpers in the `S7PlcRx.Create` namespace.
/// </summary>
public class S7CreateFactoryTests
{
    /// <summary>
    /// Validates `S71200.Create` rejects invalid rack values.
    /// </summary>
    [Test]
    public void S71200Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S71200.Create("127.0.0.1", rack: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => S71200.Create("127.0.0.1", rack: 8));
    }

    /// <summary>
    /// Validates `S7300.Create` rejects invalid rack values.
    /// </summary>
    [Test]
    public void S7300Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create("127.0.0.1", rack: -1, slot: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create("127.0.0.1", rack: 8, slot: 2));
    }

    /// <summary>
    /// Validates `S7300.Create` rejects invalid slot values.
    /// </summary>
    [Test]
    public void S7300Create_WhenSlotOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create("127.0.0.1", rack: 0, slot: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7300.Create("127.0.0.1", rack: 0, slot: 32));
    }

    /// <summary>
    /// Validates `S7400.Create` rejects invalid rack values.
    /// </summary>
    [Test]
    public void S7400Create_WhenRackOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create("127.0.0.1", rack: -1, slot: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create("127.0.0.1", rack: 8, slot: 2));
    }

    /// <summary>
    /// Validates `S7400.Create` rejects invalid slot values.
    /// </summary>
    [Test]
    public void S7400Create_WhenSlotOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create("127.0.0.1", rack: 0, slot: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => S7400.Create("127.0.0.1", rack: 0, slot: 32));
    }

    /// <summary>
    /// Smoke test ensuring `S7200.Create` returns an instance.
    /// </summary>
    [Test]
    public void S7200Create_ShouldReturnInstance()
    {
        var plc = S7200.Create("127.0.0.1", rack: 0, slot: 2);
        Assert.That(plc, Is.Not.Null);
    }
}
