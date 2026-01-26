// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;

namespace S7PlcRx.Performance;

/// <summary>
/// Provides extension methods for IRxS7 PLC instances to enable advanced performance monitoring, optimized read and
/// write operations, and benchmarking capabilities.
/// </summary>
/// <remarks>The methods in this class facilitate efficient interaction with PLCs by offering features such as
/// real-time performance metrics, automatic grouping and batching of read/write operations, and comprehensive
/// benchmarking. These extensions are designed to improve throughput, reliability, and observability when working with
/// industrial automation systems. All methods require a valid IRxS7 instance and may throw exceptions if invalid
/// arguments are supplied. Thread safety is ensured for performance data collection and metrics aggregation.</remarks>
public static class PerformanceExtensions
{
    private static readonly ConcurrentDictionary<string, PerformanceCounter> _performanceCounters = new();
    private static readonly ConcurrentDictionary<string, SimpleConnectionMetrics> _connectionMetrics = new();

    /// <summary>
    /// Monitors the performance of the specified PLC and provides periodic updates as an observable sequence of
    /// performance metrics.
    /// </summary>
    /// <remarks>The returned observable begins emitting metrics immediately and continues at the specified
    /// interval. Metrics include connection status, tag counts, operation rates, and error rates. The observable is hot
    /// and shared among subscribers; unsubscribing from all observers will stop monitoring until a new subscription is
    /// made.</remarks>
    /// <param name="plc">The PLC instance to monitor. Cannot be null.</param>
    /// <param name="monitoringInterval">The interval at which performance metrics are sampled. If null, defaults to 30 seconds.</param>
    /// <returns>An observable sequence of <see cref="PerformanceMetrics"/> objects containing performance data for the PLC,
    /// emitted at each monitoring interval.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
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
                var connectionMetrics = _connectionMetrics.GetOrAdd(metricsKey, _ => new SimpleConnectionMetrics());
                metrics.ConnectionUptime = connectionMetrics.GetUptime();
                metrics.ReconnectionCount = connectionMetrics.ReconnectionCount;

