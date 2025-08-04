// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Configuration for write optimization.
/// </summary>
public sealed class WriteOptimizationConfig
{
    /// <summary>Gets or sets a value indicating whether gets or sets whether to enable parallel writes within data block groups.</summary>
    public bool EnableParallelWrites { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets whether to verify writes by reading back.</summary>
    public bool VerifyWrites { get; set; }

    /// <summary>Gets or sets the delay between data block groups in milliseconds.</summary>
    public int InterGroupDelayMs { get; set; } = 50;

    /// <summary>Gets or sets the maximum number of concurrent writes.</summary>
    public int MaxConcurrentWrites { get; set; } = 5;

    /// <summary>Gets or sets the write timeout in milliseconds.</summary>
    public int WriteTimeoutMs { get; set; } = 5000;
}
