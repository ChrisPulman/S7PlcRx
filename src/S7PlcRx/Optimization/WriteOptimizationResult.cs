// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Result of write optimization operations.
/// </summary>
public sealed class WriteOptimizationResult
{
    /// <summary>Gets or sets the operation start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the operation end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets successful writes with their durations.</summary>
    public Dictionary<string, TimeSpan> SuccessfulWrites { get; } = new();

    /// <summary>Gets failed writes with error messages.</summary>
    public Dictionary<string, string> FailedWrites { get; } = new();

    /// <summary>Gets or sets any overall error message.</summary>
    public string? OverallError { get; set; }

    /// <summary>Gets the total operation duration.</summary>
    public TimeSpan TotalDuration => EndTime - StartTime;

    /// <summary>Gets the success rate.</summary>
    public double SuccessRate => SuccessfulWrites.Count + FailedWrites.Count > 0
        ? (double)SuccessfulWrites.Count / (SuccessfulWrites.Count + FailedWrites.Count)
        : 0.0;
}
