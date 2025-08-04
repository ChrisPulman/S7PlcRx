// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using S7PlcRx.Optimization;

namespace S7PlcRx.Performance;

/// <summary>
/// Performance optimization extensions for S7 PLC communications providing
/// advanced metrics, connection pooling, and throughput optimization.
/// </summary>
public static class S7PerformanceExtensions
{
    private static readonly ConcurrentDictionary<string, PerformanceCounter> _performanceCounters = new();
    private static readonly ConcurrentDictionary<string, ConnectionMetrics> _connectionMetrics = new();

    /// <summary>
    /// Enables comprehensive performance monitoring for PLC operations.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="monitoringInterval">Performance monitoring interval.</param>
    /// <returns>Observable stream of performance metrics.</returns>
    public static IObservable<PerformanceMetrics> MonitorPerformance(
        this IRxS7 plc,
        TimeSpan? monitoringInterval = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var interval = monitoringInterval ?? TimeSpan.FromSeconds(30);
        var metricsKey = $"{plc.IP}_{plc.PLCType}";

        return Observable.Timer(TimeSpan.Zero, interval)
            .Select(_ =>
            {
                var metrics = new PerformanceMetrics
                {
                    PLCIdentifier = metricsKey,
                    Timestamp = DateTime.UtcNow,
                    IsConnected = plc.IsConnectedValue,
                    TagCount = plc.TagList.Count,
                    ActiveTagCount = plc.TagList.Values.OfType<Tag>().Count(t => !t.DoNotPoll)
                };

                // Get or create performance counter
                var counter = _performanceCounters.GetOrAdd(metricsKey, _ => new PerformanceCounter());
                metrics.OperationsPerSecond = counter.GetOperationsPerSecond();
                metrics.AverageResponseTime = counter.GetAverageResponseTime();
                metrics.ErrorRate = counter.GetErrorRate();

                // Get connection metrics
                var connectionMetrics = _connectionMetrics.GetOrAdd(metricsKey, _ => new ConnectionMetrics());
                metrics.ConnectionUptime = connectionMetrics.GetUptime();
                metrics.ReconnectionCount = connectionMetrics.ReconnectionCount;

                return metrics;
            })
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Optimizes read operations by automatically grouping sequential reads.
    /// </summary>
    /// <typeparam name="T">The type of values to read.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagNames">The tag names to read.</param>
    /// <param name="optimizationConfig">Optimization configuration.</param>
    /// <returns>Dictionary of tag names and their optimized read results.</returns>
    public static async Task<Dictionary<string, T?>> ReadOptimized<T>(
        this IRxS7 plc,
        IEnumerable<string> tagNames,
        ReadOptimizationConfig? optimizationConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (tagNames == null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        var config = optimizationConfig ?? new ReadOptimizationConfig();
        var tagList = tagNames.ToList();
        var results = new Dictionary<string, T?>();

        if (tagList.Count == 0)
        {
            return results;
        }

        var counter = GetPerformanceCounter(plc);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Group tags by data block for optimization
            var groupedTags = GroupTagsByDataBlock(tagList, plc);

            foreach (var group in groupedTags)
            {
                var groupTasks = group.Value.Select(async tagName =>
                {
                    try
                    {
                        var value = await plc.Value<T>(tagName);
                        return new KeyValuePair<string, T?>(tagName, value);
                    }
                    catch (Exception ex)
                    {
                        counter.RecordError();
                        throw new InvalidOperationException($"Failed to read tag '{tagName}': {ex.Message}", ex);
                    }
                });

                if (config.EnableParallelReads)
                {
                    var groupResults = await Task.WhenAll(groupTasks);
                    foreach (var result in groupResults)
                    {
                        results[result.Key] = result.Value;
                    }
                }
                else
                {
                    foreach (var task in groupTasks)
                    {
                        var result = await task;
                        results[result.Key] = result.Value;
                    }
                }

                // Add delay between groups if configured
                if (config.InterGroupDelayMs > 0 && group.Key != groupedTags.Keys.Last())
                {
                    await Task.Delay(config.InterGroupDelayMs);
                }
            }

            counter.RecordOperation(stopwatch.Elapsed);
            return results;
        }
        catch (Exception)
        {
            counter.RecordError();
            throw;
        }
    }

    /// <summary>
    /// Optimizes write operations by automatically batching writes.
    /// </summary>
    /// <typeparam name="T">The type of values to write.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">Dictionary of tag names and values to write.</param>
    /// <param name="optimizationConfig">Optimization configuration.</param>
    /// <returns>Write operation results.</returns>
    public static async Task<WriteOptimizationResult> WriteOptimized<T>(
        this IRxS7 plc,
        Dictionary<string, T> values,
        WriteOptimizationConfig? optimizationConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var config = optimizationConfig ?? new WriteOptimizationConfig();
        var result = new WriteOptimizationResult { StartTime = DateTime.UtcNow };
        var counter = GetPerformanceCounter(plc);

        try
        {
            // Group writes by data block for optimization
            var groupedWrites = GroupWritesByDataBlock(values, plc);

            foreach (var group in groupedWrites)
            {
                var writeTasks = group.Value.Select(async kvp =>
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        plc.Value(kvp.Key, kvp.Value);

                        if (config.VerifyWrites)
                        {
                            await Task.Delay(50); // Small delay before verification
                            var readBack = await plc.Value<T>(kvp.Key);
                            if (readBack == null || !EqualityComparer<T>.Default.Equals(readBack, kvp.Value))
                            {
                                throw new InvalidOperationException($"Write verification failed for tag '{kvp.Key}'");
                            }
                        }

                        result.SuccessfulWrites[kvp.Key] = stopwatch.Elapsed;
                        counter.RecordOperation(stopwatch.Elapsed);
                    }
                    catch (Exception ex)
                    {
                        result.FailedWrites[kvp.Key] = ex.Message;
                        counter.RecordError();
                    }
                });

                if (config.EnableParallelWrites)
                {
                    await Task.WhenAll(writeTasks);
                }
                else
                {
                    foreach (var task in writeTasks)
                    {
                        await task;
                    }
                }

                // Add delay between groups if configured
                if (config.InterGroupDelayMs > 0 && group.Key != groupedWrites.Keys.Last())
                {
                    await Task.Delay(config.InterGroupDelayMs);
                }
            }
        }
        catch (Exception ex)
        {
            result.OverallError = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Performs a comprehensive performance benchmark of the PLC connection.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="benchmarkConfig">Benchmark configuration.</param>
    /// <returns>Comprehensive benchmark results.</returns>
    public static async Task<BenchmarkResult> RunBenchmark(
        this IRxS7 plc,
        BenchmarkConfig? benchmarkConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var config = benchmarkConfig ?? new BenchmarkConfig();
        var result = new BenchmarkResult
        {
            StartTime = DateTime.UtcNow,
            PLCIdentifier = $"{plc.IP}:{plc.PLCType}"
        };

        try
        {
            // Test connection latency
            await BenchmarkLatency(plc, result, config);

            // Test throughput
            await BenchmarkThroughput(plc, result, config);

            // Test reliability
            await BenchmarkReliability(plc, result, config);

            result.OverallScore = CalculateBenchmarkScore(result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Benchmark failed: {ex.Message}");
        }

        result.EndTime = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Gets detailed performance statistics for the PLC connection.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>Detailed performance statistics.</returns>
    public static PerformanceStatistics GetPerformanceStatistics(this IRxS7 plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var metricsKey = $"{plc.IP}_{plc.PLCType}";
        var counter = _performanceCounters.GetOrAdd(metricsKey, _ => new PerformanceCounter());
        var connectionMetrics = _connectionMetrics.GetOrAdd(metricsKey, _ => new ConnectionMetrics());

        return new PerformanceStatistics
        {
            PLCIdentifier = metricsKey,
            TotalOperations = counter.TotalOperations,
            TotalErrors = counter.TotalErrors,
            AverageResponseTime = counter.GetAverageResponseTime(),
            OperationsPerSecond = counter.GetOperationsPerSecond(),
            ErrorRate = counter.GetErrorRate(),
            ConnectionUptime = connectionMetrics.GetUptime(),
            ReconnectionCount = connectionMetrics.ReconnectionCount,
            LastUpdated = DateTime.UtcNow
        };
    }

    private static PerformanceCounter GetPerformanceCounter(IRxS7 plc)
    {
        var metricsKey = $"{plc.IP}_{plc.PLCType}";
        return _performanceCounters.GetOrAdd(metricsKey, _ => new PerformanceCounter());
    }

    private static Dictionary<string, List<string>> GroupTagsByDataBlock(IEnumerable<string> tagNames, IRxS7 plc)
    {
        var grouped = new Dictionary<string, List<string>>();

        foreach (var tagName in tagNames)
        {
            var dataBlock = ExtractDataBlockFromTag(tagName, plc);
            if (!grouped.ContainsKey(dataBlock))
            {
                grouped[dataBlock] = [];
            }

            grouped[dataBlock].Add(tagName);
        }

        return grouped;
    }

    private static Dictionary<string, Dictionary<string, T>> GroupWritesByDataBlock<T>(
        Dictionary<string, T> values, IRxS7 plc)
    {
        var grouped = new Dictionary<string, Dictionary<string, T>>();

        foreach (var kvp in values)
        {
            var dataBlock = ExtractDataBlockFromTag(kvp.Key, plc);
            if (!grouped.ContainsKey(dataBlock))
            {
                grouped[dataBlock] = [];
            }

            grouped[dataBlock][kvp.Key] = kvp.Value;
        }

        return grouped;
    }

    private static string ExtractDataBlockFromTag(string tagName, IRxS7 plc)
    {
        var res = plc.GetTag(tagName);

        // Try to get the tag from the tag list first
        if (res.tag is Tag tag)
        {
            return ExtractDataBlockFromAddress(tag.Address);
        }

        // Fallback to using the tag name as address
        return ExtractDataBlockFromAddress(tagName);
    }

    private static string ExtractDataBlockFromAddress(string? address)
    {
        if (string.IsNullOrEmpty(address) || !address!.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
        {
            return "SYSTEM";
        }

        var dotIndex = address.IndexOf('.');
        return dotIndex <= 2 ? "SYSTEM" : address.Substring(0, dotIndex);
    }

    private static async Task BenchmarkLatency(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var latencies = new List<double>();

        for (var i = 0; i < config.LatencyTestCount; i++)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await plc.GetCpuInfo().FirstAsync();
                stopwatch.Stop();
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);

                if (i < config.LatencyTestCount - 1)
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Latency test {i + 1} failed: {ex.Message}");
            }
        }

        if (latencies.Count > 0)
        {
            result.AverageLatencyMs = latencies.Average();
            result.MinLatencyMs = latencies.Min();
            result.MaxLatencyMs = latencies.Max();
        }
    }

    private static async Task BenchmarkThroughput(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var operations = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (stopwatch.Elapsed < config.ThroughputTestDuration)
            {
                await plc.GetCpuInfo().FirstAsync();
                operations++;
            }

            result.OperationsPerSecond = operations / stopwatch.Elapsed.TotalSeconds;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Throughput test failed: {ex.Message}");
        }
    }

    private static async Task BenchmarkReliability(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var successCount = 0;
        var totalOperations = config.ReliabilityTestCount;

        for (var i = 0; i < totalOperations; i++)
        {
            try
            {
                await plc.GetCpuInfo().FirstAsync();
                successCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Reliability test {i + 1} failed: {ex.Message}");
            }

            if (i < totalOperations - 1)
            {
                await Task.Delay(50);
            }
        }

        result.ReliabilityRate = (double)successCount / totalOperations;
    }

    private static double CalculateBenchmarkScore(BenchmarkResult result)
    {
        var score = 0.0;

        // Latency score (lower is better)
        if (result.AverageLatencyMs > 0)
        {
            score += Math.Max(0, 25 - (result.AverageLatencyMs / 20)); // 25 points max
        }

        // Throughput score
        if (result.OperationsPerSecond > 0)
        {
            score += Math.Min(25, result.OperationsPerSecond * 2.5); // 25 points max
        }

        // Reliability score
        score += result.ReliabilityRate * 50; // 50 points max

        return Math.Min(100, score);
    }
}
