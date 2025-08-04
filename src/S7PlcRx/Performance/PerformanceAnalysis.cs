// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Performance analysis results and optimization recommendations.
/// </summary>
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
    public Dictionary<string, int> TagChangeFrequencies { get; set; } = new Dictionary<string, int>();

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
    public List<string> Recommendations { get; set; } = new List<string>();
}
