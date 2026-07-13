// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Production;
#else
namespace S7PlcRx.Production;
#endif

/// <summary>Represents configuration settings for validating production system performance and reliability.</summary>
/// <remarks>Use this class to specify thresholds and criteria for production validation checks, such as
/// acceptable response times, reliability rates, and minimum production scores. These settings can be adjusted to match
/// the requirements of different production environments.</remarks>
public sealed class ProductionValidationConfig
{
    /// <summary>Defines the default maximum acceptable response time in milliseconds.</summary>
    private const int DefaultMaximumResponseTimeMilliseconds = 500;

    /// <summary>Gets or sets the maximum acceptable response time.</summary>
    public TimeSpan MaxAcceptableResponseTime { get; set; } = TimeSpan.FromMilliseconds(DefaultMaximumResponseTimeMilliseconds);

    /// <summary>Gets or sets the minimum reliability rate (0.0 to 1.0).</summary>
    public double MinimumReliabilityRate { get; set; } = 0.95;

    /// <summary>Gets or sets the number of operations to test for reliability.</summary>
    public int ReliabilityTestCount { get; set; } = 10;

    /// <summary>Gets or sets the minimum production score (0 to 100).</summary>
    public double MinimumProductionScore { get; set; } = 80.0;
}
