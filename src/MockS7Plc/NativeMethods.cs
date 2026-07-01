// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MockS7Plc;

/// <summary>Provides the Snap7 native interop used by <see cref="MockServer"/>.</summary>
internal static class NativeMethods
{
    /// <summary>The Snap7 server library name.</summary>
    private const string LibName = "snap7.dll";

    /// <summary>Stores the loaded Snap7 module handle.</summary>
    private static readonly nint LibraryHandle;

    /// <summary>Caches resolved native exports.</summary>
    private static readonly Dictionary<string, Delegate> ExportCache = new(StringComparer.Ordinal);

    /// <summary>Guards export resolution.</summary>
#if NET9_0_OR_GREATER
    private static readonly Lock ExportSyncRoot = new();
#else
    private static readonly object ExportSyncRoot = new();
#endif

    /// <summary>Initializes the native library handle cache.</summary>
    static NativeMethods()
    {
#if NET8_0_OR_GREATER
        var rid = RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
        var candidate = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", LibName);
        _ = NativeLibrary.TryLoad(candidate, out var handle) || NativeLibrary.TryLoad(LibName, out handle);
        LibraryHandle = handle;
#elif NET48
        var rid = Environment.Is64BitProcess ? "win-x64" : "win-x86";
        var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", rid, "native", LibName);
        LibraryHandle = File.Exists(candidate) ? LoadLibrary(candidate) : LoadLibrary(LibName);
#endif
    }

    /// <summary>Formats a Snap7 server error code.</summary>
    /// <param name="error">The server error code.</param>
    /// <returns>The formatted error text.</returns>
    internal static string GetErrorText(int error)
    {
        var buffer = new byte[1024];
        _ = GetExport<SrvErrorTextDelegate>("Srv_ErrorText")(error, buffer, buffer.Length);
        return GetNullTerminatedString(buffer);
    }

    /// <summary>Formats a Snap7 server event.</summary>
    /// <param name="event">The event to format.</param>
    /// <returns>The formatted event text.</returns>
    internal static string GetEventText(ref USrvEvent @event)
    {
        var buffer = new byte[1024];
        _ = GetExport<SrvEventTextDelegate>("Srv_EventText")(ref @event, buffer, buffer.Length);
        return GetNullTerminatedString(buffer);
    }

    /// <summary>Creates a Snap7 server instance.</summary>
    /// <returns>The native server handle.</returns>
    internal static nint Srv_Create() => GetExport<SrvCreateDelegate>(nameof(Srv_Create))();

    /// <summary>Clears the pending server events.</summary>
    /// <param name="server">The native server handle.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_ClearEvents(nint server) => GetExport<SrvClearEventsDelegate>(nameof(Srv_ClearEvents))(server);

    /// <summary>Destroys a Snap7 server instance.</summary>
    /// <param name="server">The native server handle reference.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_Destroy(ref nint server) => GetExport<SrvDestroyDelegate>(nameof(Srv_Destroy))(ref server);

    /// <summary>Gets a server mask value.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="maskKind">The mask category.</param>
    /// <param name="mask">Receives the mask value.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_GetMask(nint server, int maskKind, ref uint mask) => GetExport<SrvGetMaskDelegate>(nameof(Srv_GetMask))(server, maskKind, ref mask);

    /// <summary>Sets a server mask value.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="maskKind">The mask category.</param>
    /// <param name="mask">The mask value.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_SetMask(nint server, int maskKind, uint mask) => GetExport<SrvSetMaskDelegate>(nameof(Srv_SetMask))(server, maskKind, mask);

    /// <summary>Gets the current server status.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="serverStatus">Receives the server status.</param>
    /// <param name="cpuStatus">Receives the CPU status.</param>
    /// <param name="clientsCount">Receives the connected client count.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_GetStatus(nint server, ref int serverStatus, ref int cpuStatus, ref int clientsCount) => GetExport<SrvGetStatusDelegate>(nameof(Srv_GetStatus))(server, ref serverStatus, ref cpuStatus, ref clientsCount);

    /// <summary>Sets the virtual CPU status.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="cpuStatus">The CPU status value.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_SetCpuStatus(nint server, int cpuStatus) => GetExport<SrvSetCpuStatusDelegate>(nameof(Srv_SetCpuStatus))(server, cpuStatus);

    /// <summary>Reads the next queued server event.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="event">Receives the event data.</param>
    /// <param name="eventReady">Receives whether an event was available.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_PickEvent(nint server, ref USrvEvent @event, ref int eventReady) => GetExport<SrvPickEventDelegate>(nameof(Srv_PickEvent))(server, ref @event, ref eventReady);

    /// <summary>Sets the read/write area callback.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="callback">The callback delegate.</param>
    /// <param name="userDataPointer">The user data pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_SetRWAreaCallback(nint server, SrvRwAreaCallback callback, nint userDataPointer) => GetExport<SrvSetRwAreaCallbackDelegate>(nameof(Srv_SetRWAreaCallback))(server, callback, userDataPointer);

