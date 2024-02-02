// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
using DateTime = System.DateTime;
using TimeSpan = System.TimeSpan;

namespace S7PlcRx.Core;

/// <summary>
/// Siemens Socket Rx.
/// </summary>
internal class S7SocketRx : IDisposable
{
    private const string Failed = nameof(Failed);
    private const string Success = nameof(Success);
    private readonly Subject<Exception> _socketExceptionSubject = new();
    private IDisposable _disposable;
    private bool _disposedValue;
    private bool _initComplete;
    private bool? _isAvailable;
    private bool? _isConnected;
    private Socket? _socket;

    /// <summary>
    /// Initializes a new instance of the <see cref="S7SocketRx" /> class.
    /// </summary>
    /// <param name="ip">The ip.</param>
    /// <param name="plcType">Type of the PLC.</param>
    /// <param name="rack">The rack.</param>
    /// <param name="slot">The slot.</param>
    public S7SocketRx(string ip, CpuType plcType, short rack, short slot)
    {
        IP = ip;
        PLCType = plcType;
        Rack = rack;
        Slot = slot;
        _disposable = Connect.Subscribe();
    }

    /// <summary>
    /// Gets the connect.
    /// </summary>
    /// <value>The connect.</value>
    public IObservable<bool> Connect =>
                Observable.Create<bool>(obs =>
                {
                    var dis = new CompositeDisposable();
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    dis.Add(_socket);
                    _initComplete = false;

                    dis.Add(_socketExceptionSubject.Subscribe(ex =>
                    {
                        if (ex != null)
                        {
                            obs.OnError(ex);
                        }
                    }));
                    dis.Add(IsConnected.Subscribe(
                        deviceConnected =>
                    {
                        var isAvail = _isAvailable != null && _isAvailable.HasValue && _isAvailable.Value;
                        obs.OnNext(isAvail && deviceConnected);
                        if (_initComplete && !deviceConnected)
                        {
                            CloseSocket(_socket);
                            obs.OnError(new S7Exception("Device not connected"));
                        }
                    },
                        ex =>
                    {
                        CloseSocket(_socket);
                        obs.OnError(ex);
                    }));
                    dis.Add(IsAvailable.Subscribe(
                        async _ =>
                    {
                        try
                        {
                            if (_isAvailable != null)
                            {
                                var isAvail = _isAvailable != null && _isAvailable.HasValue && _isAvailable.Value;
                                if (isAvail)
                                {
                                    if (!_initComplete && !await InitialiseSiemensConnectionAsync())
                                    {
                                        CloseSocket(_socket);
                                        obs.OnError(new S7Exception("Device not connected"));
                                        return;
                                    }

                                    var isCon = _isConnected != null && _isConnected.HasValue && _isConnected.Value;
                                    if (_initComplete && !isCon)
                                    {
                                        CloseSocket(_socket);
                                        obs.OnError(new S7Exception("Device not connected"));
                                    }
                                }
                                else
                                {
                                    CloseSocket(_socket);

                                    obs.OnError(new S7Exception("Device Unavailable"));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            CloseSocket(_socket);
                            obs.OnError(ex);
                        }
                    },
                        ex =>
                    {
                        CloseSocket(_socket);
                        obs.OnError(ex);
                    }));

                    return dis;
                }).Retry().Publish(false).RefCount();

    /// <summary>
    /// Gets the ip.
    /// </summary>
    /// <value>The ip.</value>
    public string IP { get; }

    /// <summary>
    /// Gets the length of the data read.
    /// </summary>
    /// <value>
    /// The length of the data read.
    /// </value>
    public ushort DataReadLength { get; private set; } = 480;

    /// <summary>
    /// Gets the is available.
    /// </summary>
    /// <value>The is available.</value>
    public IObservable<bool> IsAvailable =>
        Observable.Create<bool>(obs =>
        {
            IDisposable tim;
            _isAvailable = null;
            var count = 0;
            tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(10)).Subscribe(_ =>
            {
                count++;
                if (_isAvailable == null || !_isAvailable.HasValue || (count == 1 && !_isAvailable.Value) || (count == 10 && _isAvailable.Value))
                {
                    count = 0;
                    using (var ping = new Ping())
                    {
                        if (string.IsNullOrWhiteSpace(IP))
                        {
                            _isAvailable = false;
                            obs.OnError(new ArgumentNullException(nameof(IP)));
                        }
                        else
                        {
                            try
                            {
                                var result = ping.Send(IP);
                                _isAvailable = result?.Status == IPStatus.Success;
                            }
                            catch (PingException)
                            {
                                _isAvailable = false;
                            }
                        }
                    }
                }

                obs.OnNext(_isAvailable == true);
            });
            return new SingleAssignmentDisposable { Disposable = tim };
        }).Retry().Publish(false).RefCount();

    /// <summary>
    /// Gets a value indicating whether this instance is connected.
    /// </summary>
    /// <value><c>true</c> if this instance is connected; otherwise, <c>false</c>.</value>
    public IObservable<bool> IsConnected =>
        Observable.Create<bool>(obs =>
        {
            _isConnected = null;
            var tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(10)).Subscribe(_ =>
            {
                if (_socket == null)
                {
                    _isConnected = false;
                }
                else
                {
                    try
                    {
                        _isConnected = _socket.Connected || (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);
                    }
                    catch (ObjectDisposedException)
                    {
                        _isConnected = false;
                        _disposable.Dispose();
                        _disposable = Connect.Subscribe();
                    }
                    catch (Exception ex)
                    {
                        _isConnected = false;
                        _socketExceptionSubject.OnNext(new S7Exception("Connection issue", ex));
                    }
                }

                obs.OnNext(_isConnected == true);
            });

            return new SingleAssignmentDisposable { Disposable = tim };
        }).Retry().Publish(false).RefCount().DistinctUntilChanged();

