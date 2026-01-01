// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace MockS7Plc;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1202 // Elements should be ordered by access
internal static class NativeMethods
{
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

    private const string LibName = "snap7.dll";

    static NativeMethods()
    {
#if NET8_0_OR_GREATER
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, static (libraryName, _, __) =>
        {
            if (!string.Equals(libraryName, LibName, StringComparison.OrdinalIgnoreCase))
            {
                return nint.Zero;
            }

            var rid = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
            var candidate = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", LibName);
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }

            return nint.Zero;
        });
#elif NET48
        // .NET Framework does not support NativeLibrary resolver; ensure snap7.dll is loaded from our runtimes folder.
        var rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native", LibName);
        if (File.Exists(candidate))
        {
            _ = LoadLibrary(candidate);
        }
#endif
    }

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

#if NET48
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadLibrary(string lpFileName);
#endif

#pragma warning restore SYSLIB1054
}
#pragma warning restore SA1202
#pragma warning restore SA1201