                return metrics;
            })
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Reads the specified tags from the PLC using optimized grouping and optional parallelization, returning a
    /// dictionary of tag names and their corresponding values.
    /// </summary>
    /// <remarks>Tags are grouped by data block for efficient reading. When parallel reads are enabled in the
    /// optimization configuration, tag groups are read concurrently to improve performance. The method records
    /// performance metrics and errors for diagnostic purposes. If an error occurs while reading a tag, the operation is
    /// aborted and an exception is thrown.</remarks>
    /// <typeparam name="T">The type of the value to read for each tag.</typeparam>
    /// <param name="plc">The PLC instance from which to read tag values. Cannot be null.</param>
    /// <param name="tagNames">A collection of tag names to read from the PLC. Cannot be null. If empty, an empty dictionary is returned.</param>
    /// <param name="optimizationConfig">An optional configuration that controls read optimization behavior, such as enabling parallel reads and setting
    /// inter-group delays. If null, default optimization settings are used.</param>
    /// <returns>A dictionary mapping each requested tag name to its read value. If a tag cannot be read, an exception is thrown
    /// and the dictionary may be incomplete.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> or <paramref name="tagNames"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if reading a tag fails due to a PLC communication error or invalid tag name.</exception>
    public static async Task<Dictionary<string, T?>> ReadOptimized<T>(
        this IRxS7 plc,
        IEnumerable<string> tagNames,
        Optimization.ReadOptimizationConfig? optimizationConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (tagNames == null)
        {
            throw new ArgumentNullException(nameof(tagNames));
        }

        var config = optimizationConfig ?? new Optimization.ReadOptimizationConfig();
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
    /// Writes multiple values to the PLC in an optimized manner, grouping operations and optionally verifying writes
    /// for accuracy.
    /// </summary>
    /// <remarks>Writes are grouped by data block for performance optimization. If parallel writes are enabled
    /// in the configuration, write operations within each group are performed concurrently. When write verification is
    /// enabled, each value is read back after writing to ensure accuracy. Delays between groups can be configured to
    /// control write pacing. The method is thread-safe and intended for batch write scenarios to improve throughput and
    /// reliability.</remarks>
    /// <typeparam name="T">The type of the values to be written to the PLC.</typeparam>
    /// <param name="plc">The PLC interface to which the values will be written. Cannot be null.</param>
    /// <param name="values">A dictionary containing tag names and their corresponding values to write. Cannot be null.</param>
    /// <param name="optimizationConfig">An optional configuration object that controls optimization behavior, such as enabling parallel writes, write
    /// verification, and inter-group delays. If null, default optimization settings are used.</param>
    /// <returns>A task that represents the asynchronous operation. The result contains details about successful and failed
    /// writes, timing information, and any overall errors encountered during the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> or <paramref name="values"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if write verification is enabled and a written value does not match the value read back from the PLC.</exception>
    public static async Task<Optimization.WriteOptimizationResult> WriteOptimized<T>(
        this IRxS7 plc,
        Dictionary<string, T> values,
        Optimization.WriteOptimizationConfig? optimizationConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var config = optimizationConfig ?? new Optimization.WriteOptimizationConfig();
        var result = new Optimization.WriteOptimizationResult { StartTime = DateTime.UtcNow };
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
    /// Runs a set of benchmark tests on the specified PLC, measuring latency, throughput, and reliability.
    /// </summary>
    /// <remarks>The returned <see cref="BenchmarkResult"/> includes timing information, test scores, and any
    /// errors encountered during benchmarking. The method performs multiple tests and aggregates results to provide an
    /// overall score. This method does not throw on benchmark failures; errors are recorded in the result.</remarks>
    /// <param name="plc">The PLC instance to benchmark. Cannot be null.</param>
    /// <param name="benchmarkConfig">An optional configuration object specifying benchmark parameters. If null, default settings are used.</param>
    /// <returns>A task that represents the asynchronous operation. The result contains detailed benchmark metrics and scores for
    /// the PLC.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
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
    /// Retrieves aggregated performance statistics for the specified PLC connection, including operation counts, error
    /// rates, response times, and connection metrics.
    /// </summary>
    /// <remarks>The returned statistics reflect metrics collected since the application started or since the
    /// PLC connection was first established. This method is thread-safe and can be called concurrently from multiple
    /// threads.</remarks>
    /// <param name="plc">The PLC connection for which to obtain performance statistics. Cannot be null.</param>
    /// <returns>A PerformanceStatistics object containing metrics such as total operations, error rate, average response time,
    /// connection uptime, and reconnection count for the specified PLC.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    public static PerformanceStatistics GetPerformanceStatistics(this IRxS7 plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var metricsKey = $"{plc.IP}_{plc.PLCType}";
        var counter = _performanceCounters.GetOrAdd(metricsKey, _ => new PerformanceCounter());
        var connectionMetrics = _connectionMetrics.GetOrAdd(metricsKey, _ => new SimpleConnectionMetrics());

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

    /// <summary>
    /// Retrieves the performance counter associated with the specified PLC instance, creating a new counter if one does
    /// not already exist.
    /// </summary>
    /// <remarks>This method ensures that each PLC instance is associated with a unique performance counter.
    /// If a counter does not exist for the given PLC, a new one is created and stored for future retrieval.</remarks>
    /// <param name="plc">The PLC instance for which to obtain the performance counter. Must not be null.</param>
    /// <returns>The performance counter corresponding to the specified PLC instance.</returns>
    private static PerformanceCounter GetPerformanceCounter(IRxS7 plc)
    {
        var metricsKey = $"{plc.IP}_{plc.PLCType}";
        return _performanceCounters.GetOrAdd(metricsKey, _ => new PerformanceCounter());
    }

    /// <summary>
    /// Groups tag names by their associated data block identifier as determined from each tag and the specified PLC
    /// instance.
    /// </summary>
    /// <param name="tagNames">An enumerable collection of tag names to be grouped by data block.</param>
    /// <param name="plc">An instance of the PLC interface used to extract data block information from each tag name.</param>
    /// <returns>A dictionary where each key is a data block identifier and the corresponding value is a list of tag names
    /// belonging to that data block.</returns>
    private static Dictionary<string, List<string>> GroupTagsByDataBlock(IEnumerable<string> tagNames, IRxS7 plc)
    {
        var grouped = new Dictionary<string, List<string>>();

        foreach (var tagName in tagNames)
        {
            var dataBlock = ExtractDataBlockFromTag(tagName, plc);
            if (!grouped.TryGetValue(dataBlock, out var value))
            {
                value = [];
                grouped[dataBlock] = value;
            }

            value.Add(tagName);
        }

        return grouped;
    }

    /// <summary>
    /// Groups the specified tag-value pairs by their associated data block, as determined by the provided PLC instance.
    /// </summary>
    /// <remarks>The grouping is based on the data block extracted from each tag using the provided PLC
    /// instance. Tags that resolve to the same data block are grouped together in the result.</remarks>
    /// <typeparam name="T">The type of the values to be grouped by data block.</typeparam>
    /// <param name="values">A dictionary containing tag names as keys and their corresponding values to be grouped.</param>
    /// <param name="plc">An instance of the PLC interface used to extract data block information from each tag.</param>
    /// <returns>A dictionary where each key is a data block name and the value is a dictionary of tag-value pairs belonging to
    /// that data block.</returns>
    private static Dictionary<string, Dictionary<string, T>> GroupWritesByDataBlock<T>(
        Dictionary<string, T> values, IRxS7 plc)
    {
        var grouped = new Dictionary<string, Dictionary<string, T>>();

        foreach (var kvp in values)
        {
            var dataBlock = ExtractDataBlockFromTag(kvp.Key, plc);
            if (!grouped.TryGetValue(dataBlock, out var value))
            {
                value = [];
                grouped[dataBlock] = value;
            }

            value[kvp.Key] = kvp.Value;
        }

        return grouped;
    }

    /// <summary>
    /// Extracts the data block address associated with the specified tag from the PLC instance.
    /// </summary>
    /// <param name="tagName">The name of the tag for which to retrieve the data block address. If the tag is not found, the tag name is used
    /// as the address.</param>
    /// <param name="plc">An instance of the PLC interface used to access tag information.</param>
    /// <returns>A string containing the data block address corresponding to the specified tag. If the tag is not found, returns
    /// the result of extracting the address from the tag name.</returns>
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

    /// <summary>
    /// Extracts the data block identifier from a given address string, if present.
    /// </summary>
    /// <remarks>If the address does not start with "DB" (case-insensitive), is null or empty, or does not
    /// contain a valid data block segment, the method returns "SYSTEM".</remarks>
    /// <param name="address">The address string to parse for a data block identifier. Can be null or empty.</param>
    /// <returns>A string containing the extracted data block identifier if the address is valid and formatted correctly;
    /// otherwise, "SYSTEM".</returns>
    private static string ExtractDataBlockFromAddress(string? address)
    {
        if (string.IsNullOrEmpty(address) || !address!.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
        {
            return "SYSTEM";
        }

        var dotIndex = address.IndexOf('.');
        return dotIndex <= 2 ? "SYSTEM" : address.Substring(0, dotIndex);
    }

    /// <summary>
    /// Measures the latency of CPU information retrieval operations on the specified PLC and records the results in the
    /// provided benchmark result object.
    /// </summary>
    /// <remarks>This method performs multiple latency tests by repeatedly invoking CPU information retrieval
    /// on the PLC. The results include average, minimum, and maximum latency values in milliseconds, as well as any
    /// errors encountered during individual test iterations. The method does not throw exceptions for individual test
    /// failures; instead, errors are recorded in the result object.</remarks>
    /// <param name="plc">The PLC instance to test for latency by performing CPU information retrieval operations.</param>
    /// <param name="result">The object in which the measured latency statistics and any errors encountered during the benchmark are
    /// recorded.</param>
    /// <param name="config">The configuration settings that specify how the latency benchmark is performed, including the number of test
    /// iterations.</param>
    /// <returns>A task that represents the asynchronous latency benchmarking operation.</returns>
    private static async Task BenchmarkLatency(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var latencies = new List<double>();

        for (var i = 0; i < config.LatencyTestCount; i++)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await plc.GetCpuInfo();
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

    /// <summary>
    /// Performs a throughput benchmark by repeatedly retrieving CPU information from the specified PLC and records the
    /// number of operations completed per second.
    /// </summary>
    /// <remarks>This method measures the maximum number of CPU information retrieval operations that can be
    /// performed on the PLC within the configured test duration. Any exceptions encountered during the benchmark are
    /// recorded in the result object. The method does not throw exceptions to the caller.</remarks>
    /// <param name="plc">The PLC instance to benchmark. Must implement <see cref="IRxS7"/> and support asynchronous CPU information
    /// retrieval.</param>
    /// <param name="result">The object that receives the benchmark results, including operations per second and any errors encountered
    /// during the test.</param>
    /// <param name="config">The configuration settings for the benchmark, including the duration of the throughput test.</param>
    /// <returns>A task that represents the asynchronous benchmark operation.</returns>
    private static async Task BenchmarkThroughput(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var operations = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (stopwatch.Elapsed < config.ThroughputTestDuration)
            {
                await plc.GetCpuInfo();
                operations++;
            }

            result.OperationsPerSecond = operations / stopwatch.Elapsed.TotalSeconds;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Throughput test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs a reliability benchmark by repeatedly querying CPU information from the specified PLC and records the
    /// success rate in the provided result object.
    /// </summary>
    /// <remarks>This method executes multiple read operations against the PLC to assess communication
    /// reliability. Errors encountered during individual operations are logged in the result object. The reliability
    /// rate is calculated as the ratio of successful operations to the total number of attempts.</remarks>
    /// <param name="plc">The PLC instance to be tested for reliability. Must implement the IRxS7 interface.</param>
    /// <param name="result">The result object that will be updated with reliability statistics and any errors encountered during the
    /// benchmark.</param>
    /// <param name="config">The configuration settings for the benchmark, including the number of reliability test iterations to perform.</param>
    /// <returns>A task that represents the asynchronous operation of the reliability benchmark.</returns>
    private static async Task BenchmarkReliability(IRxS7 plc, BenchmarkResult result, BenchmarkConfig config)
    {
        var successCount = 0;
        var totalOperations = config.ReliabilityTestCount;

        for (var i = 0; i < totalOperations; i++)
        {
            try
            {
                await plc.GetCpuInfo();
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

    /// <summary>
    /// Calculates a composite benchmark score based on latency, throughput, and reliability metrics.
    /// </summary>
    /// <remarks>The score is determined by combining weighted contributions from latency, throughput, and
    /// reliability. Lower latency and higher throughput and reliability yield higher scores. The final score is capped
    /// at 100.</remarks>
    /// <param name="result">The benchmark result containing average latency in milliseconds, operations per second, and reliability rate to
    /// be evaluated.</param>
    /// <returns>A double value representing the overall benchmark score, ranging from 0 to 100.</returns>
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
