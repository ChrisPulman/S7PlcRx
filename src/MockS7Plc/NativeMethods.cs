// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace MockS7Plc;

internal static class NativeMethods
{
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

    private const string LibName = "snap7.dll";

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
    [DllImport(LibName, CharSet = CharSet.Ansi)]
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
    internal static extern int Srv_ErrorText(int error, StringBuilder errMsg, int textSize);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes

    [DllImport(LibName)]
    internal static extern nint Srv_Create();

    [DllImport(LibName)]
    internal static extern int Srv_ClearEvents(nint server);

    [DllImport(LibName)]
    internal static extern int Srv_Destroy(ref nint server);

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
    [DllImport(LibName, CharSet = CharSet.Ansi)]
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
    internal static extern int Srv_EventText(ref USrvEvent @event, StringBuilder evtMsg, int textSize);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes

    [DllImport(LibName)]
    internal static extern int Srv_GetMask(nint server, int maskKind, ref uint mask);

    [DllImport(LibName)]
    internal static extern int Srv_SetMask(nint server, int maskKind, uint mask);

    [DllImport(LibName)]
    internal static extern int Srv_GetStatus(nint server, ref int serverStatus, ref int cpuStatus, ref int clientsCount);

    [DllImport(LibName)]
    internal static extern int Srv_SetCpuStatus(nint server, int cpuStatus);

    [DllImport(LibName)]
    internal static extern int Srv_PickEvent(nint server, ref USrvEvent @event, ref int evtReady);

    [DllImport(LibName)]
    internal static extern int Srv_SetRWAreaCallback(nint server, SrvRwAreaCallback callback, nint usrPtr);

    [DllImport(LibName)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
    internal static extern int Srv_StartTo(nint server, [MarshalAs(UnmanagedType.LPStr)] string address);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments

    [DllImport(LibName)]
    internal static extern int Srv_LockArea(nint server, int areaCode, int index);

    [DllImport(LibName)]
    internal static extern int Srv_UnlockArea(nint server, int areaCode, int index);

    [DllImport(LibName)]
    internal static extern int Srv_RegisterArea(nint server, int areaCode, int index, nint pUsrData, int size);

    [DllImport(LibName)]
    internal static extern int Srv_Start(nint server);

    [DllImport(LibName)]
    internal static extern int Srv_Stop(nint server);

    [DllImport(LibName)]
    internal static extern int Srv_UnregisterArea(nint server, int areaCode, int index);

    [DllImport(LibName)]
    internal static extern int Srv_SetEventsCallback(nint server, SrvCallback callback, nint usrPtr);

    [DllImport(LibName)]
    internal static extern int Srv_SetReadEventsCallback(nint server, SrvCallback callback, nint usrPtr);
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
}