    /// <summary>
    /// Gets the type of the PLC.
    /// </summary>
    /// <value>The type of the PLC.</value>
    public CpuType PLCType { get; }

    /// <summary>
    /// Gets the rack.
    /// </summary>
    /// <value>The rack.</value>
    public short Rack { get; }

    /// <summary>
    /// Gets the slot.
    /// </summary>
    /// <value>The slot.</value>
    public short Slot { get; }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Receives the specified buffer.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="size">The size.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>
    /// A int.
    /// </returns>
    public int Receive(Tag tag, byte[] buffer, int size, int offset = 0)
    {
        if (_initComplete)
        {
            try
            {
                if (_socket?.Connected == true)
                {
                    var r = _socket?.Receive(buffer, offset, size, SocketFlags.None);
                    if (tag != null && Debugger.IsAttached)
                    {
                        var res = buffer[21] == 255 ? Success : Failed;
                        Debug.WriteLine($"{DateTime.Now} Read Tag: {tag.Name} value: {tag.Value} {res}");
                    }

                    return r ?? -1;
                }

                _socketExceptionSubject.OnNext(new S7Exception("Device not connected"));
            }
            catch (Exception ex)
            {
                _socketExceptionSubject.OnNext(ex);
            }
        }

        return -1;
    }

    /// <summary>
    /// Sends the specified buffer.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="size">The size.</param>
    /// <returns>A int.</returns>
    public int Send(Tag tag, byte[] buffer, int size)
    {
        if (_initComplete)
        {
            try
            {
                if (_socket?.Connected == true)
                {
                    var r = _socket?.Send(buffer, size, SocketFlags.None);
                    if (tag != null && Debugger.IsAttached)
                    {
                        var res = r == size ? Success : Failed;
                        Debug.WriteLine($"{DateTime.Now} Wrote Tag: {tag.Name} value: {tag.Value} {res}");
                    }

                    return r ?? -1;
                }

                _socketExceptionSubject.OnNext(new S7Exception("Device not connected"));
            }
            catch (Exception ex)
            {
                _socketExceptionSubject.OnNext(ex);
            }
        }

        return -1;
    }

