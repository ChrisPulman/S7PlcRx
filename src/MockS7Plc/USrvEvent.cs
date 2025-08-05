// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>
/// USrvEvent.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct USrvEvent
{
    /// <summary>
    /// The evt time.
    /// </summary>
    public nint EvtTime;   // It's platform dependent (32 or 64 bit)

    /// <summary>
    /// The evt sender.
    /// </summary>
    public int EvtSender;

    /// <summary>
    /// The evt code.
    /// </summary>
    public uint EvtCode;

    /// <summary>
    /// The evt ret code.
    /// </summary>
    public ushort EvtRetCode;

    /// <summary>
    /// The evt param1.
    /// </summary>
    public ushort EvtParam1;

    /// <summary>
    /// The evt param2.
    /// </summary>
    public ushort EvtParam2;

    /// <summary>
    /// The evt param3.
    /// </summary>
    public ushort EvtParam3;

    /// <summary>
    /// The evt param4.
    /// </summary>
    public ushort EvtParam4;
}
