// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>Hosts a Snap7-based mock PLC server for tests.</summary>
public class MockServer : IDisposable
{
    /// <summary>The loopback address used for local server binding.</summary>
    public const string Localhost = "127.0.0.1";

    /// <summary>The Snap7 code for the PE input area.</summary>
    public static readonly int SrvAreaPe;

    /// <summary>The Snap7 code for the PA output area.</summary>
    public static readonly int SrvAreaPa = 1;

    /// <summary>The Snap7 code for the MK memory area.</summary>
    public static readonly int SrvAreaMk = 2;

    /// <summary>The Snap7 code for the CT counter area.</summary>
    public static readonly int SrvAreaCt = 3;

    /// <summary>The Snap7 code for the TM timer area.</summary>
    public static readonly int SrvAreaTm = 4;

    /// <summary>The Snap7 code for the DB area.</summary>
    public static readonly int SrvAreaDB = 5;

    /// <summary>Event mask for incoming PDU notifications.</summary>
    public static readonly uint EvcPdUincoming = 0x00010000;

    /// <summary>Event mask for data read notifications.</summary>
    public static readonly uint EvcDataRead = 0x00020000;

    /// <summary>Event mask for data write notifications.</summary>
    public static readonly uint EvcDataWrite = 0x00040000;

    /// <summary>Event mask for PDU negotiation notifications.</summary>
    public static readonly uint EvcNegotiatePdu = 0x00080000;

    /// <summary>Event mask for SZL read notifications.</summary>
    public static readonly uint EvcReadSzl = 0x00100000;

    /// <summary>Event mask for clock access notifications.</summary>
    public static readonly uint EvcClock = 0x00200000;

    /// <summary>Event mask for upload notifications.</summary>
    public static readonly uint EvcUpload = 0x00400000;

    /// <summary>Event mask for download notifications.</summary>
    public static readonly uint EvcDownload = 0x00800000;

    /// <summary>Event mask for directory access notifications.</summary>
    public static readonly uint EvcDirectory = 0x01000000;

    /// <summary>Event mask for security notifications.</summary>
    public static readonly uint EvcSecurity = 0x02000000;

    /// <summary>Event mask for control notifications.</summary>
    public static readonly uint EvcControl = 0x04000000;

    /// <summary>The Snap7 event-mask selector.</summary>
    private const int EventMaskKind = 0;

    /// <summary>The Snap7 log-mask selector.</summary>
    private const int LogMaskKind = 1;

    /// <summary>Holds the pinned handles for registered server areas.</summary>
    private readonly Dictionary<int, GCHandle> _areaHandles;

    /// <summary>Stores the default DB1 backing area.</summary>
    private byte[]? _defaultDb1;

    /// <summary>Stores the default PE backing area.</summary>
    private byte[]? _defaultPe;

    /// <summary>Stores the default PA backing area.</summary>
    private byte[]? _defaultPa;

    /// <summary>Stores the default MK backing area.</summary>
    private byte[]? _defaultMk;

    /// <summary>Stores the default CT backing area.</summary>
    private byte[]? _defaultCt;

    /// <summary>Stores the default TM backing area.</summary>
    private byte[]? _defaultTm;

    /// <summary>Holds the native Snap7 server handle.</summary>
    private nint _server;

    /// <summary>Tracks whether disposal has already run.</summary>
    private bool _disposedValue;

    /// <summary>Initializes a new instance of the <see cref="MockServer"/> class.</summary>
    public MockServer()
    {
        _server = NativeMethods.Srv_Create();
        _areaHandles = [];
    }

    /// <summary>Finalizes an instance of the <see cref="MockServer"/> class.</summary>
    ~MockServer()
    {
        Dispose(false);
    }

    /// <summary>Gets or sets the log mask.</summary>
    /// <value>
    /// The log mask.
    /// </value>
    public uint LogMask
    {
        get
        {
            var mask = default(uint);
            return NativeMethods.Srv_GetMask(_server, LogMaskKind, ref mask) == 0 ? mask : 0;
        }

        set => _ = NativeMethods.Srv_SetMask(_server, LogMaskKind, value);
    }

    /// <summary>Gets or sets the event mask.</summary>
    /// <value>
    /// The event mask.
    /// </value>
    public uint EventMask
    {
        get
        {
            var mask = default(uint);
            return NativeMethods.Srv_GetMask(_server, EventMaskKind, ref mask) == 0 ? mask : 0;
        }
        set => _ = NativeMethods.Srv_SetMask(_server, EventMaskKind, value);
    }

    /// <summary>Gets the default Data Block 1 backing store (byte-addressable).</summary>
    public byte[]? DefaultDb1 => _defaultDb1;

