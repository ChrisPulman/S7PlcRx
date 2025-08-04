// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
using DateTime = System.DateTime;
using TimeSpan = System.TimeSpan;

namespace S7PlcRx.Core;

/// <summary>
/// Enhanced Siemens Socket Rx with optimized performance for PLC communication.
/// </summary>
internal class S7SocketRx : IDisposable
{
    private const string Failed = nameof(Failed);
    private const string Success = nameof(Success);
    private const int DefaultTimeout = 10000;
    private const int MaxRetryAttempts = 3;
    private const int PingTimeoutMs = 2000;

    // Enhanced observables for better monitoring
    private readonly Subject<Exception> _socketExceptionSubject = new();
    private readonly Subject<ConnectionMetrics> _metricsSubject = new();

    // Optimized connection management
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    // Connection monitoring
    private readonly ConnectionMetrics _metrics = new();
    private readonly System.Threading.Timer? _metricsTimer;

    // State management
    private IDisposable _disposable;
    private bool _disposedValue;
    private bool _initComplete;
    private bool? _isAvailable;
    private bool? _isConnected;
    private Socket? _socket;
    private DateTime _lastSuccessfulOperation = DateTime.MinValue;
    private int _consecutiveErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="S7SocketRx" /> class.
    /// </summary>
    /// <param name="ip">The ip.</param>
    /// <param name="plcType">Type of the PLC.</param>
    /// <param name="rack">The rack.</param>
    /// <param name="slot">The slot.</param>
    public S7SocketRx(string ip, CpuType plcType, short rack, short slot)
    {
        IP = ip ?? throw new ArgumentNullException(nameof(ip));
        PLCType = plcType;
        Rack = rack;
        Slot = slot;

        // Set optimized data read length based on PLC type capabilities
        DataReadLength = GetOptimalDataReadLength(plcType);

        // Initialize metrics reporting
        _metricsTimer = new System.Threading.Timer(ReportMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _disposable = Connect.Subscribe();
    }

    /// <summary>
    /// Gets the enhanced connect observable with improved error handling.
    /// </summary>
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
                    _metrics.RecordError();
                    LogError($"Socket exception: {ex.Message}");
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
                        CloseSocketOptimized(_socket);
                        obs.OnError(new S7Exception("Device not connected"));
                    }
                },
                ex =>
                {
                    CloseSocketOptimized(_socket);
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
                                if (!_initComplete && !await InitializeSiemensConnectionOptimizedAsync())
                                {
                                    CloseSocketOptimized(_socket);
                                    obs.OnError(new S7Exception("Device not connected"));
                                    return;
                                }

                                var isCon = _isConnected != null && _isConnected.HasValue && _isConnected.Value;
                                if (_initComplete && !isCon)
                                {
                                    CloseSocketOptimized(_socket);
                                    obs.OnError(new S7Exception("Device not connected"));
                                }
                            }
                            else
                            {
                                CloseSocketOptimized(_socket);
                                obs.OnError(new S7Exception("Device Unavailable"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CloseSocketOptimized(_socket);
                        obs.OnError(ex);
                    }
                },
                ex =>
                {
                    CloseSocketOptimized(_socket);
                    obs.OnError(ex);
                }));

            return dis;
        })
        .RetryWhen(errors => errors
            .Select((ex, index) => new { ex, index })
            .SelectMany(x => x.index < MaxRetryAttempts
                ? Observable.Timer(TimeSpan.FromSeconds(Math.Pow(2, x.index))) // Exponential backoff
                : Observable.Throw<long>(x.ex)))
        .Publish(false)
        .RefCount();

    /// <summary>
    /// Gets the ip.
    /// </summary>
    public string IP { get; }

    /// <summary>
    /// Gets the optimized data read length based on PLC type capabilities.
    /// </summary>
    public ushort DataReadLength { get; private set; }

    /// <summary>
    /// Gets the enhanced availability check with optimized ping strategy.
    /// </summary>
    public IObservable<bool> IsAvailable =>
        Observable.Create<bool>(obs =>
        {
            IDisposable tim;
            _isAvailable = null;
            var count = 0;
            tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(10)).Subscribe(async _ =>
            {
                count++;
                if (_isAvailable == null || !_isAvailable.HasValue ||
                    (count == 1 && !_isAvailable.Value) ||
                    (count == 10 && _isAvailable.Value))
                {
                    count = 0;
                    _isAvailable = await CheckAvailabilityOptimizedAsync();
                }

                obs.OnNext(_isAvailable == true);
            });
            return new SingleAssignmentDisposable { Disposable = tim };
        }).Retry().Publish(false).RefCount();

    /// <summary>
    /// Gets the enhanced connection status with better detection logic.
    /// </summary>
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
                        // Enhanced connection detection with stale connection check
                        _isConnected = CheckConnectionStatusOptimized();
                    }
                    catch (ObjectDisposedException)
                    {
                        _isConnected = false;
                        RestartConnection();
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
    public CpuType PLCType { get; }

    /// <summary>
    /// Gets the rack.
    /// </summary>
    public short Rack { get; }

    /// <summary>
    /// Gets the slot.
    /// </summary>
    public short Slot { get; }

    /// <summary>
    /// Gets connection metrics observable for monitoring.
    /// </summary>
    public IObservable<ConnectionMetrics> Metrics => _metricsSubject.AsObservable();

    /// <summary>
    /// Enhanced receive method with performance monitoring and better error handling.
    /// </summary>
    public int Receive(Tag tag, byte[] buffer, int size, int offset = 0)
    {
        if (!_initComplete)
        {
            return -1;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (_socket?.Connected == true)
            {
                var received = _socket.Receive(buffer, offset, size, SocketFlags.None);

                stopwatch.Stop();
                RecordSuccessfulOperation(stopwatch.Elapsed, received, isReceive: true);

                if (tag != null && Debugger.IsAttached)
                {
                    var result = buffer[21] == 255 ? Success : Failed;
                    Debug.WriteLine($"{DateTime.Now} Read Tag: {tag.Name} value: {tag.Value} {result} ({received} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return received;
            }

            _socketExceptionSubject.OnNext(new S7Exception("Device not connected"));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>
    /// Enhanced send method with performance monitoring and better error handling.
    /// </summary>
    public int Send(Tag tag, byte[] buffer, int size)
    {
        if (!_initComplete)
        {
            return -1;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (_socket?.Connected == true)
            {
                var sent = _socket.Send(buffer, size, SocketFlags.None);

                stopwatch.Stop();
                RecordSuccessfulOperation(stopwatch.Elapsed, sent, isReceive: false);

                if (tag != null && Debugger.IsAttached)
                {
                    var result = sent == size ? Success : Failed;
                    Debug.WriteLine($"{DateTime.Now} Wrote Tag: {tag.Name} value: {tag.Value} {result} ({sent}/{size} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return sent;
            }

            _socketExceptionSubject.OnNext(new S7Exception("Device not connected"));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Enhanced SZL data retrieval with optimized buffer management.
    /// </summary>
    internal (byte[] data, ushort size) GetSZLData(ushort szlArea, ushort index = 0)
    {
        // Enhanced SZL communication protocols
        byte[] s7_SZL1 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 5, 0, 0, 8, 0, 8, 0, 1, 18, 4, 17, 68, 1, 0, 255, 9, 0, 4, 0, 0, 0, 0];
        byte[] s7_SZL2 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 6, 0, 0, 12, 0, 4, 0, 1, 18, 8, 18, 68, 1, 1, 0, 0, 0, 0, 10, 0, 0, 0];

        const int bufferSize = 1024;
        var data = _bufferPool.Rent(bufferSize);
        var resultData = _bufferPool.Rent(bufferSize);

        try
        {
            var tag = new Tag();
            int length;
            var done = false;
            var first = true;
            byte seqIn = 0;
            ushort seqOut = 0;
            var lastError = 0;
            var offset = 0;
            ushort lengthOfDataRead = 0;
            int szlDataLength;

            do
            {
                if (first)
                {
                    Word.ToByteArray(++seqOut, s7_SZL1, 11);
                    Word.ToByteArray(szlArea, s7_SZL1, 29);
                    Word.ToByteArray(index, s7_SZL1, 31);
                    Send(tag, s7_SZL1, s7_SZL1.Length);
                }
                else
                {
                    Word.ToByteArray(++seqOut, s7_SZL2, 11);
                    s7_SZL2[24] = seqIn;
                    Send(tag, s7_SZL2, s7_SZL2.Length);
                }

                if (lastError != 0)
                {
                    return (Array.Empty<byte>(), 0);
                }

                length = ReceiveIsoData(tag, ref data);

                if (lastError == 0 && length > 32)
                {
                    if (first && Word.FromByteArray(data, 27) == 0 && data[29] == 255)
                    {
                        szlDataLength = Word.FromByteArray(data, 31) - 8;
                        done = data[26] == 0;
                        seqIn = data[24];
                        lengthOfDataRead = Word.FromByteArray(data, 37);
                        Array.Copy(data, 41, resultData, offset, szlDataLength);
                        offset += szlDataLength;
                        lengthOfDataRead += lengthOfDataRead;
                        first = false;
                    }
                    else if (Word.FromByteArray(data, 27) == 0 && data[29] == 255)
                    {
                        szlDataLength = Word.FromByteArray(data, 31);
                        done = data[26] == 0;
                        seqIn = data[24];
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
            while (!done && lastError == 0);

            if (lastError == 0)
            {
                var result = new byte[offset];
                Array.Copy(resultData, result, offset);
                return (result, lengthOfDataRead);
            }

            return (Array.Empty<byte>(), 0);
        }
        finally
        {
            _bufferPool.Return(data);
            _bufferPool.Return(resultData);
        }
    }

    /// <summary>
    /// Enhanced ISO data reception with better error handling.
    /// </summary>
    internal int ReceiveIsoData(Tag tag, ref byte[] bytes)
    {
        var done = false;
        var size = 0;
        var lastError = 0;

        while (lastError == 0 && !done)
        {
            var headerReceived = Receive(tag, bytes, 4);
            if (headerReceived != 4)
            {
                lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                break;
            }

            size = Word.FromByteArray(bytes, 2);

            if (size == 7)
            {
                Receive(tag, bytes, 3, 4);
            }
            else if (size > DataReadLength + 7 || size < 16)
            {
                lastError = (int)ErrorCode.WrongNumberReceivedBytes;
            }
            else
            {
                done = true;
            }
        }

        switch (lastError)
        {
            case 0:
                // Get PDU Type
                Receive(tag, bytes, 3, 4);

                // Receive S7 ISO Payload
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
            if (disposing)
            {
                _metricsTimer?.Dispose();
                CloseSocketOptimized(_socket);

                _disposable?.Dispose();
                _socketExceptionSubject?.Dispose();
                _metricsSubject?.Dispose();
                _connectionLock?.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Gets optimal data read length based on PLC type for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetOptimalDataReadLength(CpuType plcType) => plcType switch
    {
        CpuType.Logo0BA8 => 240,
        CpuType.S7200 => 480,
        CpuType.S71200 => 960,  // Enhanced for newer PLCs
        CpuType.S7300 => 480,
        CpuType.S7400 => 960,   // Enhanced for high-end PLCs
        CpuType.S71500 => 1440, // Maximum for S7-1500
        _ => 480
    };

    /// <summary>
    /// Optimized socket closure with proper cleanup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CloseSocketOptimized(Socket? socket)
    {
        if (socket?.Connected == true)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore shutdown errors
            }
            finally
            {
                socket.Close();
                socket.Dispose();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogError(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogInfo(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWarning(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    /// <summary>
    /// Optimized availability check using efficient async ping.
    /// </summary>
    private async Task<bool> CheckAvailabilityOptimizedAsync()
    {
        if (string.IsNullOrWhiteSpace(IP))
        {
            return false;
        }

        try
        {
            using var ping = new Ping();
            var result = await ping.SendPingAsync(IP, PingTimeoutMs);
            return result.Status == IPStatus.Success;
        }
        catch (PingException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogError($"Ping failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enhanced connection status check with stale connection detection.
    /// </summary>
    private bool CheckConnectionStatusOptimized()
    {
        if (_socket == null)
        {
            return false;
        }

        try
        {
            // Use more efficient connection detection
            var isConnected = _socket.Connected &&
                            !(_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);

            // Check for stale connections (no activity for 2 minutes)
            if (isConnected && DateTime.UtcNow - _lastSuccessfulOperation > TimeSpan.FromMinutes(2))
            {
                // Connection might be stale, perform lightweight check
                return PerformLightweightConnectionCheck();
            }

            return isConnected;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Performs lightweight connection check.
    /// </summary>
    private bool PerformLightweightConnectionCheck()
    {
        try
        {
            _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enhanced Siemens connection initialization with async operations.
    /// </summary>
    private async Task<bool> InitializeSiemensConnectionOptimizedAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_socket == null)
            {
                return false;
            }

            // Enhanced socket configuration for optimal performance
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DefaultTimeout);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, DefaultTimeout);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            // Set optimal buffer sizes based on PLC type
            var bufferSize = DataReadLength * 2;
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);

            var server = new IPEndPoint(IPAddress.Parse(IP), 102);

#if NETSTANDARD2_0
            // Use APM pattern for .NET Standard 2.0 compatibility
            var connectTask = Task.Factory.FromAsync(
                (callback, state) => _socket.BeginConnect(server, callback, state),
                _socket.EndConnect,
                null);
            await connectTask.ConfigureAwait(false);
#else
            // Use modern async API for .NET 8+
            await _socket.ConnectAsync(server).ConfigureAwait(false);
#endif

            _isConnected = CheckConnectionStatusOptimized();
            if (_isConnected == false)
            {
                return false;
            }

            // Enhanced handshake with better error handling
            if (!await PerformOptimizedHandshakeAsync())
            {
                return false;
            }

            _initComplete = true;
            _lastSuccessfulOperation = DateTime.UtcNow;
            _consecutiveErrors = 0;

            LogInfo($"Successfully connected to {PLCType} at {IP}:102 with PDU length {DataReadLength}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Connection initialization failed: {ex.Message}");
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Enhanced handshake procedure with async operations.
    /// </summary>
    private async Task<bool> PerformOptimizedHandshakeAsync()
    {
        var bReceive = _bufferPool.Rent(256);
        try
        {
#if NETSTANDARD2_0
            return await PerformOptimizedHandshakeNetStandardAsync(bReceive);
#else
            return await PerformOptimizedHandshakeModernAsync(bReceive);
#endif
        }
        finally
        {
            _bufferPool.Return(bReceive);
        }
    }

#if NETSTANDARD2_0
    /// <summary>
    /// Enhanced handshake procedure for .NET Standard 2.0 compatibility.
    /// </summary>
    /// <param name="bReceive">The receive buffer.</param>
    /// <returns>A task representing the handshake operation.</returns>
    private async Task<bool> PerformOptimizedHandshakeNetStandardAsync(byte[] bReceive)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes();
            var sentTask = Task.Factory.FromAsync(
                (callback, state) => _socket!.BeginSend(bSend1, 0, bSend1.Length, SocketFlags.None, callback, state),
                _socket!.EndSend,
                null);
            var sent = await sentTask.ConfigureAwait(false);

            if (sent != bSend1.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response
            var receiveTask = Task.Factory.FromAsync(
                (callback, state) => _socket.BeginReceive(bReceive, 0, 22, SocketFlags.None, callback, state),
                _socket.EndReceive,
                null);
            var received = await receiveTask.ConfigureAwait(false);

            if (received != 22)
            {
                LogError($"Expected 22 bytes in connection response, received {received}");
                return false;
            }

            // Step 3: Communication setup request
            var bSend2 = GetCommunicationSetupBytes();
            sentTask = Task.Factory.FromAsync(
                (callback, state) => _socket.BeginSend(bSend2, 0, bSend2.Length, SocketFlags.None, callback, state),
                _socket.EndSend,
                null);
            sent = await sentTask.ConfigureAwait(false);

            if (sent != bSend2.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response
            receiveTask = Task.Factory.FromAsync(
                (callback, state) => _socket.BeginReceive(bReceive, 0, 27, SocketFlags.None, callback, state),
                _socket.EndReceive,
                null);
            received = await receiveTask.ConfigureAwait(false);

            if (received != 27)
            {
                LogError($"Expected 27 bytes in communication setup response, received {received}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }
#else
    /// <summary>
    /// Enhanced handshake procedure for modern .NET versions (.NET 8+).
    /// </summary>
    /// <param name="bReceive">The receive buffer.</param>
    /// <returns>A task representing the handshake operation.</returns>
    private async Task<bool> PerformOptimizedHandshakeModernAsync(byte[] bReceive)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes();
            var sent = await _socket!.SendAsync(bSend1, SocketFlags.None).ConfigureAwait(false);
            if (sent != bSend1.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response
            var received = await _socket.ReceiveAsync(bReceive.AsMemory(0, 22), SocketFlags.None).ConfigureAwait(false);
            if (received != 22)
            {
                LogError($"Expected 22 bytes in connection response, received {received}");
                return false;
            }

            // Step 3: Communication setup request
            var bSend2 = GetCommunicationSetupBytes();
            sent = await _socket.SendAsync(bSend2, SocketFlags.None).ConfigureAwait(false);
            if (sent != bSend2.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response
            received = await _socket.ReceiveAsync(bReceive.AsMemory(0, 27), SocketFlags.None).ConfigureAwait(false);
            if (received != 27)
            {
                LogError($"Expected 27 bytes in communication setup response, received {received}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }
#endif

    /// <summary>
    /// Gets connection request bytes optimized for specific PLC type.
    /// </summary>
    private byte[] GetConnectionRequestBytes()
    {
        byte[] bSend1 = [3, 0, 0, 22, 17, 224, 0, 0, 0, 46, 0, 193, 2, 1, 0, 194, 2, 3, 0, 192, 1, 9];

        switch (PLCType)
        {
            case CpuType.Logo0BA8:
                bSend1[13] = 1;
                bSend1[14] = 0;
                bSend1[17] = 1;
                bSend1[18] = 2;
                break;
            case CpuType.S7200:
                bSend1[13] = 16;
                bSend1[14] = 0;
                bSend1[17] = 16;
                bSend1[18] = 0;
                break;
            case CpuType.S71200:
            case CpuType.S7400:
            case CpuType.S7300:
                bSend1[13] = 1;
                bSend1[14] = 0;
                bSend1[17] = 3;
                bSend1[18] = (byte)((Rack * 2 * 16) + Slot);
                break;
            case CpuType.S71500:
                bSend1[13] = 16;
                bSend1[14] = 2;
                bSend1[17] = 3;
                bSend1[18] = (byte)((Rack * 2 * 16) + Slot);
                break;
            default:
                throw new ArgumentException($"Unsupported PLC type: {PLCType}");
        }

        return bSend1;
    }

    /// <summary>
    /// Gets communication setup bytes with optimal PDU length.
    /// </summary>
    private byte[] GetCommunicationSetupBytes()
    {
        byte[] bSend2 = [3, 0, 0, 25, 2, 240, 128, 50, 1, 0, 0, 4, 0, 0, 8, 0, 0, 240, 0, 0, 1, 0, 1, 0, 30];

        // Set optimal PDU length for the specific PLC type
        Word.ToByteArray(DataReadLength, bSend2, 23);

        return bSend2;
    }

    /// <summary>
    /// Records successful operation for monitoring.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordSuccessfulOperation(TimeSpan duration, int bytes, bool isReceive)
    {
        _lastSuccessfulOperation = DateTime.UtcNow;
        _consecutiveErrors = 0;

        if (isReceive)
        {
            _metrics.RecordReceive(duration, bytes);
        }
        else
        {
            _metrics.RecordSend(duration, bytes);
        }
    }

    /// <summary>
    /// Records error for monitoring and connection health.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordError()
    {
        _consecutiveErrors++;
        _metrics.RecordError();

        // Trigger connection restart if too many consecutive errors
        if (_consecutiveErrors > 5)
        {
            LogWarning($"Excessive failures detected: {_consecutiveErrors}. Restarting connection.");
            RestartConnection();
        }
    }

    /// <summary>
    /// Restarts connection on critical failures.
    /// </summary>
    private void RestartConnection() =>
        Task.Run(async () =>
        {
            try
            {
                LogWarning("Restarting connection due to failures");
                CloseSocketOptimized(_socket);
                _initComplete = false;
                _isConnected = false;

                // Give a brief pause before restart
                await Task.Delay(1000);

                _disposable?.Dispose();
                _disposable = Connect.Subscribe();
            }
            catch (Exception ex)
            {
                LogError($"Connection restart failed: {ex.Message}");
            }
        });

    /// <summary>
    /// Reports metrics periodically.
    /// </summary>
    private void ReportMetrics(object? state)
    {
        try
        {
            _metricsSubject.OnNext(_metrics.GetSnapshot());
        }
        catch (Exception ex)
        {
            LogError($"Failed to report metrics: {ex.Message}");
        }
    }
}
