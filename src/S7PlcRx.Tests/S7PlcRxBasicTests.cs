// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Basic functionality tests for S7PlcRx.
/// </summary>
public class S7PlcRxBasicTests
{
    /// <summary>
    /// Test that S71500 factory creates correct instance.
    /// </summary>
    [Test]
    public void S71500_Create_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        Assert.That(plc.PLCType, Is.EqualTo(CpuType.S71500));
        Assert.That(plc.Rack, Is.EqualTo(0));
        Assert.That(plc.Slot, Is.EqualTo(1));
    }

    /// <summary>
    /// Test that different PLC types can be created.
    /// </summary>
    /// <param name="cpuType">The CPU type to test.</param>
    [TestCase(CpuType.S71500)]
    [TestCase(CpuType.S7300)]
    [TestCase(CpuType.S7400)]
    [TestCase(CpuType.S71200)]
    [TestCase(CpuType.S7200)]
    public void RxS7_Create_DifferentTypes_ShouldSetCorrectCpuType(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, MockServer.Localhost, 0, 1, null, 100);

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.PLCType, Is.EqualTo(cpuType));
    }

    /// <summary>
    /// Test adding tags.
    /// </summary>
    [Test]
    public void AddUpdateTagItem_ShouldAddTagToCollection()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act
        var (tag, _) = plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");

        // Assert
        Assert.That(tag, Is.Not.Null);
        Assert.That(tag, Is.InstanceOf<Tag>());
        var typedTag = (Tag)tag!;
        Assert.That(typedTag.Name, Is.EqualTo("TestByte"));
        Assert.That(typedTag.Address, Is.EqualTo("DB1.DBB0"));
        Assert.That(typedTag.Type, Is.EqualTo(typeof(byte)));
    }

    /// <summary>
    /// Test array tags with specified length.
    /// </summary>
    [Test]
    public void AddUpdateTagItem_ArrayWithLength_ShouldSetCorrectArrayLength()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act
        var (tag, _) = plc.AddUpdateTagItem<byte[]>("TestByteArray", "DB1.DBB0", 64);

        // Assert
        Assert.That(tag, Is.Not.Null);
        var typedTag = (Tag)tag!;
        Assert.That(typedTag.Name, Is.EqualTo("TestByteArray"));
        Assert.That(typedTag.Type, Is.EqualTo(typeof(byte[])));
        Assert.That(typedTag.ArrayLength, Is.EqualTo(64));
    }

    /// <summary>
    /// Test removing tags.
    /// </summary>
    [Test]
    public void RemoveTagItem_ShouldRemoveTagFromCollection()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");

        // Act
        plc.RemoveTagItem("TestByte");

        // Assert
        Assert.That(plc.TagList.ContainsKey("TestByte"), Is.False);
    }

    /// <summary>
    /// Test observables are created correctly.
    /// </summary>
    [Test]
    public void Observables_ShouldBeCreated()
    {
        // Arrange & Act
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Assert
        Assert.That(plc.IsConnected, Is.Not.Null);
        Assert.That(plc.LastError, Is.Not.Null);
        Assert.That(plc.LastErrorCode, Is.Not.Null);
        Assert.That(plc.Status, Is.Not.Null);
        Assert.That(plc.ObserveAll, Is.Not.Null);
        Assert.That(plc.IsPaused, Is.Not.Null);
    }

    /// <summary>
    /// Test invalid rack parameter throws exception.
    /// </summary>
    /// <param name="invalidRack">Invalid rack value to test.</param>
    [TestCase(-1)]
    [TestCase(8)]
    public void S71500_Create_InvalidRack_ShouldThrowArgumentOutOfRangeException(short invalidRack)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, invalidRack, 1));
        Assert.That(ex.ParamName, Is.EqualTo("rack"));
    }

    /// <summary>
    /// Test invalid slot parameter throws exception.
    /// </summary>
    /// <param name="invalidSlot">Invalid slot value to test.</param>
    [TestCase(0)]
    [TestCase(32)]
    public void S71500_Create_InvalidSlot_ShouldThrowArgumentOutOfRangeException(short invalidSlot)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, invalidSlot));
        Assert.That(ex.ParamName, Is.EqualTo("slot"));
    }

    /// <summary>
    /// Test watchdog configuration.
    /// </summary>
    [Test]
    public void RxS7_WithWatchdog_ShouldSetWatchdogProperties()
    {
        // Arrange & Act
        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBW0", 100, 5000, 15);

        // Assert
        Assert.That(plc.WatchDogAddress, Is.EqualTo("DB10.DBW0"));
        Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(5000));
        Assert.That(plc.WatchDogWritingTime, Is.EqualTo(15));
    }

    /// <summary>
    /// Test invalid watchdog address throws exception.
    /// </summary>
    [Test]
    public void RxS7_WithInvalidWatchdogAddress_ShouldThrowArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBB0", 100, 5000, 15));
        Assert.That(ex.Message, Does.Contain("WatchDogAddress must be a DBW address"));
    }

    /// <summary>
    /// Test disposing of resources.
    /// </summary>
    [Test]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act
        plc.Dispose();

        // Assert
        Assert.That(plc.IsDisposed, Is.True);
    }
}
