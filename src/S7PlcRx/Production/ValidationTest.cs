// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Production;
#else
namespace S7PlcRx.Production;
#endif

/// <summary>Represents the result and metadata of a validation test, including timing, outcome, and related details.</summary>
public sealed class ValidationTest
{
    /// <summary>Gets or sets the test name.</summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>Gets or sets the test start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the test end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets whether the test was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets any error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets additional test details.</summary>
    public List<string> Details { get; } = [];

    /// <summary>Gets the test duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;
}
