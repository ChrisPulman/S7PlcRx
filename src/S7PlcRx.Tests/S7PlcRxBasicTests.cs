// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    [Fact]
    public void S71500_Create_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        using var plc = S71500.Create("192.168.1.100", 0, 1, null, 100);

        // Assert
        plc.Should().NotBeNull();
        plc.IP.Should().Be("192.168.1.100");
        plc.PLCType.Should().Be(CpuType.S71500);
        plc.Rack.Should().Be(0);
        plc.Slot.Should().Be(1);
    }

    /// <summary>
    /// Test that different PLC types can be created.
    /// </summary>
    /// <param name="cpuType">The CPU type to test.</param>
    [Theory]
    [InlineData(CpuType.S71500)]
    [InlineData(CpuType.S7300)]
    [InlineData(CpuType.S7400)]
    [InlineData(CpuType.S71200)]
    [InlineData(CpuType.S7200)]
    public void RxS7_Create_DifferentTypes_ShouldSetCorrectCpuType(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, "127.0.0.1", 0, 1, null, 100);

        // Assert
        plc.Should().NotBeNull();
        plc.PLCType.Should().Be(cpuType);
    }

    /// <summary>
    /// Test adding tags.
    /// </summary>
    [Fact]
    public void AddUpdateTagItem_ShouldAddTagToCollection()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Act
        var (tag, _) = plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");

        // Assert
        tag.Should().NotBeNull();
        tag.Should().BeOfType<Tag>();
        var typedTag = (Tag)tag!;
        typedTag.Name.Should().Be("TestByte");
        typedTag.Address.Should().Be("DB1.DBB0");
        typedTag.Type.Should().Be(typeof(byte));
    }

    /// <summary>
    /// Test array tags with specified length.
    /// </summary>
    [Fact]
    public void AddUpdateTagItem_ArrayWithLength_ShouldSetCorrectArrayLength()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Act
        var (tag, _) = plc.AddUpdateTagItem<byte[]>("TestByteArray", "DB1.DBB0", 64);

        // Assert
        tag.Should().NotBeNull();
        var typedTag = (Tag)tag!;
        typedTag.Name.Should().Be("TestByteArray");
        typedTag.Type.Should().Be(typeof(byte[]));
        typedTag.ArrayLength.Should().Be(64);
    }

    /// <summary>
    /// Test removing tags.
    /// </summary>
    [Fact]
    public void RemoveTagItem_ShouldRemoveTagFromCollection()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");

        // Act
        plc.RemoveTagItem("TestByte");

        // Assert
        plc.TagList.ContainsKey("TestByte").Should().BeFalse();
    }

    /// <summary>
    /// Test observables are created correctly.
    /// </summary>
    [Fact]
    public void Observables_ShouldBeCreated()
    {
        // Arrange & Act
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Assert
        plc.IsConnected.Should().NotBeNull();
        plc.LastError.Should().NotBeNull();
        plc.LastErrorCode.Should().NotBeNull();
        plc.Status.Should().NotBeNull();
        plc.ObserveAll.Should().NotBeNull();
        plc.IsPaused.Should().NotBeNull();
    }

    /// <summary>
    /// Test invalid rack parameter throws exception.
    /// </summary>
    /// <param name="invalidRack">Invalid rack value to test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void S71500_Create_InvalidRack_ShouldThrowArgumentOutOfRangeException(short invalidRack)
    {
        // Act & Assert
        var act = () => S71500.Create("127.0.0.1", invalidRack, 1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("rack");
    }

    /// <summary>
    /// Test invalid slot parameter throws exception.
    /// </summary>
    /// <param name="invalidSlot">Invalid slot value to test.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void S71500_Create_InvalidSlot_ShouldThrowArgumentOutOfRangeException(short invalidSlot)
    {
        // Act & Assert
        var act = () => S71500.Create("127.0.0.1", 0, invalidSlot);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("slot");
    }

    /// <summary>
    /// Test watchdog configuration.
    /// </summary>
    [Fact]
    public void RxS7_WithWatchdog_ShouldSetWatchdogProperties()
    {
        // Arrange & Act
        using var plc = new RxS7(CpuType.S71500, "127.0.0.1", 0, 1, "DB10.DBW0", 100, 5000, 15);

        // Assert
        plc.WatchDogAddress.Should().Be("DB10.DBW0");
        plc.WatchDogValueToWrite.Should().Be(5000);
        plc.WatchDogWritingTime.Should().Be(15);
    }

    /// <summary>
    /// Test invalid watchdog address throws exception.
    /// </summary>
    [Fact]
    public void RxS7_WithInvalidWatchdogAddress_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => new RxS7(CpuType.S71500, "127.0.0.1", 0, 1, "DB10.DBB0", 100, 5000, 15);
        act.Should().Throw<ArgumentException>()
            .WithMessage("WatchDogAddress must be a DBW address.*");
    }

    /// <summary>
    /// Test disposing of resources.
    /// </summary>
    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Act
        plc.Dispose();

        // Assert
        plc.IsDisposed.Should().BeTrue();
    }
}
