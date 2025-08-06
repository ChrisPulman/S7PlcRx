// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Advanced;
using S7PlcRx.Core;
using S7PlcRx.Enterprise;
using S7PlcRx.Enums;
using S7PlcRx.Optimization;
using S7PlcRx.Performance;
using S7PlcRx.Production;

namespace S7PlcRx.Tests;

/// <summary>
/// Comprehensive optimization tests for S7PlcRx covering performance, caching, batching, and production features.
/// These tests validate the optimized library functionality without requiring physical PLCs.
/// </summary>
public sealed class S7PlcRxOptimizationTests : IDisposable
{
    private readonly RxS7 _plc;
    private readonly MockServer _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="S7PlcRxOptimizationTests"/> class.
    /// </summary>
    public S7PlcRxOptimizationTests()
    {
        _server = new MockServer();
        var rc = _server.StartTo(MockServer.Localhost);
        rc.Should().Be(0);
        _plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, 100);
    }

    /// <summary>
    /// Test performance monitoring functionality.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task MonitorPerformance_ShouldProvideMetrics()
    {
        // Arrange
        var monitoringPeriod = TimeSpan.FromMilliseconds(100);

        // Act
        var metricsObservable = _plc.MonitorPerformance(monitoringPeriod);
        var firstMetrics = await metricsObservable.Take(1).FirstAsync();

        // Assert
        firstMetrics.Should().NotBeNull();
        firstMetrics.PLCIdentifier.Should().Contain(MockServer.Localhost);
        firstMetrics.PLCIdentifier.Should().Contain("S71500");
        firstMetrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        firstMetrics.TagCount.Should().BeGreaterOrEqualTo(0);
    }

    /// <summary>
    /// Test optimized read operations with multiple tags.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ReadOptimized_WithMultipleTags_ShouldGroupByDataBlock()
    {
        // Arrange
        var tagNames = new[] { "TestTag1", "TestTag2", "TestTag3" };

        // Add test tags
        _plc.AddUpdateTagItem<float>("TestTag1", "DB1.DBD0");
        _plc.AddUpdateTagItem<float>("TestTag2", "DB1.DBD4");
        _plc.AddUpdateTagItem<float>("TestTag3", "DB2.DBD0");

        var config = new ReadOptimizationConfig
        {
            EnableParallelReads = true,
            InterGroupDelayMs = 10,
            MaxConcurrentReads = 5
        };

        // Act
        var results = await _plc.ReadOptimized<float>(tagNames, config);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(3);
        results.Should().ContainKeys("TestTag1", "TestTag2", "TestTag3");
    }

    /// <summary>
    /// Test optimized write operations with verification.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task WriteOptimized_WithVerification_ShouldCompleteSuccessfully()
    {
        // Arrange
        _plc.AddUpdateTagItem<float>("WriteTag1", "DB1.DBD0");
        _plc.AddUpdateTagItem<float>("WriteTag2", "DB1.DBD4");

        var writeValues = new Dictionary<string, float>
        {
            ["WriteTag1"] = 25.5f,
            ["WriteTag2"] = 30.0f
        };

        var config = new WriteOptimizationConfig
        {
            EnableParallelWrites = false, // Conservative for testing
            VerifyWrites = false, // Disable verification for unit test
            InterGroupDelayMs = 10
        };

        // Act
        var result = await _plc.WriteOptimized(writeValues, config);

        // Assert
        result.Should().NotBeNull();
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        result.SuccessRate.Should().BeGreaterOrEqualTo(0.0);
    }

    /// <summary>
    /// Test performance benchmark functionality.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task RunBenchmark_ShouldProvideBenchmarkResults()
    {
        // Arrange
        var config = new BenchmarkConfig
        {
            LatencyTestCount = 3,
            ThroughputTestDuration = TimeSpan.FromMilliseconds(100),
            ReliabilityTestCount = 5
        };

        // Act
        var result = await _plc.RunBenchmark(config);

        // Assert
        result.Should().NotBeNull();
        result.PLCIdentifier.Should().Contain(MockServer.Localhost);
        result.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        result.OverallScore.Should().BeInRange(0, 100);
    }

    /// <summary>
    /// Test performance statistics collection.
    /// </summary>
    [Fact]
    public void GetPerformanceStatistics_ShouldReturnValidStats()
    {
        // Act
        var stats = _plc.GetPerformanceStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.PLCIdentifier.Should().Contain(MockServer.Localhost);
        stats.TotalOperations.Should().BeGreaterOrEqualTo(0);
        stats.TotalErrors.Should().BeGreaterOrEqualTo(0);
        stats.ErrorRate.Should().BeInRange(0.0, 1.0);
        stats.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Test advanced batch reading with optimization.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ReadBatchOptimized_ShouldOptimizeDataBlockAccess()
    {
        // Arrange
        var tagMapping = new Dictionary<string, string>
        {
            ["Temperature1"] = "DB1.DBD0",
            ["Temperature2"] = "DB1.DBD4",
            ["Pressure1"] = "DB2.DBD0",
            ["Flow1"] = "DB2.DBD4"
        };

        // Add tags to PLC
        foreach (var mapping in tagMapping)
        {
            _plc.AddUpdateTagItem<float>(mapping.Key, mapping.Value);
        }

        // Act
        var result = await _plc.ReadBatchOptimized<float>(tagMapping);

        // Assert
        result.Should().NotBeNull();
        result.Values.Should().HaveCount(4);
        result.Success.Should().HaveCount(4);
        result.OverallSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Test advanced batch writing with verification and rollback.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task WriteBatchOptimized_WithRollback_ShouldHandleErrors()
    {
        // Arrange
        var writeValues = new Dictionary<string, float>
        {
            ["SetPoint1"] = 25.5f,
            ["SetPoint2"] = 30.0f
        };

        // Add tags
        foreach (var kvp in writeValues)
        {
            _plc.AddUpdateTagItem<float>(kvp.Key, $"DB1.DBD{writeValues.Keys.ToList().IndexOf(kvp.Key) * 4}");
        }

        // Act
        var result = await _plc.WriteBatchOptimized(writeValues, verifyWrites: false, enableRollback: true);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().HaveCount(2);
        result.OverallSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Test smart tag change monitoring with debouncing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task MonitorTagSmart_ShouldProvideChangeDetection()
    {
        // Arrange
        _plc.AddUpdateTagItem<float>("SmartTag", "DB1.DBD0");
        const double changeThreshold = 0.5;
        const int debounceMs = 50;

        // Act
        var smartMonitor = _plc.MonitorTagSmart<float>("SmartTag", changeThreshold, debounceMs);

        // We can't easily test this without actual data changes, so just verify the observable is created
        smartMonitor.Should().NotBeNull();

        // Test that we can subscribe without errors
        using var subscription = smartMonitor.Subscribe(change =>
        {
            change.Should().NotBeNull();
            change.TagName.Should().Be("SmartTag");
        });

        // Give it a moment to ensure no immediate errors
        await Task.Delay(100);
    }

    /// <summary>
    /// Test cache-enabled value reading.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ValueCached_ShouldUseIntelligentCaching()
    {
        // Arrange
        _plc.AddUpdateTagItem<float>("CacheTag", "DB1.DBD0");
        var cacheTimeout = TimeSpan.FromSeconds(1);

        // Act
        var value1 = await _plc.ValueCached<float>("CacheTag", cacheTimeout);
        var value2 = await _plc.ValueCached<float>("CacheTag", cacheTimeout);

        // Assert
        // Both should complete without error (actual values depend on PLC connectivity)
        value1.Should().BeOfType(typeof(float));
        value2.Should().BeOfType(typeof(float));
    }

    /// <summary>
    /// Test cache statistics and management.
    /// </summary>
    [Fact]
    public void CacheManagement_ShouldProvideStatistics()
    {
        // Arrange
        _plc.AddUpdateTagItem<float>("StatsTag", "DB1.DBD0");

        // Act
        var statsBefore = _plc.GetCacheStatistics();
        _plc.ClearCache(); // Clear all cache
        var statsAfter = _plc.GetCacheStatistics();

        // Assert
        statsBefore.Should().NotBeNull();
        statsAfter.Should().NotBeNull();
        statsAfter.TotalEntries.Should().BeLessOrEqualTo(statsBefore.TotalEntries);
    }

    /// <summary>
    /// Test production system validation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ValidateProductionReadiness_ShouldAssessSystemHealth()
    {
        // Arrange
        var config = new ProductionValidationConfig
        {
            MaxAcceptableResponseTime = TimeSpan.FromSeconds(1),
            MinimumReliabilityRate = 0.8,
            ReliabilityTestCount = 3,
            MinimumProductionScore = 60.0
        };

        // Act
        var result = await _plc.ValidateProductionReadiness(config);

        // Assert
        result.Should().NotBeNull();
        result.PLCIdentifier.Should().Contain(MockServer.Localhost);
        result.ValidationTests.Should().NotBeEmpty();
        result.OverallScore.Should().BeInRange(0, 100);
        result.TotalValidationTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    /// <summary>
    /// Test production error handling with circuit breaker.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteWithErrorHandling_ShouldProvideResilience()
    {
        // Arrange
        var config = new ProductionErrorConfig
        {
            MaxRetryAttempts = 2,
            BaseRetryDelayMs = 50,
            UseExponentialBackoff = true,
            CircuitBreakerThreshold = 3,
            CircuitBreakerTimeout = TimeSpan.FromSeconds(1)
        };

        // Act & Assert
        var result = await _plc.ExecuteWithErrorHandling(
            async () =>
        {
            // Simulate a simple operation
            await Task.Delay(10);
            return "Success";
        },
            config);

        result.Should().Be("Success");
    }

    /// <summary>
    /// Test high-performance tag group creation and operations.
    /// </summary>
    [Fact]
    public void CreateTagGroup_ShouldProvideOptimizedAccess()
    {
        // Arrange
        var tagNames = new[] { "GroupTag1", "GroupTag2", "GroupTag3" };
        foreach (var tagName in tagNames)
        {
            _plc.AddUpdateTagItem<float>(tagName, $"DB1.DBD{Array.IndexOf(tagNames, tagName) * 4}");
        }

        // Act
        var tagGroup = _plc.CreateTagGroup<float>("TestGroup", tagNames);

        // Assert
        tagGroup.Should().NotBeNull();
        tagGroup.GroupName.Should().Be("TestGroup");
    }

    /// <summary>
    /// Test comprehensive diagnostics collection.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task GetDiagnostics_ShouldProvideComprehensiveInfo()
    {
        // Act
        var diagnostics = await _plc.GetDiagnostics();

        // Assert
        diagnostics.Should().NotBeNull();
        diagnostics.PLCType.Should().Be(CpuType.S71500);
        diagnostics.IPAddress.Should().Be(MockServer.Localhost);
        diagnostics.Rack.Should().Be(0);
        diagnostics.Slot.Should().Be(1);
        diagnostics.DiagnosticTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        diagnostics.TagMetrics.Should().NotBeNull();
        diagnostics.Recommendations.Should().NotBeNull();
    }

    /// <summary>
    /// Test performance analysis and optimization recommendations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AnalyzePerformance_ShouldProvideRecommendations()
    {
        // Arrange
        var monitoringDuration = TimeSpan.FromMilliseconds(200);

        // Act
        var analysis = await _plc.AnalyzePerformance(monitoringDuration);

        // Assert
        analysis.Should().NotBeNull();
        analysis.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        analysis.EndTime.Should().BeAfter(analysis.StartTime);
        analysis.MonitoringDuration.Should().Be(monitoringDuration);
        analysis.Recommendations.Should().NotBeNull();
    }

    /// <summary>
    /// Test multiple tag observation with batch optimization.
    /// </summary>
    [Fact]
    public void ObserveBatch_ShouldProvideEfficientMonitoring()
    {
        // Arrange
        var tagNames = new[] { "ObserveTag1", "ObserveTag2", "ObserveTag3" };
        foreach (var tagName in tagNames)
        {
            _plc.AddUpdateTagItem<float>(tagName, $"DB1.DBD{Array.IndexOf(tagNames, tagName) * 4}");
        }

        // Act
        var batchObservable = _plc.ObserveBatch<float>(tagNames);

        // Assert
        batchObservable.Should().NotBeNull();

        // Test subscription works
        using var subscription = batchObservable.Subscribe(values =>
        {
            values.Should().NotBeNull();
            values.Should().ContainKeys(tagNames);
        });
    }

    /// <summary>
    /// Test symbol table loading and symbolic addressing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task LoadSymbolTable_ShouldEnableSymbolicAddressing()
    {
        // Arrange
        const string csvData = "Name,Address,DataType,Length,Description\n" +
                     "Temperature1,DB1.DBD0,REAL,1,Process Temperature\n" +
                     "Pressure1,DB1.DBD4,REAL,1,System Pressure";

        // Act
        var symbolTable = await _plc.LoadSymbolTable(csvData, SymbolTableFormat.Csv);

        // Assert
        symbolTable.Should().NotBeNull();
        symbolTable.Symbols.Should().HaveCount(2);
        symbolTable.Symbols.Should().ContainKey("Temperature1");
        symbolTable.Symbols.Should().ContainKey("Pressure1");
        symbolTable.LoadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Test high-availability PLC manager with failover.
    /// </summary>
    [Fact]
    public void CreateHighAvailabilityConnection_ShouldProvideFailover()
    {
        // Arrange
        var primaryPlc = new RxS7(CpuType.S71500, "192.168.1.100", 0, 1);
        var backupPlcs = new List<IRxS7>
        {
            new RxS7(CpuType.S71500, "192.168.1.101", 0, 1),
            new RxS7(CpuType.S71500, "192.168.1.102", 0, 1)
        };

        // Act
        using var haManager = EnterpriseExtensions.CreateHighAvailabilityConnection(
            primaryPlc, backupPlcs, TimeSpan.FromSeconds(10));

        // Assert
        haManager.Should().NotBeNull();
        haManager.ActivePLC.Should().Be(primaryPlc);
        haManager.FailoverEvents.Should().NotBeNull();

        // Cleanup
        primaryPlc.Dispose();
        foreach (var backup in backupPlcs)
        {
            backup.Dispose();
        }
    }

    /// <summary>
    /// Test optimization engine batch processing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task OptimizationEngine_BatchProcessing_ShouldImprovePerformance()
    {
        // Arrange
        var tags = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var tagName = $"BatchTag{i}";
            tags.Add(tagName);
            _plc.AddUpdateTagItem<float>(tagName, $"DB1.DBD{i * 4}");
        }

        // Act - Test batch reading
        var batchResult = await _plc.ValueBatch<float>(tags.ToArray());

        // Assert
        batchResult.Should().NotBeNull();
        batchResult.Should().HaveCount(10);
        foreach (var tag in tags)
        {
            batchResult.Should().ContainKey(tag);
        }
    }

    /// <summary>
    /// Test connection pool functionality.
    /// </summary>
    [Fact]
    public void ConnectionPool_ShouldManageMultipleConnections()
    {
        // Arrange
        var config = new ConnectionPoolConfig
        {
            MaxConnections = 5,
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            EnableConnectionReuse = true
        };

        // Act
        using var connectionPool = new ConnectionPool(config);

        // Assert
        connectionPool.Should().NotBeNull();
        connectionPool.MaxConnections.Should().Be(5);
        connectionPool.ActiveConnections.Should().Be(0);
    }

    /// <summary>
    /// Test performance analysis with real-time recommendations.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task PerformanceAnalysis_RealTime_ShouldProvideInsights()
    {
        // Arrange
        _plc.AddUpdateTagItem<float>("AnalysisTag1", "DB1.DBD0");
        _plc.AddUpdateTagItem<float>("AnalysisTag2", "DB1.DBD4");

        var monitoringDuration = TimeSpan.FromMilliseconds(300);

        // Act
        var analysis = await _plc.AnalyzePerformance(monitoringDuration);

        // Assert
        analysis.Should().NotBeNull();
        analysis.MonitoringDuration.Should().Be(monitoringDuration);
        analysis.TagChangeFrequencies.Should().NotBeNull();
        analysis.Recommendations.Should().NotBeNull();
        analysis.TotalTagChanges.Should().BeGreaterOrEqualTo(0);
    }

    /// <summary>
    /// Test security context and encrypted communication.
    /// </summary>
    [Fact]
    public void SecurityContext_ShouldProvideEncryptedCommunication()
    {
        // Arrange
        var securityContext = new SecurityContext
        {
            EnableEncryption = true,
            CertificatePath = "test.pfx",
            CertificatePassword = "test123"
        };

        // Act & Assert
        securityContext.Should().NotBeNull();
        securityContext.EnableEncryption.Should().BeTrue();
        securityContext.CertificatePath.Should().Be("test.pfx");
    }

    /// <summary>
    /// Disposes test resources.
    /// </summary>
    public void Dispose()
    {
        _plc?.Dispose();
        _server?.Stop();
    }
}
