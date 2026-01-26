// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.Enterprise;

/// <summary>
/// Represents the configuration settings required to establish a connection to a programmable logic controller (PLC).
/// </summary>
/// <remarks>Use this class to specify connection parameters such as PLC type, network address, rack, slot, and an
/// optional connection name when initializing or managing PLC connections. This configuration is typically used by PLC
/// communication libraries to open and maintain a session with the target device.</remarks>
public sealed class PlcConnectionConfig
{
    /// <summary>Gets or sets the PLC type.</summary>
    public CpuType PLCType { get; set; }

    /// <summary>Gets or sets the IP address.</summary>
    public string IPAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the rack number.</summary>
    public short Rack { get; set; }

    /// <summary>Gets or sets the slot number.</summary>
    public short Slot { get; set; }

    /// <summary>Gets or sets the connection name.</summary>
    public string ConnectionName { get; set; } = string.Empty;
}
