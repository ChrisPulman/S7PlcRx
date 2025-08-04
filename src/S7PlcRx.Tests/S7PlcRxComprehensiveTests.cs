// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Comprehensive tests for S7PlcRx functionality covering all PLC types and operations.
/// These tests validate the complete S7PlcRx library functionality without requiring physical PLCs.
/// </summary>
public class S7PlcRxComprehensiveTests
{
    /// <summary>
    /// Test creation of all supported PLC types.
    /// </summary>
    /// <param name="cpuType">Type of the cpu.</param>
    [Theory]
    [InlineData(CpuType.S71500)]
    [InlineData(CpuType.S7400)]
    [InlineData(CpuType.S7300)]
    [InlineData(CpuType.S71200)]
    [InlineData(CpuType.S7200)]
    [InlineData(CpuType.Logo0BA8)]
    public void CreatePLC_AllSupportedTypes_ShouldSetCorrectProperties(CpuType cpuType)
    {
        // Arrange & Act
        using var plc = new RxS7(cpuType, "192.168.1.100", 0, 1, null, 100);

        // Assert
        plc.Should().NotBeNull();
        plc.PLCType.Should().Be(cpuType);
        plc.IP.Should().Be("192.168.1.100");
        plc.Rack.Should().Be(0);
        plc.Slot.Should().Be(1);
        plc.IsDisposed.Should().BeFalse();
    }

    /// <summary>
    /// Test S71500 factory method with different configurations.
    /// </summary>
    [Fact]
    public void S71500Factory_WithDifferentConfigurations_ShouldCreateCorrectly()
    {
        // Test basic creation
        using var plc1 = S71500.Create("192.168.1.100", 0, 1);
        plc1.PLCType.Should().Be(CpuType.S71500);

        // Test with interval
        using var plc2 = S71500.Create("192.168.1.100", 0, 1, null, 50);
        plc2.PLCType.Should().Be(CpuType.S71500);

        // Test with watchdog
        using var plc3 = S71500.Create("192.168.1.100", 0, 1, "DB1.DBW0", 100);
        plc3.WatchDogAddress.Should().Be("DB1.DBW0");
    }