    internal (byte[] data, ushort size) GetSZLData(ushort szlArea, ushort index = 0)
    {
        ////               0, 1, 2, 3 , 4, 5,   6,   7,  8, 9, 10,11,12,13,14,15,16,17,18,19, 20,21, 22, 23,24,25,  26,27,28,29,30,31,32
        byte[] s7_SZL1 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 5, 0, 0, 8, 0, 8, 0, 1, 18, 4, 17, 68, 1, 0, 255, 9, 0, 4, 0, 0, 0, 0];
        byte[] s7_SZL2 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 6, 0, 0, 12, 0, 4, 0, 1, 18, 8, 18, 68, 1, 1, 0, 0, 0, 0, 10, 0, 0, 0];
        const int size = 1024;
        var data = new byte[size];
        var resultData = new byte[size];
        var tag = new Tag();
        int length;
        var done = false;
        var first = true;
        byte seq_in = 0;
        ushort seq_out = 0;
        var lastError = 0;
        var offset = 0;
        ushort lengthOfDataRead = 0;
        int szlDataLength;

        do
        {
            if (first)
            {
                Word.ToByteArray(++seq_out, s7_SZL1, 11);
                Word.ToByteArray(szlArea, s7_SZL1, 29);
                Word.ToByteArray(index, s7_SZL1, 31);
                Send(tag, s7_SZL1, s7_SZL1.Length);
            }
            else
            {
                Word.ToByteArray(++seq_out, s7_SZL2, 11);
                s7_SZL2[24] = seq_in;
                Send(tag, s7_SZL2, s7_SZL2.Length);
            }

            if (lastError != 0)
            {
                return (Array.Empty<byte>(), 0);
            }

            length = ReceiveIsoData(tag, ref data);

            if (lastError == 0 && length > 32)
            {
                if (first && (Word.FromByteArray(data, 27) == 0) && (data[29] == 255))
                {
                    szlDataLength = Word.FromByteArray(data, 31) - 8;
                    done = data[26] == 0;
                    seq_in = data[24];
                    lengthOfDataRead = Word.FromByteArray(data, 37);
                    Array.Copy(data, 41, resultData, offset, szlDataLength);
                    offset += szlDataLength;
                    lengthOfDataRead += lengthOfDataRead;
                    first = false;
                }
                else if ((Word.FromByteArray(data, 27) == 0) && (data[29] == 255))
                {
                    szlDataLength = Word.FromByteArray(data, 31);
                    done = data[26] == 0;
                    seq_in = data[24];
                    Array.Copy(data, 37, resultData, offset, szlDataLength);
                    offset += szlDataLength;
                    lengthOfDataRead += lengthOfDataRead;
                }
                else
                {
                    lastError = (int)ErrorCode.WrongVarFormat;
                }
            }
            else
            {
                lastError = (int)ErrorCode.WrongNumberReceivedBytes;
            }
        }
        while (!done && (lastError == 0));

        if (lastError == 0)
        {
            var result = new byte[length];
            Array.Copy(resultData, result, length);
            return (result, lengthOfDataRead);
        }

        return (Array.Empty<byte>(), 0);
    }

    internal int ReceiveIsoData(Tag tag, ref byte[] bytes)
    {
        var done = false;
        var size = 0;
        var lastError = 0;
        //// byte lastPDUType;
        while ((lastError == 0) && !done)
        {
            Receive(tag, bytes, 4);
            if (lastError == 0)
            {
                size = Word.FromByteArray(bytes, 2);

                if (size == 7)
                {
                    Receive(tag, bytes, 3, 4);
                }
                else if ((size > DataReadLength + 7) || (size < 16))
                {
                    lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                }
                else
                {
                    done = true;
                }
            }
        }

        switch (lastError)
        {
            case 0:
                // Get PDU Type, incase we need it
                Receive(tag, bytes, 3, 4);
                //// lastPDUType = bytes[5];

                // Receives the S7 ISO Payload
                Receive(tag, bytes, size - 7, 7);
                return size;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release
    /// only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            CloseSocket(_socket);
            if (disposing)
            {
                _disposable.Dispose();
                ((IDisposable)_socketExceptionSubject).Dispose();
            }

            _disposedValue = true;
        }
    }

    private static void CloseSocket(Socket? socket)
    {
        if (socket?.Connected == true)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task<bool> InitialiseSiemensConnectionAsync()
    {
        var bReceive = new byte[256];
        try
        {
            if (_socket == null)
            {
                return false;
            }

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 10000);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 10000);
            var server = new IPEndPoint(IPAddress.Parse(IP), 102);
            await _socket.ConnectAsync(server);
            _isConnected = _socket.Connected || (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);
            if (_isConnected == false)
            {
                return false;
            }

            ////            //0  1  2  3   4   5    6  7  8  9   10 11   12 13 14 15   16 17 18 19   20 21//
            byte[] bSend1 = [3, 0, 0, 22, 17, 224, 0, 0, 0, 46, 0, 193, 2, 1, 0, 194, 2, 3, 0, 192, 1, 9];

            switch (PLCType)
            {
                case CpuType.Logo0BA8:
                    bSend1[13] = 1;
                    bSend1[14] = 0;
                    bSend1[17] = 1;
                    bSend1[18] = 2;
                    DataReadLength = 240;
                    break;

                case CpuType.S7200:
                    bSend1[13] = 16;
                    bSend1[14] = 0;
                    bSend1[17] = 16;
                    bSend1[18] = 0;
                    DataReadLength = 480;
                    break;

                case CpuType.S71200:
                case CpuType.S7400:
                case CpuType.S7300:
                    bSend1[13] = 1;
                    bSend1[14] = 0;
                    bSend1[17] = 3;
                    bSend1[18] = (byte)((Rack * 2 * 16) + Slot);
                    DataReadLength = 480;
                    break;

                case CpuType.S71500:
                    bSend1[13] = 16;
                    bSend1[14] = 2;
                    bSend1[17] = 3;
                    bSend1[18] = (byte)((Rack * 2 * 16) + Slot);
                    DataReadLength = 480;
                    break;

                default:
                    return false;
            }

            var sent = _socket.Send(bSend1, 22, SocketFlags.None);

            var result = _socket.Receive(bReceive, 22, SocketFlags.None);
            if (result != 22)
            {
                throw new S7Exception(nameof(ErrorCode.WrongNumberReceivedBytes));
            }

            ////              1  2  3  4   5  6    7    8   9  10 11 12 13 14 15 16 17 18   19 20 21 22 23 24 25//
            byte[] bsend2 = [3, 0, 0, 25, 2, 240, 128, 50, 1, 0, 0, 4, 0, 0, 8, 0, 0, 240, 0, 0, 1, 0, 1, 0, 30];

            // (23,24) PDU Length Requested = HI-LO Here Default 480 bytes
            Word.ToByteArray(DataReadLength, bsend2, 23);
            _socket.Send(bsend2, 25, SocketFlags.None);

            result = _socket.Receive(bReceive, 27, SocketFlags.None);
            if (result != 27)
            {
                throw new S7Exception(nameof(ErrorCode.WrongNumberReceivedBytes));
            }
        }
        catch
        {
            return false;
        }

        _initComplete = true;
        return true;
    }
}
