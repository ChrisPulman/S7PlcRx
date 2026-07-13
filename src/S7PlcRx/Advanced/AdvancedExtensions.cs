// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.BatchOperations;
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.Performance;
using S7PlcRx.Reactive.Production;
#else
using S7PlcRx.BatchOperations;
using S7PlcRx.Enums;
using S7PlcRx.Performance;
using S7PlcRx.Production;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Advanced;
#else
namespace S7PlcRx.Advanced;
#endif

/// <summary>
/// Provides advanced extension methods for efficient batch operations, diagnostics, and performance analysis on PLC
/// (Programmable Logic Controller) instances using the IRxS7 interface.
/// </summary>
/// <remarks>These extension methods enable high-performance reading, writing, monitoring, and analysis of PLC
/// variables, supporting scenarios such as batch updates, optimized data access, and system diagnostics. Methods are
/// designed to simplify complex PLC interactions and provide recommendations for optimization. All methods require a
/// valid IRxS7 instance and may throw exceptions if invalid arguments are supplied. Thread safety and performance
/// considerations are addressed where relevant in individual method documentation.</remarks>
public static class AdvancedExtensions
{
    /// <summary>Message used when a PLC extension method receives a null instance.</summary>
    private const string PlcNullMessage = "PLC instance cannot be null.";

    /// <summary>Defines the delay before reading a written value back for verification.</summary>
    private const int WriteVerificationDelayMilliseconds = 50;

    /// <summary>Defines the first valid separator position in a data-block address.</summary>
    private const int MinimumDataBlockDotIndex = 3;

    /// <summary>Defines the tag count above which connection pooling is recommended.</summary>
    private const int HighTagCountThreshold = 200;

    /// <summary>Defines the connection latency above which network checks are recommended.</summary>
    private const double HighConnectionLatencyMilliseconds = 500;

    /// <summary>Defines the inactive-tag ratio above which cleanup is recommended.</summary>
    private const double InactiveTagRatioThreshold = 0.3;

    /// <summary>Defines the maximum change rate for a slow-changing tag.</summary>
    private const double SlowTagChangesPerMinuteThreshold = 0.1;

    /// <summary>Defines the minimum change rate for a fast-changing tag.</summary>
    private const double FastTagChangesPerMinuteThreshold = 10;

    /// <summary>Provides advanced batch, diagnostics, and analysis extensions for PLC instances.</summary>
    /// <param name="plc">The PLC instance.</param>
    extension(IRxS7 plc)
    {
    /// <summary>
    /// Observes the values of multiple PLC variables as a batch and emits updates as a dictionary when any of the
    /// specified variables change.
    /// </summary>
    /// <remarks>The returned observable is hot and shared among all subscribers. If a specified variable is
    /// not already being polled, polling is automatically enabled for that variable. The sequence emits a new
    /// dictionary only when the set of variable values changes.</remarks>
    /// <typeparam name="T">The type of the variable values to observe. Must be compatible with the PLC variable types.</typeparam>
    /// <param name="variables">The names of the PLC variables to observe. If empty, an empty dictionary is emitted.</param>
    /// <returns>An observable sequence that emits a dictionary mapping each specified variable name to its most recent value.
    /// The dictionary is updated and emitted whenever any of the observed variables change.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="plc"/> parameter is null.</exception>
        public IObservable<Dictionary<string, T?>> ObserveBatch<T>(params string[] variables)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        if (variables is null || variables.Length == 0)
        {
            return Observable.Return(new Dictionary<string, T?>());
        }

        foreach (var variable in variables)
        {
            if (!plc.TagList.ContainsKey(variable))
            {
                _ = plc.GetTag(variable).SetTagPollIng(true);
            }
        }

