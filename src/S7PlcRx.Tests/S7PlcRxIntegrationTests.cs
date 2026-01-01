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
    [Test]
    public void S7PlcCreation_WithDifferentTypes_ShouldWorkCorrectly()
    {
        // Test S71500 creation
        using var plc1500 = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc1500, Is.Not.Null);
        Assert.That(plc1500.PLCType, Is.EqualTo(CpuType.S71500));

        // Test S7400 creation
        using var plc400 = new RxS7(CpuType.S7400, MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc400, Is.Not.Null);
        Assert.That(plc400.PLCType, Is.EqualTo(CpuType.S7400));

        // Test S7300 creation
        using var plc300 = new RxS7(CpuType.S7300, MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc300, Is.Not.Null);
        Assert.That(plc300.PLCType, Is.EqualTo(CpuType.S7300));

        // Test S71200 creation
        using var plc1200 = new RxS7(CpuType.S71200, MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc1200, Is.Not.Null);
        Assert.That(plc1200.PLCType, Is.EqualTo(CpuType.S71200));

        // Test S7200 creation
        using var plc200 = new RxS7(CpuType.S7200, MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc200, Is.Not.Null);
        Assert.That(plc200.PLCType, Is.EqualTo(CpuType.S7200));
    }

    /// <summary>
    /// Test tag creation and management for different data types.
    /// </summary>
    [Test]
    public void TagManagement_WithDifferentDataTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert - Test different data types
        var (byteTag, _) = plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");
        Assert.That(byteTag, Is.Not.Null);
        Assert.That(((Tag)byteTag!).Type, Is.EqualTo(typeof(byte)));

        var (wordTag, _) = plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW2");
        Assert.That(wordTag, Is.Not.Null);
        Assert.That(((Tag)wordTag!).Type, Is.EqualTo(typeof(ushort)));

        var (intTag, _) = plc.AddUpdateTagItem<short>("TestInt", "DB1.DBW4");
        Assert.That(intTag, Is.Not.Null);
        Assert.That(((Tag)intTag!).Type, Is.EqualTo(typeof(short)));

        var (dwordTag, _) = plc.AddUpdateTagItem<uint>("TestDWord", "DB1.DBD6");
        Assert.That(dwordTag, Is.Not.Null);
        Assert.That(((Tag)dwordTag!).Type, Is.EqualTo(typeof(uint)));

        var (dintTag, _) = plc.AddUpdateTagItem<int>("TestDInt", "DB1.DBD10");
        Assert.That(dintTag, Is.Not.Null);
        Assert.That(((Tag)dintTag!).Type, Is.EqualTo(typeof(int)));

        var (realTag, _) = plc.AddUpdateTagItem<float>("TestReal", "DB1.DBD14");
        Assert.That(realTag, Is.Not.Null);
        Assert.That(((Tag)realTag!).Type, Is.EqualTo(typeof(float)));

        var (lrealTag, _) = plc.AddUpdateTagItem<double>("TestLReal", "DB1.DBD18");
        Assert.That(lrealTag, Is.Not.Null);
        Assert.That(((Tag)lrealTag!).Type, Is.EqualTo(typeof(double)));

        // Test arrays
        var (byteArrayTag, _) = plc.AddUpdateTagItem<byte[]>("TestByteArray", "DB1.DBB26", 10);
        Assert.That(byteArrayTag, Is.Not.Null);
        Assert.That(((Tag)byteArrayTag!).Type, Is.EqualTo(typeof(byte[])));
        Assert.That(((Tag)byteArrayTag!).ArrayLength, Is.EqualTo(10));

        var (realArrayTag, _) = plc.AddUpdateTagItem<float[]>("TestRealArray", "DB1.DBD36", 5);
        Assert.That(realArrayTag, Is.Not.Null);
        Assert.That(((Tag)realArrayTag!).Type, Is.EqualTo(typeof(float[])));
        Assert.That(((Tag)realArrayTag!).ArrayLength, Is.EqualTo(5));

        // Verify tags are in TagList
        Assert.That(plc.TagList.ContainsKey("TestByte"), Is.True);
        Assert.That(plc.TagList.ContainsKey("TestWord"), Is.True);
        Assert.That(plc.TagList.ContainsKey("TestReal"), Is.True);
        Assert.That(plc.TagList.ContainsKey("TestRealArray"), Is.True);
    }

    /// <summary>
    /// Test tag removal functionality.
    /// </summary>
    [Test]
    public void TagRemoval_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW2");

        // Act
        plc.RemoveTagItem("TestByte");

        // Assert
        Assert.That(plc.TagList.ContainsKey("TestByte"), Is.False);
        Assert.That(plc.TagList.ContainsKey("TestWord"), Is.True);

        // Cleanup remaining tag
        plc.RemoveTagItem("TestWord");
        Assert.That(plc.TagList.ContainsKey("TestWord"), Is.False);
    }

    /// <summary>
    /// Test tag observables creation.
    /// </summary>
    [Test]
    public void TagObservables_ShouldBeCreated()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");

        // Act
        var observable = plc.Observe<ushort>("TestWord");

        // Assert
        Assert.That(observable, Is.Not.Null);
        Assert.That(observable, Is.AssignableTo<IObservable<ushort>>());
    }

    /// <summary>
    /// Test watchdog configuration.
    /// </summary>
    [Test]
    public void WatchdogConfiguration_ShouldWorkCorrectly()
    {
        // Arrange & Act
        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBW100", 100, 4500, 10);

        // Assert
        Assert.That(plc.WatchDogAddress, Is.EqualTo("DB10.DBW100"));
        Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(4500));
        Assert.That(plc.WatchDogWritingTime, Is.EqualTo(10));

        // Test invalid watchdog address
        var ex = Assert.Throws<ArgumentException>(() => new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBB100", 100, 4500, 10));
        Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));
    }

    /// <summary>
    /// Test PLC status observables.
    /// </summary>
    [Test]
    public void PLCStatusObservables_ShouldBeCreated()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert
        Assert.That(plc.IsConnected, Is.Not.Null);
        Assert.That(plc.LastError, Is.Not.Null);
        Assert.That(plc.LastErrorCode, Is.Not.Null);
        Assert.That(plc.Status, Is.Not.Null);
        Assert.That(plc.ObserveAll, Is.Not.Null);
        Assert.That(plc.IsPaused, Is.Not.Null);
        Assert.That(plc.ReadTime, Is.Not.Null);
    }

    /// <summary>
    /// Test error handling with invalid parameters.
    /// </summary>
    [Test]
    public void ErrorHandling_WithInvalidParameters_ShouldThrowCorrectExceptions()
    {
        // Test invalid rack
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, -1, 1));
        Assert.That(ex1?.ParamName, Is.EqualTo("rack"));

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 8, 1));
        Assert.That(ex2?.ParamName, Is.EqualTo("rack"));

        // Test invalid slot
        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, 0));
        Assert.That(ex3?.ParamName, Is.EqualTo("slot"));

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, 32));
        Assert.That(ex4?.ParamName, Is.EqualTo("slot"));
    }

    /// <summary>
    /// Test address parsing for different memory areas.
    /// </summary>
    [Test]
    public void AddressParsing_WithDifferentMemoryAreas_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert - Data Block addresses
        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<byte>("DB_Test", "DB1.DBB0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<ushort>("DBW_Test", "DB1.DBW0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<uint>("DBD_Test", "DB1.DBD0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<bool>("DBX_Test", "DB1.DBX0.0"));

        // Input addresses
        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<byte>("IB_Test", "IB0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<ushort>("IW_Test", "IW0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<bool>("I_Test", "I0.0"));

        // Output addresses
        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<byte>("QB_Test", "QB0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<ushort>("QW_Test", "QW0"));

        // Memory addresses
        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<byte>("MB_Test", "MB0"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<ushort>("MW_Test", "MW0"));

        // Timer and Counter
        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<double>("T_Test", "T1"));

        Assert.DoesNotThrow(() => plc.AddUpdateTagItem<ushort>("C_Test", "C1"));
    }

    /// <summary>
    /// Test CPU information observable creation.
    /// </summary>
    [Test]
    public void GetCpuInfo_ShouldReturnObservable()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act
        var cpuInfoObservable = plc.GetCpuInfo();

        // Assert
        Assert.That(cpuInfoObservable, Is.Not.Null);
        Assert.That(cpuInfoObservable, Is.AssignableTo<IObservable<string[]>>());
    }

    /// <summary>
    /// Test high-frequency tag operations simulation.
    /// </summary>
    [Test]
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
        Assert.That(creationRate, Is.GreaterThan(100), "Tag creation should be fast");

        Assert.That(plc.TagList.Count, Is.EqualTo(tagCount), "All tags should be created");

        Console.WriteLine($"Tag creation rate: {creationRate:F1} tags/second");
    }

    /// <summary>
    /// Test memory usage patterns.
    /// </summary>
    [Test]
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
            Assert.That(observable, Is.Not.Null);
        }

        // Force garbage collection after operations
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryUsed = memoryAfter - memoryBefore;

        // Assert reasonable memory usage
        var memoryPerPLC = memoryUsed / plcCount;

        Assert.That(memoryPerPLC, Is.LessThan(1_000_000), $"Memory usage should be reasonable. Actual: {memoryPerPLC} bytes per PLC");

        Console.WriteLine($"Memory usage: {memoryPerPLC} bytes per PLC instance");
    }

    /// <summary>
    /// Test resource disposal.
    /// </summary>
    [Test]
    public void ResourceDisposal_ShouldCleanupCorrectly()
    {
        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestTag", "DB1.DBB0");

        // Act
        plc.Dispose();

        // Assert
        Assert.That(plc.IsDisposed, Is.True, "PLC should be marked as disposed");

        // Verify multiple dispose calls don't cause issues
        Assert.DoesNotThrow(() => plc.Dispose(), "Multiple dispose calls should be safe");
    }

    /// <summary>
    /// Test comprehensive PLC type coverage.
    /// </summary>
    /// <param name="cpuType">Type of the cpu.</param>
    [TestCase(CpuType.S71500)]
    [TestCase(CpuType.S7400)]
    [TestCase(CpuType.S7300)]
    [TestCase(CpuType.S71200)]
    [TestCase(CpuType.S7200)]
    public void PLCTypeSupport_ShouldCoverAllTypes(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, MockServer.Localhost, 0, 1, null, 100);

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.PLCType, Is.EqualTo(cpuType));
        Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        Assert.That(plc.Rack, Is.EqualTo(0));
        Assert.That(plc.Slot, Is.EqualTo(1));

        // Test tag creation works for all PLC types
        var (tag, _) = plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        Assert.That(tag, Is.Not.Null);
    }

    /// <summary>
    /// Test tag value setting and getting (synchronous).
    /// </summary>
    [Test]
    public void TagValueOperations_Synchronous_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");

        // Act - Set value (this will be queued for when connection is available)
        Assert.DoesNotThrow(() => plc.Value("TestWord", (ushort)1234), "Setting value should not throw");

        // The actual value setting will be attempted when PLC connects
        // For this test, we just verify the API works
        Assert.Pass("Tag value operations API test completed");
    }

    /// <summary>
    /// Test connection reconnection after simulated cable unplug.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
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
        Assert.That(plc.IsConnectedValue, Is.True, "PLC should reconnect after cable is plugged back");
    }

    /// <summary>
    /// Test connection reconnection after PLC stop and run.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
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
        Assert.That(plc.IsConnectedValue, Is.True, "PLC should reconnect after PLC is run again");
    }
}
