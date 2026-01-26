// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.Production;

/// <summary>
/// Represents diagnostic information collected from a production programmable logic controller (PLC) connection,
/// including connection details, performance metrics, and recommendations.
/// </summary>
/// <remarks>This class is typically used to capture and analyze the state of a PLC connection and its associated
/// metrics at a specific point in time. It aggregates connection parameters, diagnostic results, and any errors or
/// optimization suggestions identified during the diagnostic process. All properties are intended to be set and read by
/// consumers managing or monitoring PLC diagnostics.</remarks>
public class ProductionDiagnostics
{
    /// <summary>Gets or sets the PLC type.</summary>
    public CpuType PLCType { get; set; }

    /// <summary>Gets or sets the IP address.</summary>
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the rack number.</summary>
    public short Rack { get; set; }

    /// <summary>Gets or sets the slot number.</summary>
    public short Slot { get; set; }

    /// <summary>Gets or sets a value indicating whether gets or sets the connection status.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Gets or sets when diagnostics were collected.</summary>
    public DateTime DiagnosticTime { get; set; }

    /// <summary>Gets or sets the connection latency in milliseconds.</summary>
    public double ConnectionLatencyMs { get; set; }

    /// <summary>Gets or sets the CPU information.</summary>
    public string[] CPUInformation { get; set; } = [];

    /// <summary>Gets or sets the tag metrics.</summary>
    public ProductionTagMetrics TagMetrics { get; set; } = new ProductionTagMetrics();

    /// <summary>Gets or sets the optimization recommendations.</summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>Gets or sets any errors encountered during diagnostics.</summary>
    public List<string> Errors { get; set; } = [];
}
