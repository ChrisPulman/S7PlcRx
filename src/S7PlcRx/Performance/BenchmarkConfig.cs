// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Represents the configuration settings for benchmark tests, including parameters for latency, throughput, and
/// reliability measurements.
/// </summary>
/// <remarks>Use this class to specify the number and duration of various benchmark tests when running performance
/// evaluations. All properties are configurable to tailor the benchmarking process to specific requirements.</remarks>
public sealed class BenchmarkConfig
{
    /// <summary>Gets or sets the number of latency tests to perform.</summary>
    public int LatencyTestCount { get; set; } = 10;

    /// <summary>Gets or sets the duration for throughput testing.</summary>
    public TimeSpan ThroughputTestDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the number of reliability tests to perform.</summary>
    public int ReliabilityTestCount { get; set; } = 20;
}
