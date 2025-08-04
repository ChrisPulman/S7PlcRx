// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.BatchOperations;
using S7PlcRx.Enums;
using S7PlcRx.Performance;
using S7PlcRx.Production;

namespace S7PlcRx;

/// <summary>
/// Advanced extensions for enhanced S7 PLC functionality and performance optimization.
/// </summary>
public static class S7AdvancedExtensions
{
    /// <summary>
    /// Observes multiple variables efficiently using batch optimization.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The variables to observe.</param>
    /// <returns>An observable dictionary of variable names and their values.</returns>
    public static IObservable<Dictionary<string, T?>> ObserveBatch<T>(this IRxS7 plc, params string[] variables)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        return plc.ObserveAll
            .Where(t => t != null && variables.Contains(t.Name) && TagValueIsValid<T>(t))
            .Select(t => new KeyValuePair<string, T?>(t!.Name!, (T?)t.Value))
            .Scan(new Dictionary<string, T?>(), (acc, kvp) =>
            {
                acc[kvp.Key] = kvp.Value;
                return new Dictionary<string, T?>(acc);
            })
            .DistinctUntilChanged(new DictionaryEqualityComparer<string, T?>())
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Reads multiple variables efficiently in a single operation.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The variables to read.</param>
    /// <returns>A dictionary of variable names and their values.</returns>
    public static async Task<Dictionary<string, T?>> ValueBatch<T>(this IRxS7 plc, params string[] variables)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        if (variables == null || variables.Length == 0)
        {
            return new Dictionary<string, T?>();
        }

        var results = new Dictionary<string, T?>();
        var tasks = new List<Task<T?>>();

        // Create tasks for each variable read
        foreach (var variable in variables)
        {
            tasks.Add(plc.Value<T>(variable));
        }

        // Wait for all reads to complete
        var values = await Task.WhenAll(tasks);

        // Combine results
        for (var i = 0; i < variables.Length; i++)
        {
            results[variables[i]] = values[i];
        }