        return plc.ObserveAll
            .Where(t => t is not null && variables.Contains(t.Name) && TagValueIsValid<T>(t))
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
    /// Asynchronously reads the values of multiple variables from the PLC and returns a dictionary mapping variable
    /// names to their values.
    /// </summary>
    /// <remarks>If a variable name does not exist in the PLC or cannot be read, its value in the returned
    /// dictionary will be the default value for type T. The order of the returned dictionary corresponds to the order
    /// of the requested variable names. This method may perform the reads in parallel for efficiency.</remarks>
    /// <typeparam name="T">The type of the variable values to read from the PLC.</typeparam>
    /// <param name="variables">An array of variable names to read from the PLC. Each name must correspond to a valid variable in the PLC.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a dictionary mapping each requested
    /// variable name to its value of type T, or to the default value of T if the variable could not be read or does not
    /// exist. If no variables are specified, returns an empty dictionary.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the plc parameter is null.</exception>
        public async Task<Dictionary<string, T?>> ValueBatch<T>(params string[] variables)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        return await plc.ReadValuesAsync<T>(variables).ConfigureAwait(false);
    }

    /// <summary>Writes a batch of values to the PLC asynchronously using the specified tag-value pairs.</summary>
    /// <remarks>If the underlying PLC implementation supports batch writing, the method attempts to write all
    /// values in a single operation for improved performance. Otherwise, values are written individually in parallel.
    /// No action is taken if the dictionary is null or empty.</remarks>
    /// <typeparam name="T">The type of the values to write to the PLC.</typeparam>
    /// <param name="values">A dictionary containing tag names as keys and the corresponding values to write. Cannot be null or empty.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task ValueBatch<T>(Dictionary<string, T> values)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        await plc.WriteValuesAsync(values).ConfigureAwait(false);
    }

    /// <summary>Reads a batch of tags from the PLC in an optimized manner, grouping reads by data block to improve performance.</summary>
    /// <remarks>If the tagMapping dictionary is null or empty, the method returns a successful result with no
    /// values. Tags are grouped by data block to minimize communication overhead. If a tag does not exist in the PLC's
    /// tag list, it is added before reading. Each tag read is subject to the specified timeout.</remarks>
    /// <typeparam name="T">The type of the values to read from the PLC tags.</typeparam>
    /// <param name="tagMapping">A dictionary mapping logical tag names to their corresponding PLC addresses. Each key is the tag name, and each
    /// value is the PLC address to read.</param>
    /// <param name="timeoutMs">The maximum time, in milliseconds, to wait for each tag read operation before timing out. The default is 5000
    /// milliseconds.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a BatchReadResult{T} with the values
    /// read, per-tag success status, and any errors encountered.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the plc parameter is null.</exception>
        public async Task<BatchReadResult<T>> ReadBatchOptimized<T>(
            Dictionary<string, string> tagMapping,
            int timeoutMs = 5000)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        var result = new BatchReadResult<T>();

        if (tagMapping is null || tagMapping.Count == 0)
        {
            result.OverallSuccess = true;
            return result;
        }

        EnsureMappedTags<T>(plc, tagMapping);
        var dataBlockGroups = GroupTagMappingsByDataBlock(tagMapping);
        var groupResultsArray = await ReadDataBlockGroupsAsync<T>(plc, dataBlockGroups, timeoutMs);
        ApplyReadGroupResults(result, groupResultsArray);

        result.OverallSuccess = result.Errors.Count == 0;
        return result;
    }

    /// <summary>
    /// Writes a batch of values to the specified PLC in an optimized manner, with optional write verification and
    /// rollback support.
    /// </summary>
    /// <remarks>If enableRollback is set to true and any write fails, the method attempts to restore all
    /// affected PLC addresses to their original values. Write verification, if enabled, reads back each value after
    /// writing to ensure correctness. This method is asynchronous and should be awaited to ensure completion of all
    /// write and verification operations.</remarks>
    /// <typeparam name="T">The type of the values to write to the PLC.</typeparam>
    /// <param name="values">A dictionary mapping PLC variable addresses to the values to write. Each key represents a PLC address, and each
    /// value is the data to write to that address. If the dictionary is null or empty, no write operations are
    /// performed.</param>
    /// <param name="verifyWrites">true to verify each write by reading back the value after writing; otherwise, false. Verification may increase
    /// operation time.</param>
    /// <param name="enableRollback">true to enable rollback of all written values to their original state if any write fails; otherwise, false.
    /// Rollback is attempted only if an error occurs during the batch write.</param>
    /// <returns>A BatchWriteResult object containing the outcome of the batch write operation, including per-address success and
    /// error information. If no values are provided, the result indicates overall success.</returns>
    /// <exception cref="ArgumentNullException">Thrown if plc is null.</exception>
        public async Task<BatchWriteResult> WriteBatchOptimized<T>(
            Dictionary<string, T> values,
            bool verifyWrites = false,
            bool enableRollback = false)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        var result = new BatchWriteResult();
        var originalValues = new Dictionary<string, T>();

        if (values is null || values.Count == 0)
        {
            result.OverallSuccess = true;
            return result;
        }

        if (enableRollback)
        {
            await CaptureOriginalValues(plc, values, originalValues, result);
        }

        await WriteBatchValues(plc, values, verifyWrites, result);

        if (enableRollback && result.Errors.Count > 0)
        {
            RollbackValues(plc, originalValues, result);
            result.RollbackPerformed = true;
        }

        result.OverallSuccess = result.Errors.Count == 0;
        return result;
    }

    /// <summary>Asynchronously collects diagnostic information and performance metrics from the specified PLC instance.</summary>
    /// <remarks>If the PLC is not connected, only basic information is included in the diagnostics. For
    /// S7-1500 PLCs, additional CPU information and connection latency are measured. Any errors encountered during
    /// diagnostic collection are recorded in the Errors property of the returned ProductionDiagnostics
    /// object.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ProductionDiagnostics object with
    /// collected diagnostic data, tag metrics, and optimization recommendations.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the plc parameter is null.</exception>
        public async Task<ProductionDiagnostics> GetDiagnostics()
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
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
                if (plc.PLCType == CpuType.S71500)
                {
                    // Test connection latency
                    var latencyStart = DateTime.UtcNow;
                    var cpuInfo = await plc.GetCpuInfo();
                    diagnostics.ConnectionLatencyMs = (DateTime.UtcNow - latencyStart).TotalMilliseconds;
                    diagnostics.CPUInformation = cpuInfo;
                }

                // Get tag statistics
                var allTags = new List<Tag>();
                var activeTags = 0;
                var inactiveTags = 0;
                foreach (var item in plc.TagList)
                {
                    if (item is not Tag tag)
                    {
                        continue;
                    }

                    allTags.Add(tag);
                    if (tag.DoNotPoll)
                    {
                        inactiveTags++;
                    }
                    else
                    {
                        activeTags++;
                    }
                }

                diagnostics.TagMetrics = new ProductionTagMetrics
                {
                    TotalTags = allTags.Count,
                    ActiveTags = activeTags,
                    InactiveTags = inactiveTags,
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
    /// Analyzes tag change performance on the specified PLC over a given monitoring duration and returns a summary of
    /// tag change frequencies and recommendations.
    /// </summary>
    /// <remarks>This method subscribes to all tag changes on the PLC and tracks the frequency of changes for
    /// each tag during the specified monitoring period. The analysis includes total tag changes, per-tag change counts,
    /// and suggestions for optimizing performance based on observed activity. The method is thread-safe and does not
    /// block the calling thread.</remarks>
    /// <param name="monitoringDuration">The length of time to observe tag changes. Must be a positive time span.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a PerformanceAnalysis object with
    /// tag change statistics and performance recommendations for the monitored period.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the plc parameter is null.</exception>
        public async Task<PerformanceAnalysis> AnalyzePerformance(TimeSpan monitoringDuration)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc), PlcNullMessage);
        }

        var analysis = new PerformanceAnalysis { StartTime = DateTime.UtcNow };
        var tagChangeCounts = new ConcurrentDictionary<string, int>();
        var lastValues = new ConcurrentDictionary<string, object?>();

        // Monitor tag changes
        var subscription = plc.ObserveAll
            .Subscribe(tag =>
            {
                if (tag?.Name is null)
                {
                    return;
                }

                var changed = !lastValues.TryGetValue(tag.Name, out var lastValue) || !Equals(lastValue, tag.Value);
                if (!changed)
                {
                    return;
                }

                lastValues[tag.Name] = tag.Value;
                _ = tagChangeCounts.AddOrUpdate(tag.Name, 1, (_, count) => count + 1);
            });

        // Monitor for specified duration
        await Task.Delay(monitoringDuration);
        subscription.Dispose();

        analysis.EndTime = DateTime.UtcNow;
        analysis.MonitoringDuration = monitoringDuration;

        // Analyze results
        var totalTagChanges = 0;
        foreach (var kvp in tagChangeCounts)
        {
            analysis.TagChangeFrequencies[kvp.Key] = kvp.Value;
            totalTagChanges += kvp.Value;
        }

        analysis.TotalTagChanges = totalTagChanges;
        analysis.AverageChangesPerTag = !tagChangeCounts.IsEmpty ? (double)totalTagChanges / tagChangeCounts.Count : 0;

        // Generate recommendations
        analysis.Recommendations = GeneratePerformanceRecommendations(tagChangeCounts, monitoringDuration);

        return analysis;
    }

    /// <summary>
    /// Creates a new high-performance tag group for batch reading or writing of multiple tags from the specified PLC
    /// connection.
    /// </summary>
    /// <remarks>Use this method to efficiently manage and operate on multiple tags as a single group, which
    /// can improve performance for batch operations.</remarks>
    /// <typeparam name="T">The data type of the tag values in the group.</typeparam>
    /// <param name="groupName">The name assigned to the tag group. Used to identify the group within the PLC context.</param>
    /// <param name="tagNames">An array of tag names to include in the group. Each name must correspond to a valid tag in the PLC.</param>
    /// <returns>A new instance of HighPerformanceTagGroup{T} containing the specified tags, associated with the given PLC
    /// connection.</returns>
        public HighPerformanceTagGroup<T> CreateTagGroup<T>(string groupName, params string[] tagNames) => new(plc, groupName, tagNames);
    }

    /// <summary>Ensures all mapped tags exist on the PLC.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagMapping">The tag mapping.</param>
    private static void EnsureMappedTags<T>(IRxS7 plc, Dictionary<string, string> tagMapping)
    {
        foreach (var mapping in tagMapping)
        {
            if (!plc.TagList.ContainsKey(mapping.Key))
            {
                _ = plc.AddUpdateTagItem<T>(mapping.Key, mapping.Value).SetTagPollIng(false);
            }
        }
    }

    /// <summary>Groups tag mappings by data block.</summary>
    /// <param name="tagMapping">The tag mapping.</param>
    /// <returns>The tag mappings grouped by data block.</returns>
    private static Dictionary<string, List<KeyValuePair<string, string>>> GroupTagMappingsByDataBlock(Dictionary<string, string> tagMapping)
    {
        var dataBlockGroups = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var mapping in tagMapping)
        {
            var dataBlock = ExtractDataBlockId(mapping.Value);
            if (!dataBlockGroups.TryGetValue(dataBlock, out var group))
            {
                group = [];
                dataBlockGroups[dataBlock] = group;
            }

            group.Add(mapping);
        }

        return dataBlockGroups;
    }

    /// <summary>Reads all grouped data-block tag mappings.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="dataBlockGroups">The grouped tag mappings.</param>
    /// <param name="timeoutMs">The read timeout in milliseconds.</param>
    /// <returns>The read results and errors for each group.</returns>
    private static Task<(Dictionary<string, T> Results, Dictionary<string, string> Errors)[]> ReadDataBlockGroupsAsync<T>(
        IRxS7 plc,
        Dictionary<string, List<KeyValuePair<string, string>>> dataBlockGroups,
        int timeoutMs)
    {
        var readTasks = new List<Task<(Dictionary<string, T> Results, Dictionary<string, string> Errors)>>(dataBlockGroups.Count);
        foreach (var group in dataBlockGroups.Values)
        {
            readTasks.Add(ReadDataBlockGroupAsync<T>(plc, group, timeoutMs));
        }

        return Task.WhenAll(readTasks);
    }

    /// <summary>Reads one data-block group.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="group">The group to read.</param>
    /// <param name="timeoutMs">The read timeout in milliseconds.</param>
    /// <returns>The read results and errors.</returns>
    private static async Task<(Dictionary<string, T> Results, Dictionary<string, string> Errors)> ReadDataBlockGroupAsync<T>(
        IRxS7 plc,
        List<KeyValuePair<string, string>> group,
        int timeoutMs)
    {
        var groupResults = new Dictionary<string, T>();
        var groupErrors = new Dictionary<string, string>();

        foreach (var tag in group)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                var value = await plc.ValueAsync<T>(tag.Key, cts.Token).ConfigureAwait(false);
                if (value is not null)
                {
                    groupResults[tag.Key] = value;
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

        return (groupResults, groupErrors);
    }

    /// <summary>Applies grouped read results to a batch result.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="result">The batch read result to update.</param>
    /// <param name="groupResultsArray">The grouped read results.</param>
    private static void ApplyReadGroupResults<T>(
        BatchReadResult<T> result,
        (Dictionary<string, T> Results, Dictionary<string, string> Errors)[] groupResultsArray)
    {
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
    }

    /// <summary>Captures original values before a rollback-enabled write.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="originalValues">The original values dictionary to populate.</param>
    /// <param name="result">The batch write result to update.</param>
    /// <returns>A task that represents the asynchronous capture operation.</returns>
    private static async Task CaptureOriginalValues<T>(
        IRxS7 plc,
        Dictionary<string, T> values,
        Dictionary<string, T> originalValues,
        BatchWriteResult result)
    {
        foreach (var kvp in values)
        {
            try
            {
                var originalValue = await plc.Value<T>(kvp.Key).ConfigureAwait(false);
                if (originalValue is not null)
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

    /// <summary>Writes batch values and optionally verifies them.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">The values to write.</param>
    /// <param name="verifyWrites">Whether writes should be verified.</param>
    /// <param name="result">The batch write result to update.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    private static async Task WriteBatchValues<T>(
        IRxS7 plc,
        Dictionary<string, T> values,
        bool verifyWrites,
        BatchWriteResult result)
    {
        foreach (var kvp in values)
        {
            try
            {
                plc.Value(kvp.Key, kvp.Value);
                result.Success[kvp.Key] = true;

                if (verifyWrites && !await VerifyWrite(plc, kvp).ConfigureAwait(false))
                {
                    result.Success[kvp.Key] = false;
                    result.Errors[kvp.Key] = "Write verification failed";
                }
            }
            catch (Exception ex)
            {
                result.Success[kvp.Key] = false;
                result.Errors[kvp.Key] = ex.Message;
            }
        }
    }

    /// <summary>Verifies a single written value.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="kvp">The written value.</param>
    /// <returns>true when the read-back value matches; otherwise, false.</returns>
    private static async Task<bool> VerifyWrite<T>(IRxS7 plc, KeyValuePair<string, T> kvp)
    {
        await Task.Delay(WriteVerificationDelayMilliseconds).ConfigureAwait(false);
        var readBack = await plc.Value<T>(kvp.Key).ConfigureAwait(false);
        return readBack is not null && EqualityComparer<T>.Default.Equals(readBack, kvp.Value);
    }

    /// <summary>Rolls back written values.</summary>
    /// <typeparam name="T">The tag value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="originalValues">The original values to restore.</param>
    /// <param name="result">The batch write result to update.</param>
    private static void RollbackValues<T>(IRxS7 plc, Dictionary<string, T> originalValues, BatchWriteResult result)
    {
        foreach (var kvp in originalValues)
        {
            try
            {
                plc.Value(kvp.Key, kvp.Value);
            }
            catch (Exception ex)
            {
                result.Success[kvp.Key] = false;
                result.Errors[kvp.Key] = $"Rollback failed: {ex.Message}";
            }
        }
    }

    /// <summary>Extracts the data block identifier from the specified address string.</summary>
    /// <remarks>If the address does not start with "DB" or is null or empty, the method returns "SYSTEM". If
    /// the address does not contain a period ('.') after the "DB" prefix, or if the period occurs at or before the
    /// third character, "SYSTEM" is also returned.</remarks>
    /// <param name="address">The address string from which to extract the data block identifier. Must not be null or empty and should start
    /// with "DB" to return a valid identifier.</param>
    /// <returns>A string containing the data block identifier if the address is valid and starts with "DB"; otherwise, "SYSTEM".</returns>
    private static string ExtractDataBlockId(string address)
    {
        if (string.IsNullOrEmpty(address) || !address.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
        {
            return "SYSTEM";
        }

        var dotIndex = address.IndexOf('.');
        return dotIndex < MinimumDataBlockDotIndex ? "SYSTEM" : address[..dotIndex];
    }

    /// <summary>Analyzes the distribution of tags across data blocks and returns a count of tags per data block identifier.</summary>
    /// <param name="tags">The collection of tags to analyze. Each tag must have a non-null address.</param>
    /// <returns>A dictionary mapping each data block identifier to the number of tags associated with it. If no tags are
    /// provided, the dictionary will be empty.</returns>
    private static Dictionary<string, int> AnalyzeDataBlockDistribution(IEnumerable<Tag> tags)
    {
        var distribution = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        foreach (var tag in tags)
        {
            var dataBlock = ExtractDataBlockId(tag.Address!);
            distribution[dataBlock] = distribution.TryGetValue(dataBlock, out var count) ? count + 1 : 1;
        }

        return distribution;
    }

    /// <summary>
    /// Analyzes production diagnostics and generates a list of optimization recommendations based on detected
    /// performance issues.
    /// </summary>
    /// <param name="diagnostics">The diagnostics data containing tag metrics and connection latency information to be analyzed.</param>
    /// <returns>A list of strings containing recommended optimizations. The list is empty if no issues are detected.</returns>
    private static List<string> GenerateOptimizationRecommendations(ProductionDiagnostics diagnostics)
    {
        var recommendations = new List<string>();

        if (diagnostics.TagMetrics.TotalTags > HighTagCountThreshold)
        {
            recommendations.Add("High tag count detected - consider implementing connection pooling");
        }

        if (diagnostics.ConnectionLatencyMs > HighConnectionLatencyMilliseconds)
        {
            recommendations.Add($"High latency ({diagnostics.ConnectionLatencyMs:F0}ms) - check network configuration");
        }

        if (diagnostics.TagMetrics.InactiveTags > diagnostics.TagMetrics.TotalTags * InactiveTagRatioThreshold)
        {
            recommendations.Add($"Many inactive tags ({diagnostics.TagMetrics.InactiveTags}) - consider cleanup");
        }

        return recommendations;
    }

    /// <summary>
    /// Analyzes tag change frequencies over a specified monitoring period and generates performance optimization
    /// recommendations based on observed activity patterns.
    /// </summary>
    /// <remarks>Tags that change infrequently may benefit from reduced polling frequency, while tags that
    /// change frequently may be candidates for batch operations. This method does not modify the input data.</remarks>
    /// <param name="tagChangeCounts">A thread-safe dictionary mapping tag names to the number of times each tag changed during the monitoring period.
    /// Each entry represents the total change count for a specific tag.</param>
    /// <param name="monitoringDuration">The total duration over which tag changes were monitored. Must be a positive time interval.</param>
    /// <returns>A list of performance recommendations based on the frequency of tag changes. The list is empty if no
    /// recommendations are applicable or if the monitoring duration is zero or negative.</returns>
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
        var slowChangingTagCount = 0;
        var fastChangingTagCount = 0;
        foreach (var kvp in tagChangeCounts)
        {
            var changesPerMinute = kvp.Value / totalMinutes;
            if (changesPerMinute < SlowTagChangesPerMinuteThreshold)
            {
                slowChangingTagCount++;
            }

            if (changesPerMinute > FastTagChangesPerMinuteThreshold)
            {
                fastChangingTagCount++;
            }
        }

        if (slowChangingTagCount > 0)
        {
            recommendations.Add($"Consider reducing polling frequency for {slowChangingTagCount} slow-changing tags");
        }

        // Identify fast-changing tags
        if (fastChangingTagCount > 0)
        {
            recommendations.Add($"Consider grouping {fastChangingTagCount} fast-changing tags for batch operations");
        }

        return recommendations;
    }

    /// <summary>Determines whether the specified tag is non-null and its value is compatible with the specified type parameter.</summary>
    /// <remarks>If T is object, any non-null tag is considered valid regardless of its value type.</remarks>
    /// <typeparam name="T">The type to check the tag's value against.</typeparam>
    /// <param name="tag">The tag to validate. May be null.</param>
    /// <returns>true if the tag is not null and its value is compatible with type T; otherwise, false.</returns>
    private static bool TagValueIsValid<T>(Tag? tag) =>
        tag is not null && (typeof(object) == typeof(T) || (tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T)));
}