    /// <summary>
    /// Test comprehensive tag creation for all supported data types.
    /// </summary>
    [Fact]
    public void TagCreation_AllDataTypes_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

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
        plc.TagList.Count.Should().Be(11);
        var tagKeys = new[] { "TestByte", "TestWord", "TestReal", "TestRealArray" };
        foreach (var key in tagKeys)
        {
            plc.TagList.ContainsKey(key).Should().BeTrue($"TagList should contain key '{key}'");
        }
    }

    /// <summary>
    /// Test memory area addressing for all supported types.
    /// </summary>
    [Fact]
    public void MemoryAreaAddressing_AllTypes_ShouldBeSupported()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

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
            test.Should().NotThrow("Data Block addressing should work");
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
            test.Should().NotThrow("Input addressing should work");
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
            test.Should().NotThrow("Output addressing should work");
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
            test.Should().NotThrow("Memory addressing should work");
        }

        // Timer and Counter
        plc.AddUpdateTagItem<double>("Timer_Test", "T1").Should().NotBeNull();
        plc.AddUpdateTagItem<ushort>("Counter_Test", "C1").Should().NotBeNull();
    }

    /// <summary>
    /// Test tag management operations.
    /// </summary>
    [Fact]
    public void TagManagement_Operations_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Add initial tags
        plc.AddUpdateTagItem<byte>("Tag1", "DB1.DBB0");
        plc.AddUpdateTagItem<ushort>("Tag2", "DB1.DBW2");
        plc.AddUpdateTagItem<float>("Tag3", "DB1.DBD4");

        plc.TagList.Count.Should().Be(3);

        // Test tag update (adding existing tag should update it)
        var (updatedTag, _) = plc.AddUpdateTagItem<byte>("Tag1", "DB1.DBB10"); // Different address
        ((Tag)updatedTag!).Address.Should().Be("DB1.DBB10");
        plc.TagList.Count.Should().Be(3); // Count should remain the same

        // Test tag removal
        plc.RemoveTagItem("Tag2");
        plc.TagList.Count.Should().Be(2);
        plc.TagList.ContainsKey("Tag2").Should().BeFalse();

        // Test removing non-existent tag (should not throw)
        var removeAction = () => plc.RemoveTagItem("NonExistentTag");
        removeAction.Should().NotThrow();

        // Test tag retrieval
        var (retrievedTag, _) = plc.GetTag("Tag1");
        retrievedTag.Should().NotBeNull();
        ((Tag)retrievedTag!).Name.Should().Be("Tag1");

        var (nonExistentTag, _) = plc.GetTag("NonExistentTag");
        nonExistentTag.Should().BeNull();
    }

    /// <summary>
    /// Test observable creation and basic functionality.
    /// </summary>
    [Fact]
    public void Observables_ShouldBeCreatedAndFunctional()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Test core observables exist
        plc.IsConnected.Should().NotBeNull();
        plc.LastError.Should().NotBeNull();
        plc.LastErrorCode.Should().NotBeNull();
        plc.Status.Should().NotBeNull();
        plc.ObserveAll.Should().NotBeNull();
        plc.IsPaused.Should().NotBeNull();
        plc.ReadTime.Should().NotBeNull();

        // Test tag observables
        plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        var tagObservable = plc.Observe<ushort>("TestTag");
        tagObservable.Should().NotBeNull();
        tagObservable.Should().BeAssignableTo<IObservable<ushort>>();

        // Test GetCpuInfo observable
        var cpuInfoObservable = plc.GetCpuInfo();
        cpuInfoObservable.Should().NotBeNull();
        cpuInfoObservable.Should().BeAssignableTo<IObservable<string[]>>();
    }

    /// <summary>
    /// Test watchdog configuration and validation.
    /// </summary>
    [Fact]
    public void WatchdogConfiguration_ShouldWorkCorrectly()
    {
        // Test valid watchdog configuration
        using var plc1 = new RxS7(CpuType.S71500, "127.0.0.1", 0, 1, "DB10.DBW100", 100, 4500, 15);
        plc1.WatchDogAddress.Should().Be("DB10.DBW100");
        plc1.WatchDogValueToWrite.Should().Be(4500);
        plc1.WatchDogWritingTime.Should().Be(15);

        // Test invalid watchdog address (non-DBW)
        var invalidWatchdogAction = () => new RxS7(CpuType.S71500, "127.0.0.1", 0, 1, "DB10.DBB100", 100);
        invalidWatchdogAction.Should().Throw<ArgumentException>()
            .WithMessage("*WatchDogAddress must be a DBW address*");

        // Test without watchdog
        using var plc2 = new RxS7(CpuType.S71500, "127.0.0.1", 0, 1, null, 100);
        plc2.WatchDogAddress.Should().BeNull();
    }

    /// <summary>
    /// Test error handling for invalid parameters.
    /// </summary>
    [Fact]
    public void ErrorHandling_InvalidParameters_ShouldThrowCorrectExceptions()
    {
        // Invalid rack values
        var invalidRack1 = () => S71500.Create("127.0.0.1", -1, 1);
        invalidRack1.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("rack");

        var invalidRack2 = () => S71500.Create("127.0.0.1", 8, 1);
        invalidRack2.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("rack");

        // Invalid slot values
        var invalidSlot1 = () => S71500.Create("127.0.0.1", 0, 0);
        invalidSlot1.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("slot");

        var invalidSlot2 = () => S71500.Create("127.0.0.1", 0, 32);
        invalidSlot2.Should().Throw<ArgumentOutOfRangeException>().And.ParamName.Should().Be("slot");

        // Invalid tag operations
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        var nullTagName = () => plc.RemoveTagItem(null!);
        nullTagName.Should().Throw<ArgumentNullException>();

        var emptyTagName = () => plc.RemoveTagItem(string.Empty);
        emptyTagName.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Test performance characteristics and resource usage.
    /// </summary>
    [Fact]
    public void Performance_HighVolumeOperations_ShouldBeEfficient()
    {
        // Test rapid tag creation
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 10); // Fast interval

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        const int tagCount = 100;

        for (var i = 0; i < tagCount; i++)
        {
            plc.AddUpdateTagItem<ushort>($"PerfTag{i}", $"DB1.DBW{i * 2}");
        }

        stopwatch.Stop();

        var creationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        creationRate.Should().BeGreaterThan(1000, "Tag creation should be very fast");

        plc.TagList.Count.Should().Be(tagCount);

        // Test rapid observable creation
        stopwatch.Restart();
        var observables = new List<IObservable<ushort>>();

        for (var i = 0; i < tagCount; i++)
        {
            observables.Add(plc.Observe<ushort>($"PerfTag{i}"));
        }

        stopwatch.Stop();

        var observableCreationRate = tagCount / stopwatch.Elapsed.TotalSeconds;
        observableCreationRate.Should().BeGreaterThan(1000, "Observable creation should be very fast");

        Console.WriteLine($"Tag creation rate: {creationRate:F0} tags/second");
        Console.WriteLine($"Observable creation rate: {observableCreationRate:F0} observables/second");
    }

    /// <summary>
    /// Test memory usage patterns.
    /// </summary>
    [Fact]
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

            memoryPerInstance.Should().BeLessThan(
                500_000,
                $"Memory usage per PLC instance should be reasonable. Actual: {memoryPerInstance} bytes");

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
    [Fact]
    public void Disposal_ShouldCleanupResourcesProperly()
    {
        // Arrange
        var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);
        plc.AddUpdateTagItem<byte>("TestTag", "DB1.DBB0");

        plc.IsDisposed.Should().BeFalse();

        // Act
        plc.Dispose();

        // Assert
        plc.IsDisposed.Should().BeTrue();

        // Test multiple dispose calls
        var secondDispose = () => plc.Dispose();
        secondDispose.Should().NotThrow("Multiple dispose calls should be safe");

        plc.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Test tag polling control.
    /// </summary>
    [Fact]
    public void TagPolling_Control_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Test tag polling configuration
        var (tag, _) = plc.AddUpdateTagItem<ushort>("TestTag", "DB1.DBW0");
        var tagInstance = (Tag)tag!;

        // Default should be to poll
        tagInstance.DoNotPoll.Should().BeFalse();

        // Test disabling polling
        var (_, _) = plc.GetTag("TestTag").SetTagPollIng(false);
        tagInstance.DoNotPoll.Should().BeTrue();

        // Test enabling polling
        plc.GetTag("TestTag").SetTagPollIng(true);
        tagInstance.DoNotPoll.Should().BeFalse();
    }

    /// <summary>
    /// Test value operations (synchronous API).
    /// </summary>
    [Fact]
    public void ValueOperations_SynchronousAPI_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);
        plc.AddUpdateTagItem<ushort>("TestWord", "DB1.DBW0");
        plc.AddUpdateTagItem<float>("TestReal", "DB1.DBD4");

        // Test setting values (these will be queued for when connection is available)
        var setWordAction = () => plc.Value("TestWord", (ushort)12345);
        setWordAction.Should().NotThrow("Setting Word value should not throw");

        var setRealAction = () => plc.Value("TestReal", 3.14159f);
        setRealAction.Should().NotThrow("Setting Real value should not throw");

        // Test with different data types
        plc.AddUpdateTagItem<byte>("TestByte", "DB1.DBB8");
        var setByteAction = () => plc.Value("TestByte", (byte)255);
        setByteAction.Should().NotThrow("Setting Byte value should not throw");

        plc.AddUpdateTagItem<bool>("TestBool", "DB1.DBX10.0");
        var setBoolAction = () => plc.Value("TestBool", true);
        setBoolAction.Should().NotThrow("Setting Bool value should not throw");
    }

    /// <summary>
    /// Test reactive extensions integration.
    /// </summary>
    [Fact]
    public void ReactiveExtensions_Integration_ShouldWorkCorrectly()
    {
        // Arrange
        using var plc = S71500.Create("127.0.0.1", 0, 1, null, 100);

        // Test tag to dictionary conversion
        plc.AddUpdateTagItem<ushort>("Tag1", "DB1.DBW0");
        plc.AddUpdateTagItem<ushort>("Tag2", "DB1.DBW2");

        var observable = plc.Observe<ushort>("Tag1");
        var tagValueObservable = observable.ToTagValue("Tag1");

        tagValueObservable.Should().NotBeNull();
        tagValueObservable.Should().BeAssignableTo<IObservable<(string Tag, ushort Value)>>();

        // Test ObserveAll to dictionary conversion
        var dictionaryObservable = plc.ObserveAll.TagToDictionary<object>();
        dictionaryObservable.Should().NotBeNull();
        dictionaryObservable.Should().BeAssignableTo<IObservable<IDictionary<string, object>>>();
    }

    /// <summary>
    /// Test comprehensive scenario with mixed operations.
    /// </summary>
    [Fact]
    public void ComprehensiveScenario_MixedOperations_ShouldWorkTogether()
    {
        // Arrange - Create PLC with watchdog
        using var plc = new RxS7(CpuType.S71500, "192.168.1.100", 0, 1, "DB100.DBW0", 50, 4500, 10);

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
        plc.TagList.Count.Should().Be(12);

        // Test observables for different types
        var byteObs = plc.Observe<byte>("ProcessByte");
        var wordObs = plc.Observe<ushort>("ProcessWord");
        var realObs = plc.Observe<float>("ProcessReal");
        var boolObs = plc.Observe<bool>("ProcessBit");
        var arrayObs = plc.Observe<float[]>("ProcessArray");

        // All observables should be created
        byteObs.Should().NotBeNull();
        wordObs.Should().NotBeNull();
        realObs.Should().NotBeNull();
        boolObs.Should().NotBeNull();
        arrayObs.Should().NotBeNull();

        // Test value setting operations
        var valueOperations = new Action[]
        {
            () => plc.Value("ProcessByte", (byte)100),
            () => plc.Value("ProcessWord", (ushort)1000),
            () => plc.Value("ProcessReal", 123.456f),
            () => plc.Value("ProcessBit", true),
            () => plc.Value("ProcessArray", new float[] { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f }),
            () => plc.Value("InputWord", (ushort)2000),
            () => plc.Value("OutputWord", (ushort)3000),
            () => plc.Value("MemoryByte", (byte)200),
            () => plc.Value("MemoryBit", false)
        };

        foreach (var operation in valueOperations)
        {
            operation.Should().NotThrow("Value setting operations should not throw");
        }

        // Test tag management during runtime
        plc.RemoveTagItem("ProcessBit");
        plc.TagList.Count.Should().Be(11);

        var (newTag, _) = plc.AddUpdateTagItem<int>("ProcessDInt", "DB1.DBD20");
        newTag.Should().NotBeNull();
        plc.TagList.Count.Should().Be(12);

        // Verify watchdog configuration
        plc.WatchDogAddress.Should().Be("DB100.DBW0");
        plc.WatchDogValueToWrite.Should().Be(4500);
        plc.WatchDogWritingTime.Should().Be(10);

        // Test tag polling control
        plc.GetTag("ProcessWord").SetTagPollIng(false);
        ((Tag)plc.TagList["ProcessWord"]).DoNotPoll.Should().BeTrue();

        // Test system observables
        var statusObs = plc.Status;
        var errorObs = plc.LastError;
        var connectedObs = plc.IsConnected;

        statusObs.Should().NotBeNull();
        errorObs.Should().NotBeNull();
        connectedObs.Should().NotBeNull();

        Assert.True(true, "Comprehensive scenario completed successfully");
    }

    private static void ValidateTag(ITag? tag, string expectedName, string expectedAddress, Type expectedType)
    {
        tag.Should().NotBeNull();
        var typedTag = (Tag)tag!;
        typedTag.Name.Should().Be(expectedName);
        typedTag.Address.Should().Be(expectedAddress);
        typedTag.Type.Should().Be(expectedType);
    }

    private static void ValidateArrayTag(ITag? tag, string expectedName, string expectedAddress, Type expectedType, int expectedArrayLength)
    {
        ValidateTag(tag, expectedName, expectedAddress, expectedType);
        var typedTag = (Tag)tag!;
        typedTag.ArrayLength.Should().Be(expectedArrayLength);
    }
}
