// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Result of performance benchmark.
/// </summary>
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
    public List<string> Errors { get; } = new();

    /// <summary>Gets the total benchmark duration.</summary>
    public TimeSpan TotalDuration => EndTime - StartTime;
}
