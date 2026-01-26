// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Represents the results and metrics of a performance analysis, including time intervals, tag change statistics, and
/// optimization recommendations.
/// </summary>
/// <remarks>Use this class to encapsulate data collected during a performance monitoring session, such as the
/// frequency of tag changes and suggested improvements. The properties provide access to both raw metrics and
/// calculated values, enabling further reporting or decision-making based on the analysis.</remarks>
public class PerformanceAnalysis
{
    /// <summary>
    /// Gets or sets the start time of the analysis.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of the analysis.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets or sets the monitoring duration.
    /// </summary>
    public TimeSpan MonitoringDuration { get; set; }

    /// <summary>
    /// Gets or sets the tag change frequencies.
    /// </summary>
    public Dictionary<string, int> TagChangeFrequencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the total tag changes observed.
    /// </summary>
    public int TotalTagChanges { get; set; }

    /// <summary>
    /// Gets or sets the average changes per tag.
    /// </summary>
    public double AverageChangesPerTag { get; set; }

    /// <summary>
    /// Gets or sets the optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = [];
}
