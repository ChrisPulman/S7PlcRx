// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace MockS7Plc;

/// <summary>
/// S7Server.
/// </summary>
public class MockServer : IDisposable
{
    /// <summary>
    /// The localhost.
    /// </summary>
    public const string Localhost = "127.0.0.1";
    /// <summary>
    /// The SRV area pe.
    /// </summary>
    public static readonly int SrvAreaPe;
    /// <summary>
    /// The SRV area pa.
    /// </summary>
    public static readonly int SrvAreaPa = 1;
    /// <summary>
    /// The SRV area mk.
    /// </summary>
    public static readonly int SrvAreaMk = 2;
    /// <summary>
    /// The SRV area ct.
    /// </summary>
    public static readonly int SrvAreaCt = 3;
    /// <summary>
    /// The SRV area tm.
    /// </summary>
    public static readonly int SrvAreaTm = 4;
    /// <summary>
    /// The SRV area database.
    /// </summary>
    public static readonly int SrvAreaDB = 5;

    /// <summary>
    /// The evc pd uincoming.
    /// </summary>
    public static readonly uint EvcPdUincoming = 0x00010000;
    /// <summary>
    /// The evc data read.
    /// </summary>
    public static readonly uint EvcDataRead = 0x00020000;
    /// <summary>
    /// The evc data write.
    /// </summary>
    public static readonly uint EvcDataWrite = 0x00040000;
    /// <summary>
    /// The evc negotiate pdu.
    /// </summary>
    public static readonly uint EvcNegotiatePdu = 0x00080000;
    /// <summary>
    /// The evc read SZL.
    /// </summary>
    public static readonly uint EvcReadSzl = 0x00100000;
    /// <summary>
    /// The evc clock.
    /// </summary>
    public static readonly uint EvcClock = 0x00200000;
    /// <summary>
    /// The evc upload.
    /// </summary>
    public static readonly uint EvcUpload = 0x00400000;
    /// <summary>
    /// The evc download.
    /// </summary>
    public static readonly uint EvcDownload = 0x00800000;
    /// <summary>
    /// The evc directory.
    /// </summary>
    public static readonly uint EvcDirectory = 0x01000000;
    /// <summary>
    /// The evc security.
    /// </summary>
    public static readonly uint EvcSecurity = 0x02000000;
    /// <summary>
    /// The evc control.
    /// </summary>
    public static readonly uint EvcControl = 0x04000000;

    private const int MsgTextLen = 1024;
    private const int MkEvent = 0;
    private const int MkLog = 1;

