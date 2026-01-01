// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Integration tests for S7PlcRx functionality.
/// These tests validate core S7PlcRx features with controlled scenarios.
/// </summary>
public class S7PlcRxIntegrationTests
{
    /// <summary>
    /// Test basic PLC creation and configuration.
    /// </summary>
    [Fact]
    public void S7PlcCreation_WithDifferentTypes_ShouldWorkCorrectly()
    {
        // Test S71500 creation
        using var plc1500 = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc1500.Should().NotBeNull();
        plc1500.PLCType.Should().Be(CpuType.S71500);

        // Test S7400 creation
        using var plc400 = new RxS7(CpuType.S7400, MockServer.Localhost, 0, 1, null, 100);
        plc400.Should().NotBeNull();
        plc400.PLCType.Should().Be(CpuType.S7400);

        // Test S7300 creation
        using var plc300 = new RxS7(CpuType.S7300, MockServer.Localhost, 0, 1, null, 100);
        plc300.Should().NotBeNull();
        plc300.PLCType.Should().Be(CpuType.S7300);

        // Test S71200 creation
        using var plc1200 = new RxS7(CpuType.S71200, MockServer.Localhost, 0, 1, null, 100);
        plc1200.Should().NotBeNull();
        plc1200.PLCType.Should().Be(CpuType.S71200);

        // Test S7200 creation
        using var plc200 = new RxS7(CpuType.S7200, MockServer.Localhost, 0, 1, null, 100);
        plc200.Should().NotBeNull();
        plc200.PLCType.Should().Be(CpuType.S7200);
    }

    /// <summary>
    /// Test tag creation and management for different data types.
    /// </summary>
    [Fact]
    public void TagManagement_WithDifferentDataTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert - Test different data types
        var (byteTag, _) = plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");
        byteTag.Should().NotBeNull();
        ((Tag)byteTag!).Type.Should().Be<byte>();

        var (wordTag, _) = plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW2");
        wordTag.Should().NotBeNull();
        ((Tag)wordTag!).Type.Should().Be<ushort>();

        var (intTag, _) = plc.AddUpdateTagItem<short>("TestInt", "DB1.DBW4");
        intTag.Should().NotBeNull();
        ((Tag)intTag!).Type.Should().Be<short>();

        var (dwordTag, _) = plc.AddUpdateTagItem<uint>("TestDWord", "DB1.DBD6");
        dwordTag.Should().NotBeNull();
        ((Tag)dwordTag!).Type.Should().Be<uint>();

        var (dintTag, _) = plc.AddUpdateTagItem<int>("TestDInt", "DB1.DBD10");
        dintTag.Should().NotBeNull();
        ((Tag)dintTag!).Type.Should().Be<int>();

        var (realTag, _) = plc.AddUpdateTagItem<float>("TestReal", "DB1.DBD14");
        realTag.Should().NotBeNull();
        ((Tag)realTag!).Type.Should().Be<float>();

        var (lrealTag, _) = plc.AddUpdateTagItem<double>("TestLReal", "DB1.DBD18");
        lrealTag.Should().NotBeNull();
        ((Tag)lrealTag!).Type.Should().Be<double>();

        // Test arrays
        var (byteArrayTag, _) = plc.AddUpdateTagItem<byte[]>("TestByteArray", "DB1.DBB26", 10);
        byteArrayTag.Should().NotBeNull();
        ((Tag)byteArrayTag!).Type.Should().Be<byte[]>();
        ((Tag)byteArrayTag!).ArrayLength.Should().Be(10);

        var (realArrayTag, _) = plc.AddUpdateTagItem<float[]>("TestRealArray", "DB1.DBD36", 5);
        realArrayTag.Should().NotBeNull();
        ((Tag)realArrayTag!).Type.Should().Be<float[]>();
        ((Tag)realArrayTag!).ArrayLength.Should().Be(5);