    /// <summary>Gets or sets the size (in bytes) of the default DB1 area registered on start.</summary>
    public int DefaultDb1Size { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default PE (Inputs) area registered on start.</summary>
    public int DefaultPeSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default PA (Outputs) area registered on start.</summary>
    public int DefaultPaSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default MK (Memory) area registered on start.</summary>
    public int DefaultMkSize { get; set; } = 4096;

    /// <summary>Gets or sets the size (in bytes) of the default CT (Counters) area registered on start.</summary>
    public int DefaultCtSize { get; set; } = 512;

    /// <summary>Gets or sets the size (in bytes) of the default TM (Timers) area registered on start.</summary>
    public int DefaultTmSize { get; set; } = 512;

    /// <summary>Gets or sets the virtual CPU status.</summary>
    public int CpuStatus
    {
        get
        {
            var cpuStatus = default(int);
            var serverStatus = default(int);
            var clientCount = default(int);

            return NativeMethods.Srv_GetStatus(_server, ref serverStatus, ref cpuStatus, ref clientCount) == 0 ? cpuStatus : -1;
        }

        set => _ = NativeMethods.Srv_SetCpuStatus(_server, value);
    }

    /// <summary>Gets the current server status.</summary>
    public int ServerStatus
    {
        get
        {
            var cpuStatus = default(int);
            var serverStatus = default(int);
            var clientCount = default(int);

            return NativeMethods.Srv_GetStatus(_server, ref serverStatus, ref cpuStatus, ref clientCount) == 0 ? serverStatus : -1;
        }
    }

    /// <summary>Gets the number of connected clients.</summary>
    public int ClientsCount
    {
        get
        {
            var serverStatus = default(int);
            var cpuStatus = default(int);
            var clientCount = default(int);

            return NativeMethods.Srv_GetStatus(_server, ref serverStatus, ref cpuStatus, ref clientCount) == 0 ? clientCount : -1;
        }
    }

    /// <summary>Converts an event to the Snap7 display text.</summary>
    /// <param name="event">The event.</param>
    /// <returns>The formatted event text.</returns>
    public static string EventText(ref USrvEvent @event) => NativeMethods.GetEventText(ref @event);

    /// <summary>Converts a native event timestamp to a <see cref="DateTime"/>.</summary>
    /// <param name="timeStamp">The native timestamp value.</param>
    /// <returns>The converted <see cref="DateTime"/>.</returns>
    public static DateTime EvtTimeToDateTime(nint timeStamp)
    {
        var unixStartEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return unixStartEpoch.AddSeconds(Convert.ToDouble(timeStamp));
    }

    /// <summary>Converts an error code to the Snap7 display text.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The formatted error text.</returns>
    public static string ErrorText(int error) => NativeMethods.GetErrorText(error);

    /// <summary>Starts the server on the specified address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The Snap7 result code.</returns>
    public int StartTo(string address)
    {
        EnsureDefaultAreasRegistered();
        return NativeMethods.Srv_StartTo(_server, address);
    }

    /// <summary>Starts the server using the default address configuration.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int Start()
    {
        EnsureDefaultAreasRegistered();
        return NativeMethods.Srv_Start(_server);
    }

    /// <summary>Stops the server.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int Stop() => NativeMethods.Srv_Stop(_server);

    /// <summary>Registers a structured backing store with the server.</summary>
    /// <typeparam name="T">The backing store type.</typeparam>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <param name="userData">The pinned backing store.</param>
    /// <param name="size">The size.</param>
    /// <returns>The Snap7 result code.</returns>
    public int RegisterArea<T>(int areaCode, int index, ref T userData, int size)
    {
        if (typeof(T) == typeof(byte))
        {
            throw new ArgumentException("Use RegisterArea(int areaCode, int index, byte[] userData, int size) for byte areas.", nameof(userData));
        }

        var areaUid = (areaCode << 16) + index;
        var handle = GCHandle.Alloc(userData, GCHandleType.Pinned);
        var result = NativeMethods.Srv_RegisterArea(_server, areaCode, index, handle.AddrOfPinnedObject(), size);
        if (result == 0)
        {
            _areaHandles.Add(areaUid, handle);
        }
        else
        {
            handle.Free();
        }

        return result;
    }

    /// <summary>Registers the area using a byte-array backing store.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <param name="userData">The area backing buffer.</param>
    /// <param name="size">The size.</param>
    /// <returns>The Snap7 result code.</returns>
    public int RegisterArea(int areaCode, int index, byte[] userData, int size)
    {
        if (userData is null)
        {
            throw new ArgumentNullException(nameof(userData));
        }

        var areaUid = (areaCode << 16) + index;
        var handle = GCHandle.Alloc(userData, GCHandleType.Pinned);
        var result = NativeMethods.Srv_RegisterArea(_server, areaCode, index, handle.AddrOfPinnedObject(), size);
        if (result == 0)
        {
            _areaHandles.Add(areaUid, handle);
        }
        else
        {
            handle.Free();
        }

        return result;
    }

    /// <summary>Unregisters an area from the server.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int UnregisterArea(int areaCode, int index)
    {
        var result = NativeMethods.Srv_UnregisterArea(_server, areaCode, index);
        if (result == 0)
        {
            var areaUid = (areaCode << 16) + index;
            if (_areaHandles.TryGetValue(areaUid, out var handle))
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }

                _ = _areaHandles.Remove(areaUid);
            }
        }

