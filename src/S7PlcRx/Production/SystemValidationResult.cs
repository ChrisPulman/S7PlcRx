// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// System validation result for production readiness testing.
/// </summary>
public sealed class SystemValidationResult
{
    /// <summary>Gets or sets the validation start time.</summary>
    public DateTime ValidationStartTime { get; set; }

    /// <summary>Gets or sets the validation end time.</summary>
    public DateTime ValidationEndTime { get; set; }

    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the system is production ready.</summary>
    public bool IsProductionReady { get; set; }

    /// <summary>Gets or sets the overall validation score (0-100).</summary>
    public double OverallScore { get; set; }

    /// <summary>Gets the individual validation tests.</summary>
    public List<ValidationTest> ValidationTests { get; } = [];

    /// <summary>Gets critical errors that prevent production use.</summary>
    public List<string> CriticalErrors { get; } = [];

    /// <summary>Gets the total validation time.</summary>
    public TimeSpan TotalValidationTime => ValidationEndTime - ValidationStartTime;
}