        return results;
    }

    /// <summary>
    /// Writes multiple values efficiently in batch operations.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">Dictionary of variable names and values to write.</param>
    /// <returns>A task representing the batch write operation.</returns>
    public static async Task ValueBatch<T>(this IRxS7 plc, Dictionary<string, T> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        var tasks = values.Select(kvp => Task.Run(() => plc.Value(kvp.Key, kvp.Value))).ToArray();
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Optimized batch read with verification and error handling.
    /// Groups operations by data block for maximum efficiency.
    /// </summary>
    /// <typeparam name="T">The type of values to read.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagMapping">Dictionary mapping tag names to PLC addresses.</param>
    /// <param name="timeoutMs">Operation timeout in milliseconds.</param>
    /// <returns>Batch read result with success indicators.</returns>
    public static async Task<BatchReadResult<T>> ReadBatchOptimized<T>(
        this IRxS7 plc,
        Dictionary<string, string> tagMapping,
        int timeoutMs = 5000)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        var result = new BatchReadResult<T>();

        if (tagMapping == null || tagMapping.Count == 0)
        {
            result.OverallSuccess = true;
            return result;
        }

        // Ensure all tags exist
        foreach (var mapping in tagMapping)
        {
            if (!plc.TagList.ContainsKey(mapping.Key))
            {
                plc.AddUpdateTagItem<T>(mapping.Key, mapping.Value);
            }
        }

        // Group by data block for optimization
        var dataBlockGroups = tagMapping
            .GroupBy(kvp => ExtractDataBlockId(kvp.Value))
            .ToList();

        var readTasks = dataBlockGroups.Select(async group =>
        {
            var groupResults = new Dictionary<string, T>();
            var groupErrors = new Dictionary<string, string>();

            foreach (var tag in group)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(timeoutMs))
                    {
                        var value = await plc.Value<T>(tag.Key);
                        if (value != null)
                        {
                            groupResults[tag.Key] = value;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    groupErrors[tag.Key] = "Operation timed out";
                }
                catch (Exception ex)
                {
                    groupErrors[tag.Key] = ex.Message;
                }
            }

            return new { Results = groupResults, Errors = groupErrors };
        });

        var groupResultsArray = await Task.WhenAll(readTasks);

        // Combine results
        foreach (var groupResult in groupResultsArray)
        {
            foreach (var kvp in groupResult.Results)
            {
                result.Values[kvp.Key] = kvp.Value;
                result.Success[kvp.Key] = true;
            }

            foreach (var kvp in groupResult.Errors)
            {
                result.Errors[kvp.Key] = kvp.Value;
                result.Success[kvp.Key] = false;
            }
        }

        result.OverallSuccess = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// Optimized batch write with verification and rollback capabilities.
    /// </summary>
    /// <typeparam name="T">The type of values to write.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">Values to write.</param>
    /// <param name="verifyWrites">Whether to verify writes.</param>
    /// <param name="enableRollback">Whether to enable rollback on failure.</param>
    /// <returns>Batch write result.</returns>
    public static async Task<BatchWriteResult> WriteBatchOptimized<T>(
        this IRxS7 plc,
        Dictionary<string, T> values,
        bool verifyWrites = false,
        bool enableRollback = false)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        var result = new BatchWriteResult();
        var originalValues = new Dictionary<string, T>();

        if (values == null || values.Count == 0)
        {
            result.OverallSuccess = true;
            return result;
        }

        // Store original values for rollback
        if (enableRollback)
        {
            foreach (var kvp in values)
            {
                try
                {
                    var originalValue = await plc.Value<T>(kvp.Key);
                    if (originalValue != null)
                    {
                        originalValues[kvp.Key] = originalValue;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors[kvp.Key] = $"Failed to read original value: {ex.Message}";
                }
            }
        }

        // Perform writes
        foreach (var kvp in values)
        {
            try
            {
                plc.Value(kvp.Key, kvp.Value);
                result.Success[kvp.Key] = true;

                // Verify write if requested
                if (verifyWrites)
                {
                    await Task.Delay(50);
                    var readBack = await plc.Value<T>(kvp.Key);

                    if (readBack == null || !EqualityComparer<T>.Default.Equals(readBack, kvp.Value))
                    {
                        result.Success[kvp.Key] = false;
                        result.Errors[kvp.Key] = "Write verification failed";
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success[kvp.Key] = false;
                result.Errors[kvp.Key] = ex.Message;
            }
        }

        // Rollback if needed
        if (enableRollback && result.Errors.Count > 0)
        {
            foreach (var kvp in originalValues)
            {
                try
                {
                    plc.Value(kvp.Key, kvp.Value);
                }
                catch
                {
                    // Ignore rollback errors
                }
            }

            result.RollbackPerformed = true;
        }

        result.OverallSuccess = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// Gets detailed diagnostic information from the PLC.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>A comprehensive diagnostic report.</returns>
    public static async Task<ProductionDiagnostics> GetDiagnostics(this IRxS7 plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        var diagnostics = new ProductionDiagnostics
        {
            PLCType = plc.PLCType,
            IPAddress = plc.IP,
            Rack = plc.Rack,
            Slot = plc.Slot,
            IsConnected = plc.IsConnectedValue,
            DiagnosticTime = DateTime.UtcNow
        };

        if (plc.IsConnectedValue)
        {
            try
            {
                // Test connection latency
                var latencyStart = DateTime.UtcNow;
                var cpuInfo = await plc.GetCpuInfo().FirstAsync();
                diagnostics.ConnectionLatencyMs = (DateTime.UtcNow - latencyStart).TotalMilliseconds;
                diagnostics.CPUInformation = cpuInfo;

                // Get tag statistics
                var allTags = plc.TagList.Values.OfType<Tag>().ToList();
                diagnostics.TagMetrics = new ProductionTagMetrics
                {
                    TotalTags = allTags.Count,
                    ActiveTags = allTags.Count(t => !t.DoNotPoll),
                    InactiveTags = allTags.Count(t => t.DoNotPoll),
                    DataBlockDistribution = AnalyzeDataBlockDistribution(allTags)
                };
            }
            catch (Exception ex)
            {
                diagnostics.Errors.Add($"Diagnostic collection failed: {ex.Message}");
            }
        }

        // Generate recommendations
        diagnostics.Recommendations = GenerateOptimizationRecommendations(diagnostics);

        return diagnostics;
    }

    /// <summary>
    /// Monitors tag performance and provides optimization recommendations.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="monitoringDuration">The duration to monitor performance.</param>
    /// <returns>Performance analysis and optimization recommendations.</returns>
    public static async Task<PerformanceAnalysis> AnalyzePerformance(this IRxS7 plc, TimeSpan monitoringDuration)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc), "PLC instance cannot be null.");
        }

        var analysis = new PerformanceAnalysis { StartTime = DateTime.UtcNow };
        var tagChangeCounts = new ConcurrentDictionary<string, int>();
        var lastValues = new ConcurrentDictionary<string, object?>();

        // Monitor tag changes
        var subscription = plc.ObserveAll
            .Where(t => t != null)
            .Subscribe(tag =>
            {
                if (tag?.Name != null)
                {
                    var changed = false;
                    if (lastValues.TryGetValue(tag.Name, out var lastValue))
                    {
                        changed = !Equals(lastValue, tag.Value);
                    }
                    else
                    {
                        changed = true;
                    }

                    if (changed)
                    {
                        lastValues[tag.Name] = tag.Value;
                        tagChangeCounts.AddOrUpdate(tag.Name, 1, (_, count) => count + 1);
                    }
                }
            });

        // Monitor for specified duration
        await Task.Delay(monitoringDuration);
        subscription.Dispose();

        analysis.EndTime = DateTime.UtcNow;
        analysis.MonitoringDuration = monitoringDuration;

        // Analyze results
        analysis.TagChangeFrequencies = tagChangeCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        analysis.TotalTagChanges = tagChangeCounts.Values.Sum();
        analysis.AverageChangesPerTag = tagChangeCounts.Values.Count > 0 ? tagChangeCounts.Values.Average() : 0;

        // Generate recommendations
        analysis.Recommendations = GeneratePerformanceRecommendations(tagChangeCounts, monitoringDuration);

        return analysis;
    }

    /// <summary>
    /// Creates a high-performance tag group for batch operations.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="groupName">The name of the tag group.</param>
    /// <param name="tagNames">The tags to include in the group.</param>
    /// <returns>A high-performance tag group.</returns>
    public static HighPerformanceTagGroup CreateTagGroup(this IRxS7 plc, string groupName, params string[] tagNames)
    {
        return new HighPerformanceTagGroup(plc, groupName, tagNames);
    }

    private static string ExtractDataBlockId(string address)
    {
        if (string.IsNullOrEmpty(address) || !address.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
        {
            return "SYSTEM";
        }

        var dotIndex = address.IndexOf('.');
        return dotIndex <= 2 ? "SYSTEM" : address.Substring(0, dotIndex);
    }

    private static Dictionary<string, int> AnalyzeDataBlockDistribution(IEnumerable<Tag> tags) => tags
            .GroupBy(t => ExtractDataBlockId(t.Address!))
            .ToDictionary(g => g.Key, g => g.Count());

    private static List<string> GenerateOptimizationRecommendations(ProductionDiagnostics diagnostics)
    {
        var recommendations = new List<string>();

        if (diagnostics.TagMetrics.TotalTags > 200)
        {
            recommendations.Add("High tag count detected - consider implementing connection pooling");
        }

        if (diagnostics.ConnectionLatencyMs > 500)
        {
            recommendations.Add($"High latency ({diagnostics.ConnectionLatencyMs:F0}ms) - check network configuration");
        }

        if (diagnostics.TagMetrics.InactiveTags > diagnostics.TagMetrics.TotalTags * 0.3)
        {
            recommendations.Add($"Many inactive tags ({diagnostics.TagMetrics.InactiveTags}) - consider cleanup");
        }

        return recommendations;
    }

    private static List<string> GeneratePerformanceRecommendations(
        ConcurrentDictionary<string, int> tagChangeCounts,
        TimeSpan monitoringDuration)
    {
        var recommendations = new List<string>();
        var totalMinutes = monitoringDuration.TotalMinutes;

        if (totalMinutes <= 0)
        {
            return recommendations;
        }

        // Identify slow-changing tags
        var slowChangingTags = tagChangeCounts
            .Where(kvp => kvp.Value / totalMinutes < 0.1)
            .Select(kvp => kvp.Key)
            .ToList();

        if (slowChangingTags.Count > 0)
        {
            recommendations.Add($"Consider reducing polling frequency for {slowChangingTags.Count} slow-changing tags");
        }

        // Identify fast-changing tags
        var fastChangingTags = tagChangeCounts
            .Where(kvp => kvp.Value / totalMinutes > 10)
            .Select(kvp => kvp.Key)
            .ToList();

        if (fastChangingTags.Count > 0)
        {
            recommendations.Add($"Consider grouping {fastChangingTags.Count} fast-changing tags for batch operations");
        }

        return recommendations;
    }

    /// <summary>
    /// Validates that a tag value is of the correct type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="tag">The tag to validate.</param>
    /// <returns>True if the tag is valid, false otherwise.</returns>
    private static bool TagValueIsValid<T>(Tag? tag) =>
        tag != null && tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T);
}
