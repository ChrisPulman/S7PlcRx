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
    private readonly Subject<ConnectionMetrics> _metricsSubject = new();

    // Optimized connection management
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    // Connection monitoring
    private readonly ConnectionMetrics _metrics = new();
    private readonly System.Threading.Timer? _metricsTimer;

    private Subject<Exception> _socketExceptionSubject = new();

    // State management
    private IDisposable _disposable;
    private bool _disposedValue;
    private bool _initComplete;
    private bool? _isAvailable;
    private bool? _isConnected;
    private Socket? _socket;
    private DateTime _lastSuccessfulOperation = DateTime.MinValue;
    private int _consecutiveErrors;

    // Active TSAP profile used to connect (for multi-connection compatibility)
    private TsapProfile _activeTsapProfile = TsapProfile.PG;

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
            if (_disposedValue)
            {
                obs.OnCompleted();
                return Disposable.Empty;
            }

            var dis = new CompositeDisposable();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            dis.Add(_socket);
            _initComplete = false;

            // Subject may have been disposed during teardown; recreate lazily.
            try
            {
                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex != null)
                    {
                        _metrics.RecordError();
                        LogError($"Socket exception: {ex.Message}");
                        obs.OnError(ex);
                    }
                }));
            }
            catch (ObjectDisposedException)
            {
                _socketExceptionSubject = new Subject<Exception>();
                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex != null)
                    {
                        _metrics.RecordError();
                        LogError($"Socket exception: {ex.Message}");
                        obs.OnError(ex);
                    }
                }));
            }

            dis.Add(IsConnected.Subscribe(
                deviceConnected =>
                {
                    var isAvail = _isAvailable != null && _isAvailable.HasValue && _isAvailable.Value;
                    obs.OnNext(isAvail && deviceConnected);
                    if (_initComplete && !deviceConnected)
                    {
                        CloseSocketOptimized(_socket);
                        _socket = null;
                        obs.OnError(new S7Exception("Device not connected"));
                    }
                },
                ex =>
                {
                    CloseSocketOptimized(_socket);
                    _socket = null;
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
                                    _socket = null;
                                    obs.OnError(new S7Exception("Device not connected"));
                                    return;
                                }

                                var isCon = _isConnected != null && _isConnected.HasValue && _isConnected.Value;
                                if (_initComplete && !isCon)
                                {
                                    CloseSocketOptimized(_socket);
                                    _socket = null;
                                    obs.OnError(new S7Exception("Device not connected"));
                                }
                            }
                            else
                            {
                                CloseSocketOptimized(_socket);
                                _socket = null;
                                obs.OnError(new S7Exception("Device Unavailable"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CloseSocketOptimized(_socket);
                        _socket = null;
                        obs.OnError(ex);
                    }
                },
                ex =>
                {
                    CloseSocketOptimized(_socket);
                    _socket = null;
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
    public ushort DataReadLength { get; }

    /// <summary>
    /// Gets the enhanced availability check with optimized ping strategy.
    /// </summary>
    public IObservable<bool> IsAvailable =>
        Observable.Create<bool>(obs =>
        {
            _isAvailable = null;
            var count = 0;

            var timer = new SerialDisposable();

            // Fast probe (startup)
            timer.Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(250)).Subscribe(async _ =>
            {
                count++;
                _isAvailable = await CheckAvailabilityOptimizedAsync();
                obs.OnNext(_isAvailable == true);

                // After a few quick probes, back off to reduce ping noise.
                if (count >= 8)
                {
                    count = 0;
                    timer.Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(async __ =>
                    {
                        _isAvailable = await CheckAvailabilityOptimizedAsync();
                        obs.OnNext(_isAvailable == true);
                    });
                }
            });

            return timer;
        }).Retry().Publish(false).RefCount();

    /// <summary>
    /// Gets the enhanced connection status with better detection logic.
    /// </summary>
    public IObservable<bool> IsConnected =>
        Observable.Create<bool>(obs =>
        {
            _isConnected = null;

            // Faster startup: check frequently until connected, then slow down.
            var timer = new SerialDisposable();
            var fast = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(50)).Subscribe(_ =>
            {
                var isConnectedNow = false;
                if (_socket == null)
                {
                    _isConnected = false;
                }
                else
                {
                    try
                    {
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

                isConnectedNow = _isConnected == true;
                obs.OnNext(isConnectedNow);

                if (isConnectedNow)
                {
                    // Switch to steady-state checks.
                    timer.Disposable = Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(__ =>
                    {
                        try
                        {
                            if (_socket == null)
                            {
                                _isConnected = false;
                            }
                            else
                            {
                                _isConnected = CheckConnectionStatusOptimized();
                            }
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

                        obs.OnNext(_isConnected == true);
                    });
                }
            });

            timer.Disposable = fast;
            return timer;
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

            RecordError();
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

            RecordError();
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
                // Stop connection/retry loops before disposing subjects
                try
                {
                    _disposable?.Dispose();
                }
                catch
                {
                }

                _metricsTimer?.Dispose();
                CloseSocketOptimized(_socket);
                _socket = null;

                try
                {
                    _socketExceptionSubject?.Dispose();
                }
                catch
                {
                }

                _metricsSubject?.Dispose();
                _connectionLock?.Dispose();
            }

            _disposedValue = true;
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
            // Try known TSAP profiles to maximize multi-connection compatibility
            var profiles = new[] { TsapProfile.PG, TsapProfile.OP, TsapProfile.PGAlt };

            foreach (var profile in profiles)
            {
                // Ensure fresh socket per attempt
                CloseSocketOptimized(_socket);
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

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
                var connectTask = Task.Factory.FromAsync(
                    (callback, state) => _socket.BeginConnect(server, callback, state),
                    _socket.EndConnect,
                    null);
                await connectTask.ConfigureAwait(false);
#else
                await _socket.ConnectAsync(server).ConfigureAwait(false);
#endif

                _isConnected = CheckConnectionStatusOptimized();
                if (_isConnected == false)
                {
                    continue;
                }

                // Handshake with current profile
                if (await PerformOptimizedHandshakeAsync(profile))
                {
                    _activeTsapProfile = profile;
                    _initComplete = true;
                    _lastSuccessfulOperation = DateTime.UtcNow;
                    _consecutiveErrors = 0;
                    LogInfo($"Successfully connected to {PLCType} at {IP}:102 with PDU length {DataReadLength}");
                    return true;
                }

                // Handshake failed, try next profile
                CloseSocketOptimized(_socket);
                _socket = null;
            }

            return false;
        }
        catch (Exception ex)
        {
            LogError($"Connection initialization failed: {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                if (!_disposedValue)
                {
                    _connectionLock.Release();
                }
            }
            catch (ObjectDisposedException)
            {
                // Teardown race: dispose may win while an async connect is completing.
            }
        }
    }

    /// <summary>
    /// Enhanced handshake procedure with async operations.
    /// </summary>
    private async Task<bool> PerformOptimizedHandshakeAsync(TsapProfile profile)
    {
        var bReceive = _bufferPool.Rent(256);
        try
        {
#if NETSTANDARD2_0
            return await PerformOptimizedHandshakeNetStandardAsync(bReceive, profile);
#else
            return await PerformOptimizedHandshakeModernAsync(bReceive, profile);
#endif
        }
        finally
        {
            _bufferPool.Return(bReceive);
        }
    }

#if NETSTANDARD2_0
    private async Task<bool> PerformOptimizedHandshakeNetStandardAsync(byte[] bReceive, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes(profile);
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

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactNetStandardAsync(bReceive, 22).ConfigureAwait(false);

            // minimal TPKT+COTP length sanity
            if (received < 7)
            {
                LogError($"Invalid connection response length {received}");
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

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactNetStandardAsync(bReceive, 27).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid communication setup response length {received}");
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

    private async Task<int> ReceiveTpktExactNetStandardAsync(byte[] buffer, int expectedMin)
    {
        // Read TPKT header (4 bytes)
        var read = await Task.Factory.FromAsync(
            (cb, s) => _socket!.BeginReceive(buffer, 0, 4, SocketFlags.None, cb, s),
            _socket!.EndReceive,
            null).ConfigureAwait(false);
        if (read != 4)
        {
            return read;
        }

        var length = (buffer[2] << 8) | buffer[3];
        if (length < expectedMin && expectedMin > 0)
        {
            // Try to continue anyway, but report
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}");
        }

        var remaining = length - 4;
        var total = 4;
        while (remaining > 0)
        {
            var r = await Task.Factory.FromAsync(
                (cb, s) => _socket!.BeginReceive(buffer, total, remaining, SocketFlags.None, cb, s),
                _socket!.EndReceive,
                null).ConfigureAwait(false);

            if (r <= 0)
            {
                break;
            }

            total += r;
            remaining -= r;
        }

        return total;
    }
#else
    private async Task<bool> PerformOptimizedHandshakeModernAsync(byte[] bReceive, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes(profile);
            var sent = await _socket!.SendAsync(bSend1, SocketFlags.None).ConfigureAwait(false);
            if (sent != bSend1.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactModernAsync(bReceive, 22).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid connection response length {received}");
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

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactModernAsync(bReceive, 27).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid communication setup response length {received}");
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

    private async Task<int> ReceiveTpktExactModernAsync(byte[] buffer, int expectedMin)
    {
        // Read TPKT header (4 bytes)
        var headerRead = await _socket!.ReceiveAsync(buffer.AsMemory(0, 4), SocketFlags.None).ConfigureAwait(false);
        if (headerRead != 4)
        {
            return headerRead;
        }

        var length = (buffer[2] << 8) | buffer[3];
        if (length < expectedMin && expectedMin > 0)
        {
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}");
        }

        var remaining = length - 4;
        var total = 4;
        while (remaining > 0)
        {
            var r = await _socket.ReceiveAsync(buffer.AsMemory(total, remaining), SocketFlags.None).ConfigureAwait(false);
            if (r <= 0)
            {
                break;
            }

            total += r;
            remaining -= r;
        }

        return total;
    }
#endif

    /// <summary>
    /// Gets connection request bytes for a specific TSAP profile.
    /// </summary>
    private byte[] GetConnectionRequestBytes(TsapProfile profile)
    {
        byte[] bSend1 = [3, 0, 0, 22, 17, 224, 0, 0, 0, 46, 0, 193, 2, 1, 0, 194, 2, 3, 0, 192, 1, 9];

        // Use TPDU size 512 (0x09) for S7-1200/300/400 compatibility

        // Source TSAP (C1)
        bSend1[13] = profile.SrcHi;
        bSend1[14] = profile.SrcLo;

        // Destination TSAP (C2)
        bSend1[17] = profile.DstHi;
        bSend1[18] = profile.DstLo(Rack, Slot);

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
            if (_disposedValue)
            {
                return;
            }

            try
            {
                LogWarning("Restarting connection due to failures");
                CloseSocketOptimized(_socket);
                _socket = null;
                _initComplete = false;
                _isConnected = false;

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort GetOptimalDataReadLength(CpuType plcType) => plcType switch
    {
        CpuType.Logo0BA8 => 240,
        CpuType.S7200 => 480,
        CpuType.S7300 => 480,
        CpuType.S7400 => 960,
        CpuType.S71200 => 960,
        CpuType.S71500 => 1440,
        _ => 480
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CloseSocketOptimized(Socket? socket)
    {
        if (socket == null)
        {
            return;
        }

        try
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        finally
        {
            try
            {
                socket.Close();
            }
            catch
            {
            }

            try
            {
                socket.Dispose();
            }
            catch
            {
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogError(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogInfo(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogWarning(string message)
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
            var result = await ping.SendPingAsync(IP, PingTimeoutMs).ConfigureAwait(false);
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
            var isConnected = _socket.Connected &&
                            !(_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);

            if (isConnected && DateTime.UtcNow - _lastSuccessfulOperation > TimeSpan.FromMinutes(2))
            {
                return PerformLightweightConnectionCheck();
            }

            if (!isConnected && _initComplete)
            {
                RestartConnection();
            }

            return isConnected;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch
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

    // TSAP profile helper
    private readonly record struct TsapProfile(byte SrcHi, byte SrcLo, byte DstHi, Func<short, short, byte> DstLo, string Name)
    {
        public static TsapProfile PG => new(0x01, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "PG");

        public static TsapProfile OP => new(0x02, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "OP");

        public static TsapProfile PGAlt => new(0x10, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "PGAlt");
    }
}
