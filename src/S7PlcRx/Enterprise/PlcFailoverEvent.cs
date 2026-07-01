// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>Represents an event that occurs when a failover between programmable logic controllers (PLCs) takes place.</summary>
/// <remarks>This class encapsulates information about a PLC failover event, including the time of occurrence, the
/// reason for the failover, and the identifiers of the PLCs involved. Instances of this class are typically used for
/// logging, monitoring, or auditing failover activities within PLC-based systems.</remarks>
public sealed class PlcFailoverEvent
{
    /// <summary>Gets or sets the timestamp of the failover.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the reason for failover.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the old PLC identifier.</summary>
    public string OldPlc { get; set; } = string.Empty;

    /// <summary>Gets or sets the new PLC identifier.</summary>
    public string NewPlc { get; set; } = string.Empty;
}