        return result;
    }

    /// <summary>Locks a registered area.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int LockArea(int areaCode, int index) => NativeMethods.Srv_LockArea(_server, areaCode, index);

    /// <summary>Unlocks a registered area.</summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>The Snap7 result code.</returns>
    public int UnlockArea(int areaCode, int index) => NativeMethods.Srv_UnlockArea(_server, areaCode, index);

    /// <summary>Sets the event callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetEventsCallBack(SrvCallback callback, nint usrPtr) => NativeMethods.Srv_SetEventsCallback(_server, callback, usrPtr);

    /// <summary>Sets the read-event callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetReadEventsCallBack(SrvCallback callback, nint usrPtr) => NativeMethods.Srv_SetReadEventsCallback(_server, callback, usrPtr);

    /// <summary>Sets the read/write area callback.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The user pointer.</param>
    /// <returns>The Snap7 result code.</returns>
    public int SetRwAreaCallBack(SrvRwAreaCallback callback, nint usrPtr) => NativeMethods.Srv_SetRWAreaCallback(_server, callback, usrPtr);

    /// <summary>Retrieves the next queued event.</summary>
    /// <param name="event">The event.</param>
    /// <returns><see langword="true"/> when an event was returned.</returns>
    public bool PickEvent(ref USrvEvent @event)
    {
        var evtReady = default(int);
        return NativeMethods.Srv_PickEvent(_server, ref @event, ref evtReady) != 0 ? false : evtReady != 0;
    }

    /// <summary>Clears the pending server events.</summary>
    /// <returns>The Snap7 result code.</returns>
    public int ClearEvents() => NativeMethods.Srv_ClearEvents(_server);

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            _ = ClearEvents();
            _ = Stop();
        }

        foreach (var item in _areaHandles)
        {
            var handle = item.Value;
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }

        _ = NativeMethods.Srv_Destroy(ref _server);

        _disposedValue = true;
    }

    /// <summary>Creates the default areas required for standard PLC address access.</summary>
    private void EnsureDefaultAreasRegistered()
    {
        if (_defaultDb1 is not null)
        {
            return;
        }

        static int NormalizeSize(int size) => size < 1 ? 1 : size;

        DefaultDb1Size = NormalizeSize(DefaultDb1Size);
        DefaultPeSize = NormalizeSize(DefaultPeSize);
        DefaultPaSize = NormalizeSize(DefaultPaSize);
        DefaultMkSize = NormalizeSize(DefaultMkSize);
        DefaultCtSize = NormalizeSize(DefaultCtSize);
        DefaultTmSize = NormalizeSize(DefaultTmSize);

        _defaultDb1 = new byte[DefaultDb1Size];
        _defaultPe = new byte[DefaultPeSize];
        _defaultPa = new byte[DefaultPaSize];
        _defaultMk = new byte[DefaultMkSize];
        _defaultCt = new byte[DefaultCtSize];
        _defaultTm = new byte[DefaultTmSize];

        // Register DB1 so Snap7 can service ReadVar/WriteVar (including multi-item) against a real backing store.
        _ = RegisterArea(SrvAreaDB, 1, _defaultDb1, _defaultDb1.Length);

        // Register the standard non-DB areas so IB/QB/MB and bit addressing can be used in tests.
        _ = RegisterArea(SrvAreaPe, 0, _defaultPe, _defaultPe.Length);
        _ = RegisterArea(SrvAreaPa, 0, _defaultPa, _defaultPa.Length);
        _ = RegisterArea(SrvAreaMk, 0, _defaultMk, _defaultMk.Length);
        _ = RegisterArea(SrvAreaCt, 0, _defaultCt, _defaultCt.Length);
        _ = RegisterArea(SrvAreaTm, 0, _defaultTm, _defaultTm.Length);
    }
}