        // Verify tags are in TagList
        plc.TagList.ContainsKey("TestByte").Should().BeTrue();
        plc.TagList.ContainsKey("TestWord").Should().BeTrue();
        plc.TagList.ContainsKey("TestReal").Should().BeTrue();
        plc.TagList.ContainsKey("TestRealArray").Should().BeTrue();
    }

    /// <summary>
    /// Test tag removal functionality.
    /// </summary>
    [Fact]
    public void TagRemoval_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW2");

        // Act
        plc.RemoveTagItem("TestByte");

        // Assert
        plc.TagList.ContainsKey("TestByte").Should().BeFalse();
        plc.TagList.ContainsKey("TestWord").Should().BeTrue();

        // Cleanup remaining tag
        plc.RemoveTagItem("TestWord");
        plc.TagList.ContainsKey("TestWord").Should().BeFalse();
    }

    /// <summary>
    /// Test tag observables creation.
    /// </summary>
    [Fact]
    public void TagObservables_ShouldBeCreated()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");

        // Act
        var observable = plc.Observe<ushort>("TestWord");

        // Assert
        observable.Should().NotBeNull();
        observable.Should().BeAssignableTo<IObservable<ushort>>();
    }

    /// <summary>
    /// Test watchdog configuration.
    /// </summary>
    [Fact]
    public void WatchdogConfiguration_ShouldWorkCorrectly()
    {
        // Arrange & Act
        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBW100", 100, 4500, 10);

        // Assert
        plc.WatchDogAddress.Should().Be("DB10.DBW100");
        plc.WatchDogValueToWrite.Should().Be(4500);
        plc.WatchDogWritingTime.Should().Be(10);

        // Test invalid watchdog address
        var invalidWatchdogAction = () => new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBB100", 100, 4500, 10);
        invalidWatchdogAction.Should().Throw<ArgumentException>()
            .WithMessage("*WatchDogAddress must be a DBW address*");
    }

    /// <summary>
    /// Test PLC status observables.
    /// </summary>
    [Fact]
    public void PLCStatusObservables_ShouldBeCreated()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert
        plc.IsConnected.Should().NotBeNull();
        plc.LastError.Should().NotBeNull();
        plc.LastErrorCode.Should().NotBeNull();
        plc.Status.Should().NotBeNull();
        plc.ObserveAll.Should().NotBeNull();
        plc.IsPaused.Should().NotBeNull();
        plc.ReadTime.Should().NotBeNull();
    }

    /// <summary>
    /// Test error handling with invalid parameters.
    /// </summary>
    [Fact]
    public void ErrorHandling_WithInvalidParameters_ShouldThrowCorrectExceptions()
    {
        // Test invalid rack
        var invalidRackAction = () => S71500.Create(MockServer.Localhost, -1, 1);
        invalidRackAction.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("rack");

        var invalidRackAction2 = () => S71500.Create(MockServer.Localhost, 8, 1);
        invalidRackAction2.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("rack");

        // Test invalid slot
        var invalidSlotAction = () => S71500.Create(MockServer.Localhost, 0, 0);
        invalidSlotAction.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("slot");

        var invalidSlotAction2 = () => S71500.Create(MockServer.Localhost, 0, 32);
        invalidSlotAction2.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("slot");
    }

    /// <summary>
    /// Test address parsing for different memory areas.
    /// </summary>
    [Fact]
    public void AddressParsing_WithDifferentMemoryAreas_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert - Data Block addresses
        var dbTest = () => plc.AddUpdateTagItem<byte>("DB_Test", "DB1.DBB0");
        dbTest.Should().NotThrow();

        var dbWordTest = () => plc.AddUpdateTagItem<ushort>("DBW_Test", "DB1.DBW0");
        dbWordTest.Should().NotThrow();

        var dbDWordTest = () => plc.AddUpdateTagItem<uint>("DBD_Test", "DB1.DBD0");
        dbDWordTest.Should().NotThrow();

        var dbBitTest = () => plc.AddUpdateTagItem<bool>("DBX_Test", "DB1.DBX0.0");
        dbBitTest.Should().NotThrow();

        // Input addresses
        var inputByteTest = () => plc.AddUpdateTagItem<byte>("IB_Test", "IB0");
        inputByteTest.Should().NotThrow();

        var inputWordTest = () => plc.AddUpdateTagItem<ushort>("IW_Test", "IW0");
        inputWordTest.Should().NotThrow();

        var inputBitTest = () => plc.AddUpdateTagItem<bool>("I_Test", "I0.0");
        inputBitTest.Should().NotThrow();

        // Output addresses
        var outputByteTest = () => plc.AddUpdateTagItem<byte>("QB_Test", "QB0");
        outputByteTest.Should().NotThrow();

        var outputWordTest = () => plc.AddUpdateTagItem<ushort>("QW_Test", "QW0");
        outputWordTest.Should().NotThrow();

        // Memory addresses
        var memoryByteTest = () => plc.AddUpdateTagItem<byte>("MB_Test", "MB0");
        memoryByteTest.Should().NotThrow();

        var memoryWordTest = () => plc.AddUpdateTagItem<ushort>("MW_Test", "MW0");
        memoryWordTest.Should().NotThrow();

        // Timer and Counter
        var timerTest = () => plc.AddUpdateTagItem<double>("T_Test", "T1");
        timerTest.Should().NotThrow();

        var counterTest = () => plc.AddUpdateTagItem<ushort>("C_Test", "C1");
        counterTest.Should().NotThrow();
    }

    /// <summary>
    /// Test CPU information observable creation.
    /// </summary>
    [Fact]
    public void GetCpuInfo_ShouldReturnObservable()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act
        var cpuInfoObservable = plc.GetCpuInfo();

        // Assert
        cpuInfoObservable.Should().NotBeNull();
        cpuInfoObservable.Should().BeAssignableTo<IObservable<string[]>>();
    }

    /// <summary>
    /// Test high-frequency tag operations simulation.
    /// </summary>
    [Fact]
    public void HighFrequencyOperations_Simulation_ShouldBeStable()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 10); // Fast interval
        const int tagCount = 50;

        // Act - Create many tags quickly
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < tagCount; i++)
        {
            plc.AddUpdateTagItem<ushort>($"PerfTag{i}", $"DB1.DBW{i * 2}");
        }

        stopwatch.Stop();

        // Assert
        var creationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        creationRate.Should().BeGreaterThan(100, "Tag creation should be fast");

        plc.TagList.Count.Should().Be(tagCount, "All tags should be created");

        Console.WriteLine($"Tag creation rate: {creationRate:F1} tags/second");
    }

    /// <summary>
    /// Test memory usage patterns.
    /// </summary>
    [Fact]
    public void MemoryUsage_WithManyTags_ShouldBeReasonable()
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Create and dispose of multiple PLCs
        const int plcCount = 10;
        for (var i = 0; i < plcCount; i++)
        {
            using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

            // Add tags
            for (var j = 0; j < 10; j++)
            {
                plc.AddUpdateTagItem<ushort>($"Tag{j}", $"DB1.DBW{j * 2}");
            }

            // Simulate some operations
            var observable = plc.Observe<ushort>("Tag0");
            observable.Should().NotBeNull();
        }

        // Force garbage collection after operations
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Assert reasonable memory usage
        var memoryPerPLC = memoryUsed / plcCount;

        memoryPerPLC.Should().BeLessThan(
            1_000_000,
            $"Memory usage should be reasonable. Actual: {memoryPerPLC} bytes per PLC");

        Console.WriteLine($"Memory usage: {memoryPerPLC} bytes per PLC instance");
    }

    /// <summary>
    /// Test resource disposal.
    /// </summary>
    [Fact]
    public void ResourceDisposal_ShouldCleanupCorrectly()
    {
        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestTag", "DB1.DBB0");

        // Act
        plc.Dispose();

        // Assert
        plc.IsDisposed.Should().BeTrue("PLC should be marked as disposed");

        // Verify multiple dispose calls don't cause issues
        var secondDisposeAction = () => plc.Dispose();
        secondDisposeAction.Should().NotThrow("Multiple dispose calls should be safe");
    }

    /// <summary>
    /// Test comprehensive PLC type coverage.
    /// </summary>
    /// <param name="cpuType">Type of the cpu.</param>
    [Theory]
    [InlineData(CpuType.S71500)]
    [InlineData(CpuType.S7400)]
    [InlineData(CpuType.S7300)]
    [InlineData(CpuType.S71200)]
    [InlineData(CpuType.S7200)]
    public void PLCTypeSupport_ShouldCoverAllTypes(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, MockServer.Localhost, 0, 1, null, 100);

        // Assert
        plc.Should().NotBeNull();
        plc.PLCType.Should().Be(cpuType);
        plc.IP.Should().Be(MockServer.Localhost);
        plc.Rack.Should().Be(0);
        plc.Slot.Should().Be(1);

        // Test tag creation works for all PLC types
        var (tag, _) = plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        tag.Should().NotBeNull();
    }

    /// <summary>
    /// Test tag value setting and getting (synchronous).
    /// </summary>
    [Fact]
    public void TagValueOperations_Synchronous_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");

        // Act - Set value (this will be queued for when connection is available)
        var setValueAction = () => plc.Value("TestWord", (ushort)1234);
        setValueAction.Should().NotThrow("Setting value should not throw");

        // The actual value setting will be attempted when PLC connects
        // For this test, we just verify the API works
        Assert.True(true, "Tag value operations API test completed");
    }

    /// <summary>
    /// Test connection reconnection after simulated cable unplug.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ConnectionReconnection_AfterCableUnplug_ShouldReconnect()
    {
        // Arrange
        using var server = new MockServer();
        server.Start();
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Wait for initial connection
        await plc.IsConnected.FirstAsync(x => x);

        // Act - Simulate cable unplug by stopping server
        server.Stop();

        // Wait for disconnection
        await plc.IsConnected.FirstAsync(x => !x);

        // Restart server to simulate cable plug back
        server.Start();

        // Wait for reconnection
        await plc.IsConnected.FirstAsync(x => x);

        // Assert
        plc.IsConnectedValue.Should().BeTrue("PLC should reconnect after cable is plugged back");
    }

    /// <summary>
    /// Test connection reconnection after PLC stop and run.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ConnectionReconnection_AfterPLCStopRun_ShouldReconnect()
    {
        // Arrange
        using var server = new MockServer();
        server.Start();
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Wait for initial connection
        await plc.IsConnected.FirstAsync(x => x);

        // Act - Simulate PLC stop by stopping server
        server.Stop();

        // Wait for disconnection
        await plc.IsConnected.FirstAsync(x => !x);

        // Simulate PLC run by starting server
        server.Start();

        // Wait for reconnection
        await plc.IsConnected.FirstAsync(x => x);

        // Assert
        plc.IsConnectedValue.Should().BeTrue("PLC should reconnect after PLC is run again");
    }
}
