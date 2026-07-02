// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Performance;
#else
namespace S7PlcRx.Performance;
#endif

/// <summary>
/// Represents the results of a performance benchmark, including timing, latency, throughput, reliability, and any
/// errors encountered during execution.
/// </summary>
/// <remarks>Use this class to access detailed metrics and diagnostic information from a completed benchmark run.
/// The properties provide summary statistics such as average, minimum, and maximum latency, as well as overall
/// reliability and score. Errors encountered during benchmarking are available in the <see cref="Errors"/> collection
/// for troubleshooting. This type is immutable except for its settable properties; thread safety is not guaranteed if
/// modified concurrently.</remarks>
public sealed class BenchmarkResult
{
    /// <summary>Gets or sets the benchmark start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the benchmark end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the average latency in milliseconds.</summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>Gets or sets the minimum latency in milliseconds.</summary>
    public double MinLatencyMs { get; set; }

    /// <summary>Gets or sets the maximum latency in milliseconds.</summary>
    public double MaxLatencyMs { get; set; }

    /// <summary>Gets or sets the operations per second.</summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>Gets or sets the reliability rate (0.0 to 1.0).</summary>
    public double ReliabilityRate { get; set; }

    /// <summary>Gets or sets the overall benchmark score (0 to 100).</summary>
    public double OverallScore { get; set; }

    /// <summary>Gets any errors encountered during benchmarking.</summary>
    public List<string> Errors { get; } = [];

    /// <summary>Gets the total benchmark duration.</summary>
    public TimeSpan TotalDuration => EndTime - StartTime;
}
