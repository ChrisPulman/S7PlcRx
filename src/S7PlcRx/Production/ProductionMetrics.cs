// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Represents a set of metrics related to the monitoring and connectivity status of a PLC (Programmable Logic
/// Controller) over a specified period.
/// </summary>
/// <remarks>This class is typically used to capture and report operational statistics for a PLC, such as
/// connection times, uptime percentage, and tag counts. All properties are mutable, allowing for incremental updates as
/// new data is collected.</remarks>
public sealed class ProductionMetrics
{
    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets when monitoring started.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the last update time.</summary>
    public DateTime LastUpdateTime { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets whether the PLC is currently connected.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Gets or sets the total connected time.</summary>
    public TimeSpan ConnectedTime { get; set; }

    /// <summary>Gets or sets the total disconnected time.</summary>
    public TimeSpan DisconnectedTime { get; set; }

    /// <summary>Gets or sets the uptime percentage.</summary>
    public double UptimePercentage { get; set; }

    /// <summary>Gets or sets the number of active tags.</summary>
    public int ActiveTagCount { get; set; }

    /// <summary>Gets or sets the total number of tags.</summary>
    public int TotalTagCount { get; set; }
}
