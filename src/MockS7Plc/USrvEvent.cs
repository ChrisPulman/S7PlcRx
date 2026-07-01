// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>Represents a Snap7 server event payload.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct USrvEvent
{
    /// <summary>Initializes a new instance of the <see cref="USrvEvent"/> struct.</summary>
    /// <param name="evtTime">The native event timestamp value.</param>
    /// <param name="evtSender">The event sender identifier.</param>
    /// <param name="evtCode">The event code.</param>
    /// <param name="evtRetCode">The event return code.</param>
    /// <param name="evtParam1">The first event parameter.</param>
    /// <param name="evtParam2">The second event parameter.</param>
    /// <param name="evtParam3">The third event parameter.</param>
    /// <param name="evtParam4">The fourth event parameter.</param>
    public USrvEvent(nint evtTime, int evtSender, uint evtCode, ushort evtRetCode, ushort evtParam1, ushort evtParam2, ushort evtParam3, ushort evtParam4)
    {
        EvtTime = evtTime;
        EvtSender = evtSender;
        EvtCode = evtCode;
        EvtRetCode = evtRetCode;
        EvtParam1 = evtParam1;
        EvtParam2 = evtParam2;
        EvtParam3 = evtParam3;
        EvtParam4 = evtParam4;
    }

    /// <summary>Gets the native event timestamp value.</summary>
    public nint EvtTime { get; }

    /// <summary>Gets the event sender identifier.</summary>
    public int EvtSender { get; }

    /// <summary>Gets the event code.</summary>
    public uint EvtCode { get; }

    /// <summary>Gets the event return code.</summary>
    public ushort EvtRetCode { get; }

    /// <summary>Gets the first event parameter.</summary>
    public ushort EvtParam1 { get; }

    /// <summary>Gets the second event parameter.</summary>
    public ushort EvtParam2 { get; }

    /// <summary>Gets the third event parameter.</summary>
    public ushort EvtParam3 { get; }

    /// <summary>Gets the fourth event parameter.</summary>
    public ushort EvtParam4 { get; }
}
