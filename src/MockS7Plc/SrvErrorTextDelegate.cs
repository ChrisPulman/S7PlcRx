// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>Represents the native <c>Srv_ErrorText</c> export.</summary>
/// <param name="error">The server error code.</param>
/// <param name="messageBuffer">The destination message buffer.</param>
/// <param name="textSize">The destination buffer size.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvErrorTextDelegate(int error, [Out] byte[] messageBuffer, int textSize);

/// <summary>Represents the native <c>Srv_Create</c> export.</summary>
/// <returns>The native server handle.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate nint SrvCreateDelegate();

/// <summary>Represents the native <c>Srv_ClearEvents</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvClearEventsDelegate(nint server);

/// <summary>Represents the native <c>Srv_Destroy</c> export.</summary>
/// <param name="server">The native server handle reference.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvDestroyDelegate(ref nint server);

/// <summary>Represents the native <c>Srv_EventText</c> export.</summary>
/// <param name="event">The event to format.</param>
/// <param name="messageBuffer">The destination message buffer.</param>
/// <param name="textSize">The destination buffer size.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvEventTextDelegate(ref USrvEvent @event, [Out] byte[] messageBuffer, int textSize);

/// <summary>Represents the native <c>Srv_GetMask</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="maskKind">The mask category.</param>
/// <param name="mask">Receives the mask value.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvGetMaskDelegate(nint server, int maskKind, ref uint mask);

/// <summary>Represents the native <c>Srv_SetMask</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="maskKind">The mask category.</param>
/// <param name="mask">The mask value.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvSetMaskDelegate(nint server, int maskKind, uint mask);

/// <summary>Represents the native <c>Srv_GetStatus</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="serverStatus">Receives the server status.</param>
/// <param name="cpuStatus">Receives the CPU status.</param>
/// <param name="clientsCount">Receives the connected client count.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvGetStatusDelegate(nint server, ref int serverStatus, ref int cpuStatus, ref int clientsCount);

/// <summary>Represents the native <c>Srv_SetCpuStatus</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="cpuStatus">The CPU status value.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvSetCpuStatusDelegate(nint server, int cpuStatus);

/// <summary>Represents the native <c>Srv_PickEvent</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="event">Receives the event data.</param>
/// <param name="eventReady">Receives whether an event was available.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvPickEventDelegate(nint server, ref USrvEvent @event, ref int eventReady);

/// <summary>Represents the native <c>Srv_SetRWAreaCallback</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="callback">The registered callback.</param>
/// <param name="userDataPointer">The user data pointer.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvSetRwAreaCallbackDelegate(nint server, SrvRwAreaCallback callback, nint userDataPointer);

/// <summary>Represents the native <c>Srv_StartTo</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="address">The ASCII address bytes.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvStartToDelegate(nint server, [In] byte[] address);

/// <summary>Represents the native <c>Srv_LockArea</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="areaCode">The Snap7 area code.</param>
/// <param name="index">The area index.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvLockAreaDelegate(nint server, int areaCode, int index);

/// <summary>Represents the native <c>Srv_UnlockArea</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="areaCode">The Snap7 area code.</param>
/// <param name="index">The area index.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvUnlockAreaDelegate(nint server, int areaCode, int index);

/// <summary>Represents the native <c>Srv_RegisterArea</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="areaCode">The Snap7 area code.</param>
/// <param name="index">The area index.</param>
/// <param name="userDataPointer">The pinned area pointer.</param>
/// <param name="size">The area size in bytes.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvRegisterAreaDelegate(nint server, int areaCode, int index, nint userDataPointer, int size);

/// <summary>Represents the native <c>Srv_Start</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvStartDelegate(nint server);

/// <summary>Represents the native <c>Srv_Stop</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvStopDelegate(nint server);

/// <summary>Represents the native <c>Srv_UnregisterArea</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="areaCode">The Snap7 area code.</param>
/// <param name="index">The area index.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvUnregisterAreaDelegate(nint server, int areaCode, int index);

/// <summary>Represents the native <c>Srv_SetEventsCallback</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="callback">The registered callback.</param>
/// <param name="userDataPointer">The user data pointer.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvSetEventsCallbackDelegate(nint server, SrvCallback callback, nint userDataPointer);

/// <summary>Represents the native <c>Srv_SetReadEventsCallback</c> export.</summary>
/// <param name="server">The native server handle.</param>
/// <param name="callback">The registered callback.</param>
/// <param name="userDataPointer">The user data pointer.</param>
/// <returns>The Snap7 result code.</returns>
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate int SrvSetReadEventsCallbackDelegate(nint server, SrvCallback callback, nint userDataPointer);