    /// <summary>Starts the server on the specified address.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="address">The bind address.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_StartTo(nint server, string address)
    {
        if (address is null)
        {
            throw new ArgumentNullException(nameof(address));
        }

        var addressBytes = Encoding.ASCII.GetBytes(address + '\0');
        return GetExport<SrvStartToDelegate>(nameof(Srv_StartTo))(server, addressBytes);
    }

    /// <summary>Locks a registered area.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="areaCode">The Snap7 area code.</param>
    /// <param name="index">The area index.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_LockArea(nint server, int areaCode, int index) => GetExport<SrvLockAreaDelegate>(nameof(Srv_LockArea))(server, areaCode, index);

    /// <summary>Unlocks a registered area.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="areaCode">The Snap7 area code.</param>
    /// <param name="index">The area index.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_UnlockArea(nint server, int areaCode, int index) => GetExport<SrvUnlockAreaDelegate>(nameof(Srv_UnlockArea))(server, areaCode, index);

    /// <summary>Registers a server memory area.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="areaCode">The Snap7 area code.</param>
    /// <param name="index">The area index.</param>
    /// <param name="userDataPointer">The pinned area pointer.</param>
    /// <param name="size">The area size in bytes.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_RegisterArea(nint server, int areaCode, int index, nint userDataPointer, int size) => GetExport<SrvRegisterAreaDelegate>(nameof(Srv_RegisterArea))(server, areaCode, index, userDataPointer, size);

    /// <summary>Starts the server using the default address configuration.</summary>
    /// <param name="server">The native server handle.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_Start(nint server) => GetExport<SrvStartDelegate>(nameof(Srv_Start))(server);

    /// <summary>Stops the server.</summary>
    /// <param name="server">The native server handle.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_Stop(nint server) => GetExport<SrvStopDelegate>(nameof(Srv_Stop))(server);

    /// <summary>Unregisters a server memory area.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="areaCode">The Snap7 area code.</param>
    /// <param name="index">The area index.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_UnregisterArea(nint server, int areaCode, int index) => GetExport<SrvUnregisterAreaDelegate>(nameof(Srv_UnregisterArea))(server, areaCode, index);

    /// <summary>Sets the server events callback.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="callback">The callback delegate.</param>
    /// <param name="userDataPointer">The user data pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_SetEventsCallback(nint server, SrvCallback callback, nint userDataPointer) => GetExport<SrvSetEventsCallbackDelegate>(nameof(Srv_SetEventsCallback))(server, callback, userDataPointer);

    /// <summary>Sets the read-events callback.</summary>
    /// <param name="server">The native server handle.</param>
    /// <param name="callback">The callback delegate.</param>
    /// <param name="userDataPointer">The user data pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    internal static int Srv_SetReadEventsCallback(nint server, SrvCallback callback, nint userDataPointer) => GetExport<SrvSetReadEventsCallbackDelegate>(nameof(Srv_SetReadEventsCallback))(server, callback, userDataPointer);

    /// <summary>Converts a null-terminated native buffer into a managed string.</summary>
    /// <param name="buffer">The buffer to decode.</param>
    /// <returns>The decoded string.</returns>
    private static string GetNullTerminatedString(byte[] buffer)
    {
        var terminatorIndex = Array.IndexOf(buffer, (byte)0);
        if (terminatorIndex < 0)
        {
            terminatorIndex = buffer.Length;
        }

        return Encoding.ASCII.GetString(buffer, 0, terminatorIndex);
    }

    /// <summary>Resolves and caches a native export as a delegate.</summary>
    /// <typeparam name="T">The delegate type to create.</typeparam>
    /// <param name="exportName">The native export name.</param>
    /// <returns>The cached delegate instance.</returns>
    private static T GetExport<T>(string exportName)
        where T : Delegate
    {
        lock (ExportSyncRoot)
        {
            if (LibraryHandle == default)
            {
                throw new DllNotFoundException($"Unable to load '{LibName}'.");
            }

            if (ExportCache.TryGetValue(exportName, out var cachedDelegate))
            {
                return (T)cachedDelegate;
            }

#if NET8_0_OR_GREATER
            var exportPointer = NativeLibrary.GetExport(LibraryHandle, exportName);
#elif NET48
            var exportNameBytes = Encoding.ASCII.GetBytes(exportName + '\0');
            var exportPointer = GetProcAddress(LibraryHandle, exportNameBytes);
            if (exportPointer == default)
            {
                throw new EntryPointNotFoundException(exportName);
            }
#endif
            var resolvedDelegate = Marshal.GetDelegateForFunctionPointer<T>(exportPointer);
            ExportCache.Add(exportName, resolvedDelegate);
            return resolvedDelegate;
        }
    }

#if NET48
    /// <summary>Loads a native library from an explicit path on .NET Framework.</summary>
    /// <param name="fileName">The library file path.</param>
    /// <returns>The loaded module handle.</returns>
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string fileName);

    /// <summary>Gets the address of an exported symbol on .NET Framework.</summary>
    /// <param name="moduleHandle">The loaded module handle.</param>
    /// <param name="procedureName">The exported procedure name.</param>
    /// <returns>The symbol pointer, or zero when the symbol is missing.</returns>
    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetProcAddress(nint moduleHandle, [In] byte[] procedureName);
#endif
}
