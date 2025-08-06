// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Performance metrics for PLC operations.
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp of these metrics.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets whether the PLC is connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Gets or sets the total number of tags.</summary>
    public int TagCount { get; set; }

    /// <summary>Gets or sets the number of active tags.</summary>
    public int ActiveTagCount { get; set; }

    /// <summary>Gets or sets the operations per second.</summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>Gets or sets the average response time in milliseconds.</summary>
    public double AverageResponseTime { get; set; }

    /// <summary>Gets or sets the error rate (0.0 to 1.0).</summary>
    public double ErrorRate { get; set; }

    /// <summary>Gets or sets the connection uptime.</summary>
    public TimeSpan ConnectionUptime { get; set; }

    /// <summary>Gets or sets the number of reconnections.</summary>
    public int ReconnectionCount { get; set; }
}
