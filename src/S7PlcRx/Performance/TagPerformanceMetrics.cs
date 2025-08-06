// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Performance metrics for tag operations.
/// </summary>
public class TagPerformanceMetrics
{
    /// <summary>Gets or sets the tag name.</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of read operations.</summary>
    public long ReadOperations { get; set; }

    /// <summary>Gets or sets the total number of write operations.</summary>
    public long WriteOperations { get; set; }

    /// <summary>Gets or sets the average read time in milliseconds.</summary>
    public double AverageReadTimeMs { get; set; }

    /// <summary>Gets or sets the average write time in milliseconds.</summary>
    public double AverageWriteTimeMs { get; set; }

    /// <summary>Gets or sets the number of failed operations.</summary>
    public long FailedOperations { get; set; }

    /// <summary>Gets or sets the success rate (0.0 to 1.0).</summary>
    public double SuccessRate { get; set; }

    /// <summary>Gets or sets the last operation timestamp.</summary>
    public DateTime LastOperationTime { get; set; }
}
