// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MockS7Plc;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Comprehensive tests for S7PlcRx functionality covering all PLC types and operations.
/// These tests validate the complete S7PlcRx library functionality without requiring physical PLCs.
/// </summary>
public class S7PlcRxComprehensiveTests
{
    private static readonly float[] value = [1.1f, 2.2f, 3.3f, 4.4f, 5.5f];

    /// <summary>
    /// Test creation of all supported PLC types.
    /// </summary>
    /// <param name="cpuType">Type of the cpu.</param>
    [TestCase(CpuType.S71500)]
    [TestCase(CpuType.S7400)]
    [TestCase(CpuType.S7300)]
    [TestCase(CpuType.S71200)]
    [TestCase(CpuType.S7200)]
    [TestCase(CpuType.Logo0BA8)]
    public void CreatePLC_AllSupportedTypes_ShouldSetCorrectProperties(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, MockServer.Localhost, 0, 1, null, 100);

        // Assert
        Assert.That(plc, Is.Not.Null);
        Assert.That(plc.PLCType, Is.EqualTo(cpuType));
        Assert.That(plc.IP, Is.EqualTo(MockServer.Localhost));
        Assert.That(plc.Rack, Is.EqualTo(0));
        Assert.That(plc.Slot, Is.EqualTo(1));
        Assert.That(plc.IsDisposed, Is.False);
    }

    /// <summary>
    /// Test S71500 factory method with different configurations.
    /// </summary>
    [Test]
    public void S71500Factory_WithDifferentConfigurations_ShouldCreateCorrectly()
    {
        // Test basic creation
        using var plc1 = S71500.Create(MockServer.Localhost, 0, 1);
        Assert.That(plc1.PLCType, Is.EqualTo(CpuType.S71500));

        // Test with interval
        using var plc2 = S71500.Create(MockServer.Localhost, 0, 1, null, 50);
        Assert.That(plc2.PLCType, Is.EqualTo(CpuType.S71500));

        // Test with watchdog
        using var plc3 = S71500.Create(MockServer.Localhost, 0, 1, "DB1.DBW0", 100);
        Assert.That(plc3.WatchDogAddress, Is.EqualTo("DB1.DBW0"));
    }

    /// <summary>
    /// Test comprehensive tag creation for all supported data types.
    /// </summary>
    [Test]
    public void TagCreation_AllDataTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Act & Assert - Basic types
        var (byteTag, _) = plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB0");
        ValidateTag(byteTag, "TestByte", "DB1.DBB0", typeof(byte));

        var (wordTag, _) = plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW2");
        ValidateTag(wordTag, "TestWord", "DB1.DBW2", typeof(ushort));

        var (intTag, _) = plc.AddUpdateTagItem<short>("TestInt", "DB1.DBW4");
        ValidateTag(intTag, "TestInt", "DB1.DBW4", typeof(short));

        var (dwordTag, _) = plc.AddUpdateTagItem<uint>("TestDWord", "DB1.DBD6");
        ValidateTag(dwordTag, "TestDWord", "DB1.DBD6", typeof(uint));

        var (dintTag, _) = plc.AddUpdateTagItem<int>("TestDInt", "DB1.DBD10");
        ValidateTag(dintTag, "TestDInt", "DB1.DBD10", typeof(int));

        var (realTag, _) = plc.AddUpdateTagItem<float>("TestReal", "DB1.DBD14");
        ValidateTag(realTag, "TestReal", "DB1.DBD14", typeof(float));

        var (lrealTag, _) = plc.AddUpdateTagItem<double>("TestLReal", "DB1.DBD18");
        ValidateTag(lrealTag, "TestLReal", "DB1.DBD18", typeof(double));

        var (boolTag, _) = plc.AddUpdateTagItem<bool>("TestBool", "DB1.DBX26.0");
        ValidateTag(boolTag, "TestBool", "DB1.DBX26.0", typeof(bool));

        // Array types
        var (byteArrayTag, _) = plc.AddUpdateTagItem<byte[]>("TestByteArray", "DB1.DBB30", 10);
        ValidateArrayTag(byteArrayTag, "TestByteArray", "DB1.DBB30", typeof(byte[]), 10);

        var (wordArrayTag, _) = plc.AddUpdateTagItem<ushort[]>("TestWordArray", "DB1.DBW40", 5);
        ValidateArrayTag(wordArrayTag, "TestWordArray", "DB1.DBW40", typeof(ushort[]), 5);

        var (realArrayTag, _) = plc.AddUpdateTagItem<float[]>("TestRealArray", "DB1.DBD50", 8);
        ValidateArrayTag(realArrayTag, "TestRealArray", "DB1.DBD50", typeof(float[]), 8);

        // Verify all tags are in TagList
        Assert.That(plc.TagList.Count, Is.EqualTo(11));
        var tagKeys = new[] { "TestByte", "TestWord", "TestReal", "TestRealArray" };
        foreach (var key in tagKeys)
        {
            Assert.That(plc.TagList.ContainsKey(key), Is.True, $"TagList should contain key '{key}'");
        }
    }

    /// <summary>
    /// Test memory area addressing for all supported types.
    /// </summary>
    [Test]
    public void MemoryAreaAddressing_AllTypes_ShouldBeSupported()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Data Block addressing
        var dbTests = new[]
        {
            () => plc.AddUpdateTagItem<byte>("DB_Byte", "DB1.DBB0"),
            () => plc.AddUpdateTagItem<ushort>("DB_Word", "DB1.DBW0"),
            () => plc.AddUpdateTagItem<uint>("DB_DWord", "DB1.DBD0"),
            () => plc.AddUpdateTagItem<bool>("DB_Bit", "DB1.DBX0.0"),
            () => plc.AddUpdateTagItem<byte[]>("DB_ByteArray", "DB1.DBB10", 5)
        };

        foreach (var test in dbTests)
        {
            Assert.DoesNotThrow(() => test(), "Data Block addressing should work");
        }

        // Input addressing
        var inputTests = new[]
        {
            () => plc.AddUpdateTagItem<byte>("Input_Byte", "IB0"),
            () => plc.AddUpdateTagItem<ushort>("Input_Word", "IW0"),
            () => plc.AddUpdateTagItem<uint>("Input_DWord", "ID0"),
            () => plc.AddUpdateTagItem<bool>("Input_Bit", "I0.0")
        };

        foreach (var test in inputTests)
        {
            Assert.DoesNotThrow(() => test(), "Input addressing should work");
        }

        // Output addressing
        var outputTests = new[]
        {
            () => plc.AddUpdateTagItem<byte>("Output_Byte", "QB0"),
            () => plc.AddUpdateTagItem<ushort>("Output_Word", "QW0"),
            () => plc.AddUpdateTagItem<uint>("Output_DWord", "QD0"),
            () => plc.AddUpdateTagItem<bool>("Output_Bit", "Q0.0")
        };

        foreach (var test in outputTests)
        {
            Assert.DoesNotThrow(() => test(), "Output addressing should work");
        }

        // Memory addressing
        var memoryTests = new[]
        {
            () => plc.AddUpdateTagItem<byte>("Memory_Byte", "MB0"),
            () => plc.AddUpdateTagItem<ushort>("Memory_Word", "MW0"),
            () => plc.AddUpdateTagItem<double>("Memory_LReal", "MD0"),
            () => plc.AddUpdateTagItem<bool>("Memory_Bit", "M0.0")
        };

        foreach (var test in memoryTests)
        {
            Assert.DoesNotThrow(() => test(), "Memory addressing should work");
        }

        // Timer and Counter
        Assert.That(plc.AddUpdateTagItem<double>("Timer_Test", "T1"), Is.Not.Null);
        Assert.That(plc.AddUpdateTagItem<ushort>("Counter_Test", "C1"), Is.Not.Null);
    }

    /// <summary>
    /// Test tag management operations.
    /// </summary>
    [Test]
    public void TagManagement_Operations_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Add initial tags
        plc.AddUpdateTagItem<byte>("Tag1", "DB1.DBB0");
        plc.AddUpdateTagItem<ushort>("Tag2", "DB1.DBW2");
        plc.AddUpdateTagItem<float>("Tag3", "DB1.DBD4");

        Assert.That(plc.TagList.Count, Is.EqualTo(3));

        // Test tag update (adding existing tag should update it)
        var (updatedTag, _) = plc.AddUpdateTagItem<byte>("Tag1", "DB1.DBB10"); // Different address
        Assert.That(((Tag)updatedTag!).Address, Is.EqualTo("DB1.DBB10"));
        Assert.That(plc.TagList.Count, Is.EqualTo(3)); // Count should remain the same

        // Test tag removal
        plc.RemoveTagItem("Tag2");
        Assert.That(plc.TagList.Count, Is.EqualTo(2));
        Assert.That(plc.TagList.ContainsKey("Tag2"), Is.False);

        // Test removing non-existent tag (should not throw)
        Assert.DoesNotThrow(() => plc.RemoveTagItem("NonExistentTag"));

        // Test tag retrieval
        var (retrievedTag, _) = plc.GetTag("Tag1");
        Assert.That(retrievedTag, Is.Not.Null);
        Assert.That(((Tag)retrievedTag!).Name, Is.EqualTo("Tag1"));

        var (nonExistentTag, _) = plc.GetTag("NonExistentTag");
        Assert.That(nonExistentTag, Is.Null);
    }

    /// <summary>
    /// Test observable creation and basic functionality.
    /// </summary>
    [Test]
    public void Observables_ShouldBeCreatedAndFunctional()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Test core observables exist
        Assert.That(plc.IsConnected, Is.Not.Null);
        Assert.That(plc.LastError, Is.Not.Null);
        Assert.That(plc.LastErrorCode, Is.Not.Null);
        Assert.That(plc.Status, Is.Not.Null);
        Assert.That(plc.ObserveAll, Is.Not.Null);
        Assert.That(plc.IsPaused, Is.Not.Null);
        Assert.That(plc.ReadTime, Is.Not.Null);

        // Test tag observables
        plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        var tagObservable = plc.Observe<ushort>("TestTag");
        Assert.That(tagObservable, Is.Not.Null);
        Assert.That(tagObservable, Is.AssignableTo<IObservable<ushort>>());

        // Test GetCpuInfo observable
        var cpuInfoObservable = plc.GetCpuInfo();
        Assert.That(cpuInfoObservable, Is.Not.Null);
        Assert.That(cpuInfoObservable, Is.AssignableTo<IObservable<string[]>>());
    }

    /// <summary>
    /// Test watchdog configuration and validation.
    /// </summary>
    [Test]
    public void WatchdogConfiguration_ShouldWorkCorrectly()
    {
        // Test valid watchdog configuration
        using var plc1 = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBW100", 100, 4500, 15);
        Assert.That(plc1.WatchDogAddress, Is.EqualTo("DB10.DBW100"));
        Assert.That(plc1.WatchDogValueToWrite, Is.EqualTo(4500));
        Assert.That(plc1.WatchDogWritingTime, Is.EqualTo(15));

        // Test invalid watchdog address (non-DBW)
        var ex = Assert.Throws<ArgumentException>(() => new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB10.DBB100", 100));
        Assert.That(ex?.Message, Does.Contain("WatchDogAddress must be a DBW address"));

        // Test without watchdog
        using var plc2 = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, 100);
        Assert.That(plc2.WatchDogAddress, Is.Null);
    }

    /// <summary>
    /// Test error handling for invalid parameters.
    /// </summary>
    [Test]
    public void ErrorHandling_InvalidParameters_ShouldThrowCorrectExceptions()
    {
        // Invalid rack values
        var ex1 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, -1, 1));
        Assert.That(ex1?.ParamName, Is.EqualTo("rack"));

        var ex2 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 8, 1));
        Assert.That(ex2?.ParamName, Is.EqualTo("rack"));

        // Invalid slot values
        var ex3 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, 0));
        Assert.That(ex3?.ParamName, Is.EqualTo("slot"));

        var ex4 = Assert.Throws<ArgumentOutOfRangeException>(() => S71500.Create(MockServer.Localhost, 0, 32));
        Assert.That(ex4?.ParamName, Is.EqualTo("slot"));

        // Invalid tag operations
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        Assert.Throws<ArgumentNullException>(() => plc.RemoveTagItem(null!));

        Assert.Throws<ArgumentNullException>(() => plc.RemoveTagItem(string.Empty));
    }

    /// <summary>
    /// Test performance characteristics and resource usage.
    /// </summary>
    [Test]
    public void Performance_HighVolumeOperations_ShouldBeEfficient()
    {
        // Test rapid tag creation
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 10); // Fast interval

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        const int tagCount = 100;

        for (var i = 0; i < tagCount; i++)
        {
            plc.AddUpdateTagItem<ushort>($"PerfTag{i}", $"DB1.DBW{i * 2}");
        }

        stopwatch.Stop();

        var creationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        Assert.That(creationRate, Is.GreaterThan(1000), "Tag creation should be very fast");

        Assert.That(plc.TagList.Count, Is.EqualTo(tagCount));

        // Test rapid observable creation
        stopwatch.Restart();
        var observables = new List<IObservable<ushort>>();

        for (var i = 0; i < tagCount; i++)
        {
            observables.Add(plc.Observe<ushort>($"PerfTag{i}"));
        }

        stopwatch.Stop();

        var observableCreationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        Assert.That(observableCreationRate, Is.GreaterThan(1000), "Observable creation should be very fast");

        Console.WriteLine($"Tag creation rate: {creationRate:F0} tags/second");
        Console.WriteLine($"Observable creation rate: {observableCreationRate:F0} observables/second");
    }

    /// <summary>
    /// Test memory usage patterns.
    /// </summary>
    [Test]
    public void MemoryUsage_MultipleInstances_ShouldBeReasonable()
    {
        // Force garbage collection before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Create multiple PLC instances
        const int instanceCount = 20;
        var plcInstances = new List<IRxS7>();

        try
        {
            for (var i = 0; i < instanceCount; i++)
            {
                var plc = S71500.Create($"192.168.1.{100 + i}", 0, 1, null, 100);
                plcInstances.Add(plc);

                // Add some tags to each instance
                for (var j = 0; j < 5; j++)
                {
                    plc.AddUpdateTagItem<ushort>($"Tag{j}", $"DB1.DBW{j * 2}");
                }
            }

            var memoryAfterCreation = GC.GetTotalMemory(false);
            var memoryPerInstance = (memoryAfterCreation - memoryBefore) / instanceCount;

            Assert.That(memoryPerInstance, Is.LessThan(500_000), $"Memory usage per PLC instance should be reasonable. Actual: {memoryPerInstance} bytes");

            Console.WriteLine($"Memory usage per PLC instance: {memoryPerInstance:N0} bytes");
        }
        finally
        {
            // Cleanup
            foreach (var plc in plcInstances)
            {
                plc.Dispose();
            }
        }

        // Force garbage collection after disposal
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Test disposal and resource cleanup.
    /// </summary>
    [Test]
    public void Disposal_ShouldCleanupResourcesProperly()
    {
        // Arrange
        var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestTag", "DB1.DBB0");

        Assert.That(plc.IsDisposed, Is.False);

        // Act
        plc.Dispose();

        // Assert
        Assert.That(plc.IsDisposed, Is.True);

        // Test multiple dispose calls
        Assert.DoesNotThrow(() => plc.Dispose(), "Multiple dispose calls should be safe");

        Assert.That(plc.IsDisposed, Is.True);
    }

    /// <summary>
    /// Test tag polling control.
    /// </summary>
    [Test]
    public void TagPolling_Control_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Test tag polling configuration
        var (tag, _) = plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        var tagInstance = (Tag)tag!;

        // Default should be to poll
        Assert.That(tagInstance.DoNotPoll, Is.False);

        // Test disabling polling
        var (_, _) = plc.GetTag("TestTag").SetTagPollIng(false);
        Assert.That(tagInstance.DoNotPoll, Is.True);

        // Test enabling polling
        plc.GetTag("TestTag").SetTagPollIng(true);
        Assert.That(tagInstance.DoNotPoll, Is.False);
    }

    /// <summary>
    /// Test value operations (synchronous API).
    /// </summary>
    [Test]
    public void ValueOperations_SynchronousAPI_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");
        plc.AddUpdateTagItem<float>("TestReal", "DB1.DBD4");

        // Test setting values (these will be queued for when connection is available)
        Assert.DoesNotThrow(() => plc.Value("TestWord", (ushort)12345), "Setting Word value should not throw");

        Assert.DoesNotThrow(() => plc.Value("TestReal", 3.14159f), "Setting Real value should not throw");

        // Test with different data types
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB8");
        Assert.DoesNotThrow(() => plc.Value("TestByte", (byte)255), "Setting Byte value should not throw");

        plc.AddUpdateTagItem<bool>("TestBool", "DB1.DBX10.0");
        Assert.DoesNotThrow(() => plc.Value("TestBool", true), "Setting Bool value should not throw");
    }

    /// <summary>
    /// Test reactive extensions integration.
    /// </summary>
    [Test]
    public void ReactiveExtensions_Integration_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, 100);

        // Test tag to dictionary conversion
        plc.AddUpdateTagItem<ushort>("Tag1", "DB1.DBW0");
        plc.AddUpdateTagItem<ushort>("Tag2", "DB1.DBW2");

        var observable = plc.Observe<ushort>("Tag1");
        var tagValueObservable = observable.ToTagValue("Tag1");

        Assert.That(tagValueObservable, Is.Not.Null);
        Assert.That(tagValueObservable, Is.AssignableTo<IObservable<(string Tag, ushort Value)>>());

        // Test ObserveAll to dictionary conversion
        var dictionaryObservable = plc.ObserveAll.TagToDictionary<object>();
        Assert.That(dictionaryObservable, Is.Not.Null);
        Assert.That(dictionaryObservable, Is.AssignableTo<IObservable<IDictionary<string, object>>>());
    }

    /// <summary>
    /// Test comprehensive scenario with mixed operations.
    /// </summary>
    [Test]
    public void ComprehensiveScenario_MixedOperations_ShouldWorkTogether()
    {
        // Arrange - Create PLC with watchdog
        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB100.DBW0", 50, 4500, 10);

        // Add various tags
        plc.AddUpdateTagItem<byte>("ProcessByte", "DB1.DBB0");
        plc.AddUpdateTagItem<ushort>("ProcessWord", "DB1.DBW2");
        plc.AddUpdateTagItem<float>("ProcessReal", "DB1.DBD4");
        plc.AddUpdateTagItem<bool>("ProcessBit", "DB1.DBX8.0");
        plc.AddUpdateTagItem<float[]>("ProcessArray", "DB1.DBD10", 10);

        // Test inputs and outputs
        plc.AddUpdateTagItem<ushort>("InputWord", "IW0");
        plc.AddUpdateTagItem<ushort>("OutputWord", "QW0");

        // Test memory areas
        plc.AddUpdateTagItem<byte>("MemoryByte", "MB100");
        plc.AddUpdateTagItem<bool>("MemoryBit", "M100.0");

        // Test timers and counters
        plc.AddUpdateTagItem<double>("Timer1", "T1");
        plc.AddUpdateTagItem<ushort>("Counter1", "C1");

        // Verify all tags created, including the watchdog
        Assert.That(plc.TagList.Count, Is.EqualTo(12));

        // Test observables for different types
        var byteObs = plc.Observe<byte>("ProcessByte");
        var wordObs = plc.Observe<ushort>("ProcessWord");
        var realObs = plc.Observe<float>("ProcessReal");
        var boolObs = plc.Observe<bool>("ProcessBit");
        var arrayObs = plc.Observe<float[]>("ProcessArray");

        // All observables should be created
        Assert.That(byteObs, Is.Not.Null);
        Assert.That(wordObs, Is.Not.Null);
        Assert.That(realObs, Is.Not.Null);
        Assert.That(boolObs, Is.Not.Null);
        Assert.That(arrayObs, Is.Not.Null);

        // Test value setting operations
        var valueOperations = new Action[]
        {
            () => plc.Value("ProcessByte", (byte)100),
            () => plc.Value("ProcessWord", (ushort)1000),
            () => plc.Value("ProcessReal", 123.456f),
            () => plc.Value("ProcessBit", true),
            () => plc.Value("ProcessArray", value),
            () => plc.Value("InputWord", (ushort)2000),
            () => plc.Value("OutputWord", (ushort)3000),
            () => plc.Value("MemoryByte", (byte)200),
            () => plc.Value("MemoryBit", false)
        };

        foreach (var operation in valueOperations)
        {
            Assert.DoesNotThrow(() => operation(), "Value setting operations should not throw");
        }

        // Test tag management during runtime
        plc.RemoveTagItem("ProcessBit");
        Assert.That(plc.TagList.Count, Is.EqualTo(11));

        var (newTag, _) = plc.AddUpdateTagItem<int>("ProcessDInt", "DB1.DBD20");
        Assert.That(newTag, Is.Not.Null);
        Assert.That(plc.TagList.Count, Is.EqualTo(12));

        // Verify watchdog configuration
        Assert.That(plc.WatchDogAddress, Is.EqualTo("DB100.DBW0"));
        Assert.That(plc.WatchDogValueToWrite, Is.EqualTo(4500));
        Assert.That(plc.WatchDogWritingTime, Is.EqualTo(10));

        // Test tag polling control
        plc.GetTag("ProcessWord").SetTagPollIng(false);
        Assert.That(((Tag)plc.TagList["ProcessWord"]!).DoNotPoll, Is.True);

        // Test system observables
        var statusObs = plc.Status;
        var errorObs = plc.LastError;
        var connectedObs = plc.IsConnected;

        Assert.That(statusObs, Is.Not.Null);
        Assert.That(errorObs, Is.Not.Null);
        Assert.That(connectedObs, Is.Not.Null);

        Assert.Pass("Comprehensive scenario completed successfully");
    }

    private static void ValidateTag(ITag? tag, string expectedName, string expectedAddress, Type expectedType)
    {
        Assert.That(tag, Is.Not.Null);
        var typedTag = (Tag)tag!;
        Assert.That(typedTag.Name, Is.EqualTo(expectedName));
        Assert.That(typedTag.Address, Is.EqualTo(expectedAddress));
        Assert.That(typedTag.Type, Is.EqualTo(expectedType));
    }

    private static void ValidateArrayTag(ITag? tag, string expectedName, string expectedAddress, Type expectedType, int expectedArrayLength)
    {
        ValidateTag(tag, expectedName, expectedAddress, expectedType);
        var typedTag = (Tag)tag!;
        Assert.That(typedTag.ArrayLength, Is.EqualTo(expectedArrayLength));
    }
}
