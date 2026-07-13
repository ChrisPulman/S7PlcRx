// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;

namespace S7PlcRx.Reactive;
#else
using S7PlcRx.Enums;

namespace S7PlcRx;
#endif

/// <summary>Describes the PLC endpoint used by an <see cref="RxS7"/> connection.</summary>
/// <param name="cpuType">The PLC CPU family.</param>
/// <param name="address">The PLC IP address.</param>
/// <param name="rack">The PLC rack number.</param>
/// <param name="slot">The PLC CPU slot number.</param>
public sealed class S7ConnectionOptions(CpuType cpuType, string address, short rack, short slot)
{
    /// <summary>Gets the PLC CPU family.</summary>
    public CpuType CpuType { get; } = cpuType;

    /// <summary>Gets the PLC IP address.</summary>
    public string IpAddress { get; } = address;

    /// <summary>Gets the PLC rack number.</summary>
    public short Rack { get; } = rack;

    /// <summary>Gets the PLC CPU slot number.</summary>
    public short Slot { get; } = slot;
}
