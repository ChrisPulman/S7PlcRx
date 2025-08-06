// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Configuration for performance benchmarks.
/// </summary>
public sealed class BenchmarkConfig
{
    /// <summary>Gets or sets the number of latency tests to perform.</summary>
    public int LatencyTestCount { get; set; } = 10;

    /// <summary>Gets or sets the duration for throughput testing.</summary>
    public TimeSpan ThroughputTestDuration { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the number of reliability tests to perform.</summary>
    public int ReliabilityTestCount { get; set; } = 20;
}
