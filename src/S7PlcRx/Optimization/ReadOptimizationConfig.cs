// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>
/// Provides configuration options for optimizing read operations, including parallelism, delays, concurrency limits,
/// and timeouts within data block groups.
/// </summary>
/// <remarks>Use this class to fine-tune the performance characteristics of read operations in scenarios where
/// data is organized into block groups. Adjusting these settings can help balance throughput, latency, and resource
/// usage based on application requirements.</remarks>
public sealed class ReadOptimizationConfig
{
    /// <summary>Gets or sets a value indicating whether gets or sets whether to enable parallel reads within data block groups.</summary>
    public bool EnableParallelReads { get; set; } = true;

    /// <summary>Gets or sets the delay between data block groups in milliseconds.</summary>
    public int InterGroupDelayMs { get; set; }

    /// <summary>Gets or sets the maximum number of concurrent reads.</summary>
    public int MaxConcurrentReads { get; set; } = 10;

    /// <summary>Gets or sets the read timeout in milliseconds.</summary>
    public int ReadTimeoutMs { get; set; } = 5000;
}
