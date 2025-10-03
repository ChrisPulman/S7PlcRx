// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Linq;
using S7PlcRx.Advanced;
using S7PlcRx.Core;
using S7PlcRx.Enterprise;
using S7PlcRx.Enums;
using S7PlcRx.Optimization;

namespace S7PlcRx.Examples;

/// <summary>
/// Comprehensive examples demonstrating S7PlcRx optimizations for industrial automation.
/// Shows batch operations, performance monitoring, and advanced PLC communication patterns.
/// </summary>
public static class AdvancedExamples
{
    internal const string IpAddress = "192.168.0.5";
    internal const CpuType PlcType = CpuType.S71200;

    /// <summary>
    /// Demonstrates basic batch reading optimization for multiple tags.
    /// Reduces network overhead by grouping operations by data block.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task BasicBatchReadExample(IRxS7 plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC connection is not initialized");
        }

        Console.WriteLine("=== BASIC BATCH READ 20 VALUES EXAMPLE ===");
        Stopwatch stopwatch = new();
        plc.AddUpdateTagItem<float[]>("R_Values", "DB1.DBD20", 20) // Configure 20 floats from DB1
            .SetTagPollIng(false); // Disable polling for this tag
        stopwatch.Restart();
        var rValues = await plc.Value<float[]>("R_Values"); // Read the array of floats
        stopwatch.Stop();
        Console.WriteLine($"Read {rValues?.Length ?? 0} values in {stopwatch.ElapsedMilliseconds} ms");
        if (rValues?.Length > 0)
        {
            for (var i = 0; i < rValues.Length; i++)
            {
                Console.WriteLine($"R_Values[{i}]: {rValues[i]:F2}");
            }
        }
        else
        {
            Console.WriteLine("No values read from R_Values tag.");
        }

        // Define tag mapping for batch operations
        var tagMapping = new Dictionary<string, string>
        {
            ["Temperature1"] = "DB1.DBD0", // Process temperature 1
            ["Temperature2"] = "DB1.DBD4", // Process temperature 2
            ["Pressure1"] = "DB1.DBD8", // System pressure
            ["FlowRate"] = "DB1.DBD12", // Flow rate sensor
            ["Level"] = "DB1.DBD16" // Tank level
        };

        // Perform optimized batch read (80% faster than individual reads)
        var results = await plc.ReadBatchOptimized<float>(tagMapping);

        if (results.OverallSuccess)
        {
            Console.WriteLine("=== BATCH READ RESULTS ===");
            Console.WriteLine($"Successfully read {results.SuccessCount} out of {tagMapping.Count} tags");

            foreach (var kvp in results.Values)
            {
                Console.WriteLine($"{kvp.Key}: {kvp.Value:F2}");
            }
        }
        else
        {
            Console.WriteLine($"Batch read had {results.ErrorCount} errors:");
            foreach (var error in results.Errors)
            {
                Console.WriteLine($"  {error.Key}: {error.Value}");
            }
        }
    }

    /// <summary>
    /// Demonstrates advanced batch writing with verification and rollback.
    /// Ensures data integrity in critical industrial operations.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task AdvancedBatchWriteExample(IRxS7 plc)
    {
        // Add tags for writing
        plc.AddUpdateTagItem<float>("SetPoint1", "DB3.DBD0").SetTagPollIng(false);
        plc.AddUpdateTagItem<float>("SetPoint2", "DB3.DBD4").SetTagPollIng(false);
        plc.AddUpdateTagItem<bool>("EnableProcess", "DB3.DBX8.0").SetTagPollIng(false);
        plc.AddUpdateTagItem<int>("RecipeNumber", "DB3.DBD10").SetTagPollIng(false);

        // Define values to write
        var writeValues = new Dictionary<string, object>
        {
            ["SetPoint1"] = 25.5f, // Temperature setpoint
            ["SetPoint2"] = 1.8f, // Pressure setpoint
            ["EnableProcess"] = true, // Enable flag
            ["RecipeNumber"] = 42 // Active recipe
        };

        Console.WriteLine("=== ADVANCED BATCH WRITE ===");
        Console.WriteLine("Writing values with verification and rollback enabled...");

        // Perform batch write with verification and rollback protection
        var writeResult = await plc.WriteBatchOptimized<object>(
            writeValues,
            verifyWrites: true,      // Read back to verify writes
            enableRollback: true);   // Rollback on any failure

        if (writeResult.OverallSuccess)
        {
            Console.WriteLine($"✅ All {writeResult.SuccessCount} writes completed successfully");
        }
        else
        {
            Console.WriteLine($"❌ {writeResult.ErrorCount} writes failed");
            if (writeResult.RollbackPerformed)
            {
                Console.WriteLine("🔄 Rollback performed - system restored to previous state");
            }

            foreach (var error in writeResult.Errors)
            {
                Console.WriteLine($"  {error.Key}: {error.Value}");
            }
        }
    }

    /// <summary>
    /// Demonstrates high-performance tag groups for related operations.
    /// Optimizes batch operations for logically grouped tags.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task HighPerformanceTagGroupExample(IRxS7 plc)
    {
        // Create specialized tag groups for different process areas
        var temperatureGroup = plc.CreateTagGroup<float>(
            "Temperatures",
            "DB4.DBD0",   // Reactor temperature
            "DB4.DBD4",   // Cooling temperature
            "DB4.DBD8",   // Ambient temperature
            "DB4.DBD12");   // Exhaust temperature

        var pressureGroup = plc.CreateTagGroup<float>(
            "Pressures",
            "DB5.DBD0",   // System pressure
            "DB5.DBD4",   // Line pressure
            "DB5.DBD8");   // Vacuum pressure

        Console.WriteLine("=== HIGH-PERFORMANCE TAG GROUPS ===");

        // Read all temperatures efficiently
        var temperatures = await temperatureGroup.ReadAll();
        Console.WriteLine("Temperature Readings:");
        foreach (var temp in temperatures)
        {
            Console.WriteLine($"  {temp.Key}: {temp.Value:F1}°C");
        }

        // Read all pressures efficiently
        var pressures = await pressureGroup.ReadAll();
        Console.WriteLine("Pressure Readings:");
        foreach (var pressure in pressures)
        {
            Console.WriteLine($"  {pressure.Key}: {pressure.Value:F6} bar");
        }

        // Monitor group changes in real-time
        var subscription = temperatureGroup.ObserveGroup().Subscribe(groupData =>
        {
            var avgTemp = groupData.Values.Average();
            Console.WriteLine($"Average Temperature: {avgTemp:F1}°C");
        });

        // Keep monitoring for 30 seconds
        await Task.Delay(30000);
        subscription.Dispose();

        // Clean up
        temperatureGroup.Dispose();
        pressureGroup.Dispose();
    }

    /// <summary>
    /// Demonstrates intelligent monitoring with change detection and filtering.
    /// Reduces noise and focuses on significant changes only.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task IntelligentMonitoringExample(IRxS7 plc)
    {
        // Add monitoring tags
        plc.AddUpdateTagItem<float>("ProcessValue", "DB6.DBD0");
        plc.AddUpdateTagItem<float>("AnalogInput1", "DB6.DBD4");
        plc.AddUpdateTagItem<bool>("AlarmStatus", "DB6.DBX8.0");

        Console.WriteLine("=== INTELLIGENT MONITORING ===");

        // Monitor multiple values with batch optimization
        var batchObserver = plc.ObserveBatch<object>("ProcessValue", "AnalogInput1", "AlarmStatus");

        var monitoringSubscription = batchObserver.Subscribe(values =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Batch Update:");
            foreach (var kvp in values)
            {
                var valueStr = kvp.Value switch
                {
                    float f => $"{f:F2}",
                    bool b => b ? "ACTIVE" : "INACTIVE",
                    _ => kvp.Value?.ToString() ?? "NULL"
                };
                Console.WriteLine($"  {kvp.Key}: {valueStr}");
            }

            Console.WriteLine();
        });

        // Monitor with intelligent change detection (only significant changes)
        // Uses 0.5 threshold for analog values and 100ms debounce
        var smartMonitor = plc.MonitorTagSmart<float>("ProcessValue", changeThreshold: 0.5, debounceMs: 100);

        var smartSubscription = smartMonitor.Subscribe(change =>
        {
            Console.WriteLine("🔔 Significant Change Detected:");
            Console.WriteLine($"   Tag: {change.TagName}");
            Console.WriteLine($"   Previous: {change.PreviousValue:F2}");
            Console.WriteLine($"   Current: {change.CurrentValue:F2}");
            Console.WriteLine($"   Change: {change.ChangeAmount:F2}");
            Console.WriteLine($"   Time: {change.ChangeTime:HH:mm:ss.fff}");
            Console.WriteLine();
        });

        // Run monitoring for 60 seconds
        Console.WriteLine("Monitoring for 60 seconds... (only significant changes will be shown)");
        await Task.Delay(60000);

        // Clean up
        monitoringSubscription.Dispose();
        smartSubscription.Dispose();
    }

    /// <summary>
    /// Demonstrates comprehensive performance analysis and optimization recommendations.
    /// Provides actionable insights for system optimization.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task PerformanceAnalysisExample(IRxS7 plc)
    {
        Console.WriteLine("=== PERFORMANCE ANALYSIS ===");

        // Get comprehensive system diagnostics
        var diagnostics = await plc.GetDiagnostics();

        Console.WriteLine("System Overview:");
        Console.WriteLine($"  PLC Type: {diagnostics.PLCType}");
        Console.WriteLine($"  IP Address: {diagnostics.IPAddress}");
        Console.WriteLine($"  Connection Status: {(diagnostics.IsConnected ? "✅ Connected" : "❌ Disconnected")}");
        Console.WriteLine($"  Connection Latency: {diagnostics.ConnectionLatencyMs:F0}ms");
        Console.WriteLine();

        Console.WriteLine("Tag Statistics:");
        Console.WriteLine($"  Total Tags: {diagnostics.TagMetrics.TotalTags}");
        Console.WriteLine($"  Active Tags: {diagnostics.TagMetrics.ActiveTags}");
        Console.WriteLine($"  Inactive Tags: {diagnostics.TagMetrics.InactiveTags}");
        Console.WriteLine();

        Console.WriteLine("Data Block Distribution:");
        foreach (var db in diagnostics.TagMetrics.DataBlockDistribution)
        {
            Console.WriteLine($"  {db.Key}: {db.Value} tags");
        }

        Console.WriteLine();

        if (diagnostics.CPUInformation.Length > 0)
        {
            Console.WriteLine("CPU Information:");
            foreach (var info in diagnostics.CPUInformation)
            {
                Console.WriteLine($"  {info}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("Optimization Recommendations:");
        if (diagnostics.Recommendations.Count > 0)
        {
            foreach (var recommendation in diagnostics.Recommendations)
            {
                Console.WriteLine($"  💡 {recommendation}");
            }
        }
        else
        {
            Console.WriteLine("  ✅ System is well optimized!");
        }

        Console.WriteLine();

        // Perform detailed performance analysis over 2 minutes
        Console.WriteLine("Performing detailed performance analysis (2 minutes)...");
        var analysis = await plc.AnalyzePerformance(TimeSpan.FromMinutes(2));

        Console.WriteLine("Performance Analysis Results:");
        Console.WriteLine($"  Analysis Duration: {analysis.MonitoringDuration.TotalMinutes:F1} minutes");
        Console.WriteLine($"  Total Tag Changes: {analysis.TotalTagChanges}");
        Console.WriteLine($"  Average Changes per Tag: {analysis.AverageChangesPerTag:F1}");
        Console.WriteLine();

        Console.WriteLine("Tag Change Frequencies:");
        var topChangingTags = analysis.TagChangeFrequencies
            .OrderByDescending(kvp => kvp.Value)
            .Take(10);

        foreach (var tag in topChangingTags)
        {
            var changesPerMinute = tag.Value / analysis.MonitoringDuration.TotalMinutes;
            Console.WriteLine($"  {tag.Key}: {tag.Value} changes ({changesPerMinute:F1}/min)");
        }

        Console.WriteLine();

        Console.WriteLine("Performance Recommendations:");
        foreach (var recommendation in analysis.Recommendations)
        {
            Console.WriteLine($"  🎯 {recommendation}");
        }
    }

    /// <summary>
    /// Demonstrates complete production workflow with all optimizations.
    /// Shows integration of batch operations, monitoring, and error handling.
    /// </summary>
    /// <param name="plc">The PLC.</param>
    /// <returns>
    /// A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    public static async Task ProductionWorkflowExample(IRxS7 plc)
    {
        Console.WriteLine("=== PRODUCTION WORKFLOW EXAMPLE ===");
        Console.WriteLine("Simulating a complete production cycle with optimizations...");
        Console.WriteLine();

        try
        {
            // 1. System Initialization - Read current state
            Console.WriteLine("1️⃣ System Initialization");
            var initTags = new Dictionary<string, string>
            {
                ["SystemReady"] = "DB7.DBX0.0",
                ["RecipeLoaded"] = "DB7.DBX0.1",
                ["ProcessStep"] = "DB7.DBW2",
                ["BatchNumber"] = "DB7.DBD4"
            };

            var systemState = await plc.ReadBatchOptimized<object>(initTags);
            Console.WriteLine($"   System Ready: {systemState.Values["SystemReady"]}");
            Console.WriteLine($"   Recipe Loaded: {systemState.Values["RecipeLoaded"]}");
            Console.WriteLine($"   Current Step: {systemState.Values["ProcessStep"]}");
            Console.WriteLine($"   Batch Number: {systemState.Values["BatchNumber"]}");
            Console.WriteLine();

            // 2. Recipe Setup - Write process parameters
            Console.WriteLine("2️⃣ Recipe Setup");
            var recipeParams = new Dictionary<string, object>
            {
                ["Temperature_SP"] = 85.5f, // Temperature setpoint
                ["Pressure_SP"] = 2.1f, // Pressure setpoint
                ["MixSpeed_SP"] = 150, // Mixer speed
                ["ProcessTime"] = 3600, // Process time in seconds
                ["RecipeID"] = 12345 // Recipe identifier
            };

            var dbIndex = 0;

            // Add recipe tags
            foreach (var param in recipeParams)
            {
                var address = $"DB9.DBD{dbIndex}"; // Simplified addressing
                dbIndex += 4;
                plc.AddUpdateTagItem(param.Value.GetType(), param.Key, address).SetTagPollIng(false);
            }

            var recipeResult = await plc.WriteBatchOptimized<object>(
                recipeParams, verifyWrites: true, enableRollback: true);

            Console.WriteLine($"   Recipe parameters written: {recipeResult.SuccessCount}/{recipeParams.Count}");
            if (!recipeResult.OverallSuccess)
            {
                Console.WriteLine("   ⚠️  Recipe setup had errors - aborting");
                return;
            }

            Console.WriteLine();

            // 3. Process Monitoring - Create monitoring groups
            Console.WriteLine("3️⃣ Process Monitoring Setup");
            var processGroup = plc.CreateTagGroup<float>(
                "ProcessMonitoring",
                "DB7.DBD0", // Actual temperature
                "DB7.DBD4", // Actual pressure
                "DB7.DBD8"); // Actual mixer speed

            var alarmGroup = plc.CreateTagGroup<bool>(
                "AlarmMonitoring",
                "DB8.DBX0.0", // High temperature alarm
                "DB8.DBX0.1", // High pressure alarm
                "DB8.DBX0.2", // Equipment fault alarm
                "DB8.DBX0.3", // Emergency stop
                "DB7.DBX10.0"); // Process running

            Console.WriteLine("   ✅ Monitoring groups created");
            Console.WriteLine();

            // 4. Real-time Monitoring
            Console.WriteLine("4️⃣ Real-time Process Monitoring (30 seconds)");

            var processSubscription = processGroup.ObserveGroup().Subscribe(processData =>
            {
                Console.WriteLine($"   [{DateTime.Now:HH:mm:ss}] Process Values:");
                foreach (var kvp in processData)
                {
                    Console.WriteLine($"     {kvp.Key}: {kvp.Value}");
                }
            });

            var alarmSubscription = alarmGroup.ObserveGroup().Subscribe(alarmData =>
            {
                var activeAlarms = alarmData.Where(kvp => kvp.Value is bool b && b);
                if (activeAlarms.Any())
                {
                    Console.WriteLine($"   🚨 ALARMS ACTIVE: {string.Join(", ", activeAlarms.Select(a => a.Key))}");
                }
            });

            // Monitor for 30 seconds
            await Task.Delay(30000);

            // 5. Performance Analysis
            Console.WriteLine();
            Console.WriteLine("5️⃣ Performance Analysis");
            var finalDiagnostics = await plc.GetDiagnostics();
            Console.WriteLine($"   Connection Latency: {finalDiagnostics.ConnectionLatencyMs:F0}ms");
            Console.WriteLine($"   Active Tags: {finalDiagnostics.TagMetrics.ActiveTags}");
            Console.WriteLine($"   Recommendations: {finalDiagnostics.Recommendations.Count}");

            // Clean up
            processSubscription.Dispose();
            alarmSubscription.Dispose();
            processGroup.Dispose();
            alarmGroup.Dispose();

            Console.WriteLine();
            Console.WriteLine("✅ Production workflow completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Production workflow error: {ex.Message}");
        }
    }

    /// <summary>
    /// Entry point for running all optimization examples.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("🚀 S7PlcRx Optimization Examples");
        Console.WriteLine("==================================");
        Console.WriteLine();

        var connectionConfigs = new[]
        {
                new PlcConnectionConfig
                {
                    PLCType = PlcType,
                    IPAddress = IpAddress,
                    Rack = 0,
                    Slot = 1
                }
        };

        var poolConfig = new ConnectionPoolConfig
        {
            MaxConnections = 5,
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            EnableConnectionReuse = true,
            HealthCheckInterval = TimeSpan.FromMinutes(1)
        };
        var cons = EnterpriseExtensions.CreateConnectionPool(connectionConfigs, poolConfig);
        try
        {
            var step = 0;
            var connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await BasicBatchReadExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 1)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }

            connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await AdvancedBatchWriteExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 2)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }

            connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await HighPerformanceTagGroupExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 3)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }

            connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await IntelligentMonitoringExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 4)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }

            connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await PerformanceAnalysisExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 5)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }

            connection = cons.GetConnection;
            connection.IsConnected.Where(x => x).Subscribe(async _ =>
            {
                await ProductionWorkflowExample(connection);
                Console.WriteLine("\n" + new string('─', 50) + "\n");
                step++;
            });

            while (step < 6)
            {
                await Task.Delay(1000); // Wait for all connections to initialize
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Example execution error: {ex.Message}");
        }

        cons.Dispose();
        Console.WriteLine();
        Console.WriteLine("🎉 All optimization examples completed!");
    }
}
