// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Represents the result of a write optimization operation, including timing information, per-write outcomes, and
/// overall error details.
/// </summary>
/// <remarks>Use this class to access detailed results of a write optimization process, such as the start and end
/// times, lists of successful and failed writes, and aggregate metrics like total duration and success rate. The
/// dictionaries provide per-write information, with keys typically representing write identifiers. This type is
/// immutable except for properties explicitly marked as settable.</remarks>
public sealed class WriteOptimizationResult
{
    /// <summary>Gets or sets the operation start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the operation end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets successful writes with their durations.</summary>
    public Dictionary<string, TimeSpan> SuccessfulWrites { get; } = [];

    /// <summary>Gets failed writes with error messages.</summary>
    public Dictionary<string, string> FailedWrites { get; } = [];

    /// <summary>Gets or sets any overall error message.</summary>
    public string? OverallError { get; set; }

    /// <summary>Gets the total operation duration.</summary>
    public TimeSpan TotalDuration => EndTime - StartTime;

    /// <summary>Gets the success rate.</summary>
    public double SuccessRate => SuccessfulWrites.Count + FailedWrites.Count > 0
        ? (double)SuccessfulWrites.Count / (SuccessfulWrites.Count + FailedWrites.Count)
        : 0.0;
}
