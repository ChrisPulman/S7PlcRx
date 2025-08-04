// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enterprise;

/// <summary>
/// PLC failover event information.
/// </summary>
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