    private readonly Dictionary<int, GCHandle> _hArea;
    private byte[]? _defaultDb1;
    private byte[]? _defaultPe;
    private byte[]? _defaultPa;
    private byte[]? _defaultMk;
    private byte[]? _defaultCt;
    private byte[]? _defaultTm;
    private nint _server;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockServer"/> class.
    /// </summary>
    public MockServer()
    {
        _server = NativeMethods.Srv_Create();
        _hArea = [];
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="MockServer"/> class.
    /// </summary>
    ~MockServer()
    {
        Dispose(false);
    }

    /// <summary>
    /// Gets or sets the log mask.
    /// </summary>
    /// <value>
    /// The log mask.
    /// </value>
    public uint LogMask
    {
        get
        {
            var mask = default(uint);
            if (NativeMethods.Srv_GetMask(_server, MkLog, ref mask) == 0)
            {
                return mask;
            }

            return 0;
        }

        set => _ = NativeMethods.Srv_SetMask(_server, MkLog, value);
    }

    /// <summary>
    /// Gets or sets the event mask.
    /// </summary>
    /// <value>
    /// The event mask.
    /// </value>
    public uint EventMask
    {
        get
        {
            var mask = default(uint);
            if (NativeMethods.Srv_GetMask(_server, MkEvent, ref mask) == 0)
            {
                return mask;
            }

            return 0;
        }
        set => _ = NativeMethods.Srv_SetMask(_server, MkEvent, value);
    }

    /// <summary>
    /// Gets the default Data Block 1 backing store (byte-addressable).
    /// </summary>
    public byte[]? DefaultDb1 => _defaultDb1;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default DB1 area registered on start.
    /// </summary>
    public int DefaultDb1Size { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default PE (Inputs) area registered on start.
    /// </summary>
    public int DefaultPeSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default PA (Outputs) area registered on start.
    /// </summary>
    public int DefaultPaSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default MK (Memory) area registered on start.
    /// </summary>
    public int DefaultMkSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default CT (Counters) area registered on start.
    /// </summary>
    public int DefaultCtSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the size (in bytes) of the default TM (Timers) area registered on start.
    /// </summary>
    public int DefaultTmSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the cpu status.
    /// </summary>
    /// <value>
    /// The cpu status.
    /// </value>
    public int CpuStatus
    {
        // Property Virtual CPU status R/W
        get
        {
            var cStatus = default(int);
            var sStatus = default(int);
            var cCount = default(int);

            if (NativeMethods.Srv_GetStatus(_server, ref sStatus, ref cStatus, ref cCount) == 0)
            {
                return cStatus;
            }

            return -1;
        }
        set => _ = NativeMethods.Srv_SetCpuStatus(_server, value);
    }

    /// <summary>
    /// Gets the server status.
    /// </summary>
    /// <value>
    /// The server status.
    /// </value>
    public int ServerStatus
    {
        // Property Server Status Read Only
        get
        {
            var cStatus = default(int);
            var sStatus = default(int);
            var cCount = default(int);
            if (NativeMethods.Srv_GetStatus(_server, ref sStatus, ref cStatus, ref cCount) == 0)
            {
                return sStatus;
            }

            return -1;
        }
    }

    /// <summary>
    /// Gets the clients count.
    /// </summary>
    /// <value>
    /// The clients count.
    /// </value>
    public int ClientsCount
    {
        // Property Clients Count Read Only
        get
        {
            var cStatus = default(int);
            var sStatus = default(int);
            var cCount = default(int);
            if (NativeMethods.Srv_GetStatus(_server, ref cStatus, ref sStatus, ref cCount) == 0)
            {
                return cCount;
            }

            return -1;
        }
    }

    /// <summary>
    /// Events the text.
    /// </summary>
    /// <param name="event">The event.</param>
    /// <returns>Result.</returns>
    public static string EventText(ref USrvEvent @event)
    {
        var message = new StringBuilder(MsgTextLen);
        _ = NativeMethods.Srv_EventText(ref @event, message, MsgTextLen);
        return message.ToString();
    }

    /// <summary>
    /// Evts the time to date time.
    /// </summary>
    /// <param name="timeStamp">The time stamp.</param>
    /// <returns>Result.</returns>
    public static DateTime EvtTimeToDateTime(nint timeStamp)
    {
        var unixStartEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return unixStartEpoch.AddSeconds(Convert.ToDouble(timeStamp));
    }

    /// <summary>
    /// Errors the text.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>Result.</returns>
    public static string ErrorText(int error)
    {
        var message = new StringBuilder(MsgTextLen);
        _ = NativeMethods.Srv_ErrorText(error, message, MsgTextLen);
        return message.ToString();
    }

    /// <summary>
    /// Starts to.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>Result.</returns>
    public int StartTo(string address)
    {
        EnsureDefaultAreasRegistered();
        return NativeMethods.Srv_StartTo(_server, address);
    }

    /// <summary>
    /// Starts this instance.
    /// </summary>
    /// <returns>Result.</returns>
    public int Start()
    {
        EnsureDefaultAreasRegistered();
        return NativeMethods.Srv_Start(_server);
    }

    /// <summary>
    /// Stops this instance.
    /// </summary>
    /// <returns>Result.</returns>
    public int Stop() => NativeMethods.Srv_Stop(_server);

    /// <summary>
    /// Registers the area.
    /// </summary>
    /// <typeparam name="T">The Type.</typeparam>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <param name="pUsrData">The p usr data.</param>
    /// <param name="size">The size.</param>
    /// <returns>Result.</returns>
    public int RegisterArea<T>(int areaCode, int index, ref T pUsrData, int size)
    {
        var areaUid = (areaCode << 16) + index;
        var handle = GCHandle.Alloc(pUsrData, GCHandleType.Pinned);
        var result = NativeMethods.Srv_RegisterArea(_server, areaCode, index, handle.AddrOfPinnedObject(), size);
        if (result == 0)
        {
            _hArea.Add(areaUid, handle);
        }
        else
        {
            handle.Free();
        }

        return result;
    }

    /// <summary>
    /// Unregisters the area.
    /// </summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>Result.</returns>
    public int UnregisterArea(int areaCode, int index)
    {
        var result = NativeMethods.Srv_UnregisterArea(_server, areaCode, index);
        if (result == 0)
        {
            var areaUid = (areaCode << 16) + index;
            if (_hArea.TryGetValue(areaUid, out var handle))
            {
                // should be always true
                if (handle.IsAllocated)
                {
                    // Free the handle
                    handle.Free();
                }

                _ = _hArea.Remove(areaUid);
            }
        }

        return result;
    }

    /// <summary>
    /// Locks the area.
    /// </summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>Result.</returns>
    public int LockArea(int areaCode, int index) => NativeMethods.Srv_LockArea(_server, areaCode, index);

    /// <summary>
    /// Unlocks the area.
    /// </summary>
    /// <param name="areaCode">The area code.</param>
    /// <param name="index">The index.</param>
    /// <returns>Result.</returns>
    public int UnlockArea(int areaCode, int index) => NativeMethods.Srv_UnlockArea(_server, areaCode, index);

    /// <summary>
    /// Sets the events call back.
    /// </summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The usr PTR.</param>
    /// <returns>Result.</returns>
    public int SetEventsCallBack(SrvCallback callback, nint usrPtr) => NativeMethods.Srv_SetEventsCallback(_server, callback, usrPtr);

    /// <summary>
    /// Sets the read events call back.
    /// </summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The usr PTR.</param>
    /// <returns>Result.</returns>
    public int SetReadEventsCallBack(SrvCallback callback, nint usrPtr) => NativeMethods.Srv_SetReadEventsCallback(_server, callback, usrPtr);

    /// <summary>
    /// Sets the rw area call back.
    /// </summary>
    /// <param name="callback">The callback.</param>
    /// <param name="usrPtr">The usr PTR.</param>
    /// <returns>Result.</returns>
    public int SetRwAreaCallBack(SrvRwAreaCallback callback, nint usrPtr) => NativeMethods.Srv_SetRWAreaCallback(_server, callback, usrPtr);

    /// <summary>
    /// Picks the event.
    /// </summary>
    /// <param name="event">The event.</param>
    /// <returns>Result.</returns>
    public bool PickEvent(ref USrvEvent @event)
    {
        var evtReady = default(int);
        if (NativeMethods.Srv_PickEvent(_server, ref @event, ref evtReady) == 0)
        {
            return evtReady != 0;
        }

        return false;
    }

    /// <summary>
    /// Clears the events.
    /// </summary>
    /// <returns>Result.</returns>
    public int ClearEvents() => NativeMethods.Srv_ClearEvents(_server);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                ClearEvents();
                Stop();
            }

            foreach (var item in _hArea)
            {
                var handle = item.Value;
                if (handle.IsAllocated)
                {
                    // Free the handle
                    handle.Free();
                }
            }

            _ = NativeMethods.Srv_Destroy(ref _server);

            _disposedValue = true;
        }
    }

    private void EnsureDefaultAreasRegistered()
    {
        if (_defaultDb1 != null)
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
        _ = RegisterArea(SrvAreaDB, 1, ref _defaultDb1[0], _defaultDb1.Length);

        // Register the standard non-DB areas so IB/QB/MB and bit addressing can be used in tests.
        _ = RegisterArea(SrvAreaPe, 0, ref _defaultPe[0], _defaultPe.Length);
        _ = RegisterArea(SrvAreaPa, 0, ref _defaultPa[0], _defaultPa.Length);
        _ = RegisterArea(SrvAreaMk, 0, ref _defaultMk[0], _defaultMk.Length);
        _ = RegisterArea(SrvAreaCt, 0, ref _defaultCt[0], _defaultCt.Length);
        _ = RegisterArea(SrvAreaTm, 0, ref _defaultTm[0], _defaultTm.Length);
    }
}
