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
/// Provides an enhanced, observable-based socket communication layer for connecting to Siemens S7 PLCs with improved
/// connection management, error handling, and performance monitoring.
/// </summary>
/// <remarks>S7SocketRx manages the lifecycle of a TCP/IP connection to a Siemens S7 PLC, exposing connection and
/// availability status as observables for reactive monitoring. It supports automatic reconnection with exponential
/// backoff, optimized data transfer based on PLC type, and periodic metrics reporting. This class is intended for
/// internal use and is not thread-safe for concurrent operations beyond its designed observability and connection
/// management. Dispose the instance to release all resources and terminate background monitoring.</remarks>
internal class S7SocketRx : IDisposable
{
    private const string Failed = nameof(Failed);
    private const string Success = nameof(Success);
    private const int DefaultTimeout = 10000;
    private const int InitialRetryDelayMilliseconds = 250;
    private const int MaxRetryDelayMilliseconds = 5000;
    private const int PingTimeoutMs = 2000;
    private const int PortProbeTimeoutMs = 750;
    private const int AvailabilityFailureThreshold = 3;
    private const int ConnectionFailureThreshold = 3;
    private static readonly TimeSpan PlcReadyProbeInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PlcReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecentOperationAvailabilityWindow = TimeSpan.FromSeconds(3);

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
    private int _consecutiveAvailabilityFailures;
    private int _consecutiveConnectionFailures;
    private int _restartInProgress;

    // Active TSAP profile used to connect (for multi-connection compatibility)
    private TsapProfile _activeTsapProfile = TsapProfile.PG;

    /// <summary>
    /// Initializes a new instance of the <see cref="S7SocketRx"/> class for communicating with a Siemens S7 PLC using the specified.
    /// connection parameters.
    /// </summary>
    /// <remarks>This constructor configures the connection and initializes metrics reporting for the PLC
    /// communication session. The optimal data read length is set automatically based on the specified PLC
    /// type.</remarks>
    /// <param name="ip">The IP address of the target PLC. Cannot be null.</param>
    /// <param name="plcType">The type of PLC CPU to connect to.</param>
    /// <param name="rack">The rack number of the PLC to connect to.</param>
    /// <param name="slot">The slot number of the PLC to connect to.</param>
    /// <exception cref="ArgumentNullException">Thrown if ip is null.</exception>
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
    /// Gets an observable sequence that manages the connection to the device, emitting the connection status as a
    /// boolean value.
    /// </summary>
    /// <remarks>The observable attempts to establish and maintain a connection to the device, automatically
    /// retrying on failure with exponential backoff up to a maximum delay. The sequence emits <see langword="true"/>
    /// when the device is connected and available, and <see langword="false"/> otherwise. If the connection cannot be
    /// established or is lost, the observable signals an error. Subscribers receive updates on the connection status
    /// and are notified of errors such as device unavailability or socket exceptions. The connection is shared among
    /// all subscribers, and resources are released when there are no active subscriptions.</remarks>
    public IObservable<bool> Connect =>
        Observable.Create<bool>(obs =>
        {
            if (_disposedValue)
            {
                obs.OnCompleted();
                return Disposable.Empty;
            }

            var dis = new CompositeDisposable();
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
            .SelectMany(x =>
            {
                // Exponential backoff with cap: 1s, 2s, 4s, 8s, 16s, 30s, 30s...
                // Use bit shifting for better performance and prevent overflow
                var exponent = Math.Min(x.index, 4);
                var delayMilliseconds = Math.Min(InitialRetryDelayMilliseconds * (1 << exponent), MaxRetryDelayMilliseconds);

                // Log only first 5 attempts and then every 10th attempt to prevent log flooding
                if (x.index < 5 || x.index % 10 == 0)
                {
                    LogWarning($"Connection attempt {x.index + 1} failed: {x.ex.Message}. Retrying in {delayMilliseconds}ms...");
                }

                return Observable.Timer(TimeSpan.FromMilliseconds(delayMilliseconds));
            }))
        .Publish(false)
        .RefCount();

    /// <summary>
    /// Gets the IP address associated with the current instance.
    /// </summary>
    public string IP { get; }

    /// <summary>
    /// Gets the optimized data read length based on PLC type capabilities.
    /// </summary>
    public ushort DataReadLength { get; private set; }

    /// <summary>
    /// Gets an observable sequence that indicates whether the resource is currently available.
    /// </summary>
    /// <remarks>The observable emits a value each time the availability status is checked, providing <see
    /// langword="true"/> if the resource is available; otherwise, <see langword="false"/>. The sequence uses a fast
    /// polling interval initially, then reduces frequency to minimize resource usage. Subscribers receive updates as
    /// the availability status changes. The sequence is shared among all subscribers and automatically manages its
    /// lifetime.</remarks>
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
                await ProbeAvailabilityAndNotifyAsync(obs).ConfigureAwait(false);

                // After a few quick probes, back off to reduce ping noise.
                if (count >= 8)
                {
                    count = 0;
                    timer.Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(async __ =>
                    {
                        await ProbeAvailabilityAndNotifyAsync(obs).ConfigureAwait(false);
                    });
                }
            });

            return timer;
        }).Retry().Publish(false).RefCount();

    /// <summary>
    /// Gets an observable sequence that indicates whether the connection to the remote endpoint is currently
    /// established.
    /// </summary>
    /// <remarks>The observable emits a value whenever the connection status changes. Subscribers receive <see
    /// langword="true"/> when the connection is established and <see langword="false"/> when it is lost. The sequence
    /// emits the current status immediately upon subscription and continues to provide updates as the connection state
    /// changes. The observable is shared among all subscribers and only emits distinct consecutive values.</remarks>
    public IObservable<bool> IsConnected =>
        Observable.Create<bool>(obs =>
        {
            _isConnected = null;

            // Faster startup: check frequently until connected, then slow down.
            var timer = new SerialDisposable();
            var fast = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(50)).Subscribe(_ =>
            {
                _isConnected = EvaluateConnectionStateWithHysteresis();
                var isConnectedNow = _initComplete && _isConnected == true;
                obs.OnNext(isConnectedNow);

                if (isConnectedNow)
                {
                    // Switch to steady-state checks.
                    timer.Disposable = Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(__ =>
                    {
                        _isConnected = EvaluateConnectionStateWithHysteresis();
                        obs.OnNext(_initComplete && _isConnected == true);
                    });
                }
            });

            timer.Disposable = fast;
            return timer;
        }).Retry().Publish(false).RefCount().DistinctUntilChanged();

    /// <summary>
    /// Gets the type of PLC (Programmable Logic Controller) associated with this instance.
    /// </summary>
    public CpuType PLCType { get; }

    /// <summary>
    /// Gets the rack number associated with the device or connection.
    /// </summary>
    public short Rack { get; }

    /// <summary>
    /// Gets the slot number associated with this instance.
    /// </summary>
    public short Slot { get; }

    /// <summary>
    /// Gets an observable sequence that provides real-time connection metrics.
    /// </summary>
    /// <remarks>Subscribers receive updates whenever new connection metrics are available. The sequence
    /// completes when the underlying connection is closed or disposed.</remarks>
    public IObservable<ConnectionMetrics> Metrics => _metricsSubject.AsObservable();

    /// <summary>
    /// Receives data from the connected device and writes it into the specified buffer.
    /// </summary>
    /// <remarks>If the device is not connected or initialization is incomplete, the method returns -1 and no
    /// data is written to the buffer. Exceptions encountered during the receive operation are reported through the
    /// socket exception subject. The method is not thread-safe.</remarks>
    /// <param name="tag">The tag associated with the data to be received. Can be null if tag information is not required.</param>
    /// <param name="buffer">The buffer to store the received data. Must not be null and must have sufficient space to accommodate the data.</param>
    /// <param name="size">The maximum number of bytes to receive. Must be greater than zero and not exceed the available space in the
    /// buffer starting at the specified offset.</param>
    /// <param name="offset">The zero-based position in the buffer at which to begin storing the received data. Must be non-negative and
    /// within the bounds of the buffer.</param>
    /// <returns>The number of bytes received and written to the buffer, or -1 if the operation fails or the device is not
    /// connected.</returns>
    public int Receive(Tag tag, byte[] buffer, int size, int offset = 0)
        => ReceiveCore(tag, buffer, size, offset, traceOperation: true);

    /// <summary>
    /// Sends data to the connected device using the specified tag and buffer.
    /// </summary>
    /// <remarks>If the device is not connected or an error occurs during the send operation, the method
    /// returns -1 and notifies subscribers of the exception. The method does not throw exceptions for connection or
    /// send failures.</remarks>
    /// <param name="tag">The tag associated with the data being sent. Can be null if no tag information is required.</param>
    /// <param name="buffer">The buffer containing the data to send. Cannot be null.</param>
    /// <param name="size">The number of bytes to send from the buffer. Must be less than or equal to the length of the buffer and greater
    /// than zero.</param>
    /// <returns>The number of bytes sent if the operation is successful; otherwise, -1 if the device is not connected or an
    /// error occurs.</returns>
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
    /// Retrieves System-Zustandsliste (SZL) data from the PLC for the specified SZL area and index.
    /// </summary>
    /// <remarks>This method performs a low-level SZL read operation using enhanced communication protocols.
    /// The returned data format depends on the specified SZL area and index. If the requested SZL area or index is not
    /// available, the returned data will be empty. This method is intended for advanced scenarios where direct access
    /// to PLC system information is required.</remarks>
    /// <param name="szlArea">The SZL area code that identifies the type of system information to retrieve.</param>
    /// <param name="index">The index within the SZL area to access. The default is 0.</param>
    /// <returns>A tuple containing a byte array with the SZL data and a ushort indicating the size of the data. Returns an empty
    /// array and zero size if the operation fails.</returns>
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
    /// Receives ISO protocol data from the specified tag and stores the result in the provided byte array.
    /// </summary>
    /// <remarks>The method expects the incoming data to conform to the ISO protocol format. If the received
    /// data does not meet protocol requirements, the method returns 0 to indicate failure.</remarks>
    /// <param name="tag">The tag representing the communication endpoint from which to receive ISO data.</param>
    /// <param name="bytes">A reference to the byte array that receives the data. The array must be large enough to hold the received ISO
    /// data.</param>
    /// <returns>The total number of bytes received if the operation is successful; otherwise, 0 if an error occurs.</returns>
    internal int ReceiveIsoData(Tag tag, ref byte[] bytes)
    {
        var done = false;
        var size = 0;
        var lastError = 0;

        while (lastError == 0 && !done)
        {
            var headerReceived = ReceiveExact(tag, bytes, 4);
            if (headerReceived != 4)
            {
                lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                break;
            }

            size = Word.FromByteArray(bytes, 2);

            if (size == 7)
            {
                if (ReceiveExact(tag, bytes, 3, 4) != 3)
                {
                    lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                }
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
                if (ReceiveExact(tag, bytes, 3, 4) != 3)
                {
                    return 0;
                }

                // Receive S7 ISO Payload
                if (ReceiveExact(tag, bytes, size - 7, 7) != size - 7)
                {
                    return 0;
                }

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

    private async Task<bool> WaitForPlcReadyAsync(Socket socket)
    {
        var deadline = DateTime.UtcNow + PlcReadyTimeout;
        var consecutiveSuccessfulProbes = 0;

        while (!_disposedValue && DateTime.UtcNow < deadline)
        {
            if (!CheckConnectionStatusOptimized(socket))
            {
                return false;
            }

            var cpuData = GetSZLData(socket, 28);
            var orderCode = GetSZLData(socket, 17);
            if (cpuData.data.Length >= 204 && orderCode.data.Length >= 25)
            {
                consecutiveSuccessfulProbes++;
                if (consecutiveSuccessfulProbes >= 2)
                {
                    return true;
                }
            }
            else
            {
                consecutiveSuccessfulProbes = 0;
            }

            await Task.Delay(PlcReadyProbeInterval).ConfigureAwait(false);
        }

        return false;
    }

    private (byte[] data, ushort size) GetSZLData(Socket socket, ushort szlArea, ushort index = 0)
    {
        byte[] s7_SZL1 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 5, 0, 0, 8, 0, 8, 0, 1, 18, 4, 17, 68, 1, 0, 255, 9, 0, 4, 0, 0, 0, 0];
        byte[] s7_SZL2 = [3, 0, 0, 33, 2, 240, 128, 50, 7, 0, 0, 6, 0, 0, 12, 0, 4, 0, 1, 18, 8, 18, 68, 1, 1, 0, 0, 0, 0, 10, 0, 0, 0];

        const int bufferSize = 1024;
        var data = _bufferPool.Rent(bufferSize);
        var resultData = _bufferPool.Rent(bufferSize);

        try
        {
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
                    if (SendOnSocket(socket, s7_SZL1, s7_SZL1.Length) != s7_SZL1.Length)
                    {
                        return (Array.Empty<byte>(), 0);
                    }
                }
                else
                {
                    Word.ToByteArray(++seqOut, s7_SZL2, 11);
                    s7_SZL2[24] = seqIn;
                    if (SendOnSocket(socket, s7_SZL2, s7_SZL2.Length) != s7_SZL2.Length)
                    {
                        return (Array.Empty<byte>(), 0);
                    }
                }

                length = ReceiveIsoData(socket, ref data);

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
                        first = false;
                    }
                    else if (Word.FromByteArray(data, 27) == 0 && data[29] == 255)
                    {
                        szlDataLength = Word.FromByteArray(data, 31);
                        done = data[26] == 0;
                        seqIn = data[24];
                        Array.Copy(data, 37, resultData, offset, szlDataLength);
                        offset += szlDataLength;
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    private int SendOnSocket(Socket socket, byte[] buffer, int size)
    {
        var total = 0;
        while (total < size)
        {
            var sent = socket.Send(buffer, total, size - total, SocketFlags.None);
            if (sent <= 0)
            {
                return total;
            }

            total += sent;
        }

        return total;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    private int ReceiveExact(Socket socket, byte[] buffer, int size, int offset = 0)
    {
        var total = 0;
        while (total < size)
        {
            var read = socket.Receive(buffer, offset + total, size - total, SocketFlags.None);
            if (read <= 0)
            {
                return total > 0 ? total : read;
            }

            total += read;
        }

        return total;
    }

    private int ReceiveIsoData(Socket socket, ref byte[] bytes)
    {
        var done = false;
        var size = 0;
        var lastError = 0;

        while (lastError == 0 && !done)
        {
            var headerReceived = ReceiveExact(socket, bytes, 4);
            if (headerReceived != 4)
            {
                lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                break;
            }

            size = Word.FromByteArray(bytes, 2);

            if (size == 7)
            {
                if (ReceiveExact(socket, bytes, 3, 4) != 3)
                {
                    lastError = (int)ErrorCode.WrongNumberReceivedBytes;
                }
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

        if (lastError != 0)
        {
            return 0;
        }

        if (ReceiveExact(socket, bytes, 3, 4) != 3)
        {
            return 0;
        }

        return ReceiveExact(socket, bytes, size - 7, 7) == size - 7 ? size : 0;
    }

    private int ReceiveRaw(Tag? tag, byte[] buffer, int size, int offset = 0)
        => ReceiveCore(tag, buffer, size, offset, traceOperation: false);

    private int ReceiveCore(Tag? tag, byte[] buffer, int size, int offset, bool traceOperation)
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

                if (traceOperation && tag != null && Debugger.IsAttached)
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
    /// Attempts to establish and optimize a connection to a Siemens PLC using multiple TSAP profiles for maximum
    /// compatibility.
    /// </summary>
    /// <remarks>This method tries several known TSAP profiles to improve the likelihood of connecting to
    /// different Siemens PLC models. It configures socket options for optimal performance and handles connection
    /// retries internally. If the connection cannot be established with any profile, the method returns <see
    /// langword="false"/>. This method is thread-safe and should not be called concurrently with other connection
    /// initialization methods.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the connection
    /// is successfully established and initialized; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> InitializeSiemensConnectionOptimizedAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_disposedValue)
            {
                return false;
            }

            if (_initComplete && _socket != null && CheckConnectionStatusOptimized(_socket))
            {
                return true;
            }

            // Try known TSAP profiles to maximize multi-connection compatibility
            var profiles = new[] { TsapProfile.PG, TsapProfile.OP, TsapProfile.PGAlt };

            foreach (var profile in profiles)
            {
                var attemptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Enhanced socket configuration for optimal performance
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DefaultTimeout);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, DefaultTimeout);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                attemptSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                // Set optimal buffer sizes based on PLC type
                var bufferSize = DataReadLength * 2;
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);

                var server = new IPEndPoint(IPAddress.Parse(IP), 102);

#if NETSTANDARD2_0
                var connectTask = Task.Factory.FromAsync(
                    (callback, state) => attemptSocket.BeginConnect(server, callback, state),
                    attemptSocket.EndConnect,
                    null);
                await connectTask.ConfigureAwait(false);
#else
                await attemptSocket.ConnectAsync(server).ConfigureAwait(false);
#endif

                if (!CheckConnectionStatusOptimized(attemptSocket))
                {
                    CloseSocketOptimized(attemptSocket);
                    continue;
                }

                // Handshake with current profile
                if (await PerformOptimizedHandshakeAsync(attemptSocket, profile).ConfigureAwait(false) &&
                    await WaitForPlcReadyAsync(attemptSocket).ConfigureAwait(false))
                {
                    var oldSocket = _socket;
                    _socket = attemptSocket;
                    CloseSocketOptimized(oldSocket);
                    _activeTsapProfile = profile;
                    _initComplete = true;
                    _isConnected = true;
                    _lastSuccessfulOperation = DateTime.UtcNow;
                    _consecutiveErrors = 0;
                    _consecutiveAvailabilityFailures = 0;
                    LogInfo($"Successfully connected to {PLCType} at {IP}:102 with PDU length {DataReadLength}");
                    return true;
                }

                // Handshake failed, try next profile
                CloseSocketOptimized(attemptSocket);
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
    /// Performs an optimized asynchronous handshake using the specified TSAP profile.
    /// </summary>
    /// <param name="profile">The TSAP profile to use for the handshake operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the handshake
    /// succeeds; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeAsync(Socket socket, TsapProfile profile)
    {
        var bReceive = _bufferPool.Rent(256);
        try
        {
#if NETSTANDARD2_0
            return await PerformOptimizedHandshakeNetStandardAsync(socket, bReceive, profile).ConfigureAwait(false);
#else
            return await PerformOptimizedHandshakeModernAsync(socket, bReceive, profile).ConfigureAwait(false);
#endif
        }
        finally
        {
            _bufferPool.Return(bReceive);
        }
    }

#if NETSTANDARD2_0
    private async Task<bool> PerformOptimizedHandshakeNetStandardAsync(Socket socket, byte[] bReceive, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes(profile);
            var sentTask = Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(bSend1, 0, bSend1.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
            var sent = await sentTask.ConfigureAwait(false);
            if (sent != bSend1.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactNetStandardAsync(socket, bReceive, 22).ConfigureAwait(false);

            // minimal TPKT+COTP length sanity
            if (received < 7)
            {
                LogError($"Invalid connection response length {received}");
                return false;
            }

            // Step 3: Communication setup request
            var bSend2 = GetCommunicationSetupBytes();
            sentTask = Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(bSend2, 0, bSend2.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
            sent = await sentTask.ConfigureAwait(false);
            if (sent != bSend2.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactNetStandardAsync(socket, bReceive, 27).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid communication setup response length {received}");
                return false;
            }

            UpdateNegotiatedPduLength(bReceive, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }

    private async Task<int> ReceiveTpktExactNetStandardAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        // Read TPKT header (4 bytes)
        var read = await Task.Factory.FromAsync(
            (cb, s) => socket.BeginReceive(buffer, 0, 4, SocketFlags.None, cb, s),
            socket.EndReceive,
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
                (cb, s) => socket.BeginReceive(buffer, total, remaining, SocketFlags.None, cb, s),
                socket.EndReceive,
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
    /// <summary>
    /// Performs an optimized asynchronous handshake sequence with a remote endpoint using the modern socket API.
    /// </summary>
    /// <remarks>This method sends and receives protocol-specific handshake messages to establish a
    /// connection. If any step in the handshake fails, the method logs an error and returns <see langword="false"/>.
    /// The method does not throw exceptions for handshake failures; instead, it returns <see langword="false"/> to
    /// indicate failure.</remarks>
    /// <param name="bReceive">A buffer used to receive handshake response data from the remote endpoint. Must be large enough to hold the
    /// expected handshake messages.</param>
    /// <param name="profile">The connection profile containing parameters required for the handshake process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the handshake
    /// completes successfully; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeModernAsync(Socket socket, byte[] bReceive, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var bSend1 = GetConnectionRequestBytes(profile);
            var sent = await socket.SendAsync(bSend1, SocketFlags.None).ConfigureAwait(false);
            if (sent != bSend1.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactModernAsync(socket, bReceive, 22).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid connection response length {received}");
                return false;
            }

            // Step 3: Communication setup request
            var bSend2 = GetCommunicationSetupBytes();
            sent = await socket.SendAsync(bSend2, SocketFlags.None).ConfigureAwait(false);
            if (sent != bSend2.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactModernAsync(socket, bReceive, 27).ConfigureAwait(false);
            if (received < 7)
            {
                LogError($"Invalid communication setup response length {received}");
                return false;
            }

            UpdateNegotiatedPduLength(bReceive, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Asynchronously receives a complete TPKT packet from the underlying socket into the specified buffer.
    /// </summary>
    /// <remarks>The method reads the 4-byte TPKT header to determine the packet length, then reads the
    /// remaining data to complete the packet. If the actual packet length is less than the specified minimum, a warning
    /// is logged but the packet is still read. The method returns as soon as the full packet is received or if the
    /// connection is closed before completion.</remarks>
    /// <param name="buffer">The buffer that receives the TPKT packet data. Must be large enough to hold the entire packet.</param>
    /// <param name="expectedMin">The minimum expected length of the TPKT packet, in bytes. Used for validation; set to 0 to disable the check.</param>
    /// <returns>The total number of bytes read into the buffer, or a value less than 4 if the TPKT header could not be read.</returns>
    private async Task<int> ReceiveTpktExactModernAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        // Read TPKT header (4 bytes)
        var headerRead = await socket.ReceiveAsync(buffer.AsMemory(0, 4), SocketFlags.None).ConfigureAwait(false);
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
            var r = await socket.ReceiveAsync(buffer.AsMemory(total, remaining), SocketFlags.None).ConfigureAwait(false);
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

    private async Task ProbeAvailabilityAndNotifyAsync(IObserver<bool> observer)
    {
        var isReachable = await CheckAvailabilityOptimizedAsync().ConfigureAwait(false);
        if (isReachable)
        {
            _consecutiveAvailabilityFailures = 0;
            _isAvailable = true;
        }
        else if (_initComplete && _isConnected == true)
        {
            _consecutiveAvailabilityFailures++;

            if (_consecutiveAvailabilityFailures < AvailabilityFailureThreshold)
            {
                _isAvailable = true;
            }
            else
            {
                _isAvailable = false;
                LogWarning($"Availability probe failed {_consecutiveAvailabilityFailures} times consecutively. Marking PLC unavailable.");
            }
        }
        else
        {
            _consecutiveAvailabilityFailures = AvailabilityFailureThreshold;
            _isAvailable = false;
        }

        observer.OnNext(_isAvailable == true);
    }

    private bool EvaluateConnectionStateWithHysteresis()
    {
        if (!_initComplete)
        {
            _consecutiveConnectionFailures = 0;
            return false;
        }

        var isConnectedNow = CheckConnectionStatusOptimized();
        if (isConnectedNow)
        {
            _consecutiveConnectionFailures = 0;
            return true;
        }

        _consecutiveConnectionFailures++;

        if (_initComplete && _isConnected == true && _consecutiveConnectionFailures < ConnectionFailureThreshold)
        {
            return true;
        }

        if (_initComplete && _consecutiveConnectionFailures == ConnectionFailureThreshold)
        {
            LogWarning($"Connection probe failed {_consecutiveConnectionFailures} times consecutively. Restarting connection.");
            RestartConnection();
        }

        return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckConnectionStatusOptimized(Socket socket)
    {
        try
        {
            return socket.Connected &&
                !(socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateNegotiatedPduLength(byte[] response, int responseLength)
    {
        if (responseLength < 27)
        {
            return;
        }

        var negotiatedPduLength = Word.FromByteArray(response, 25);
        if (negotiatedPduLength is < 240 or > 4096)
        {
            return;
        }

        if (negotiatedPduLength != DataReadLength)
        {
            LogInfo($"Negotiated PDU length updated to {negotiatedPduLength} bytes.");
        }

        DataReadLength = negotiatedPduLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReceiveExact(Tag tag, byte[] buffer, int size, int offset = 0)
    {
        var total = 0;
        while (total < size)
        {
            var read = ReceiveRaw(tag, buffer, size - total, offset + total);
            if (read <= 0)
            {
                return total > 0 ? total : read;
            }

            total += read;
        }

        return total;
    }

    /// <summary>
    /// Builds a connection request message as a byte array using the specified TSAP profile settings.
    /// </summary>
    /// <remarks>The generated message is compatible with S7-1200, S7-300, and S7-400 devices, using a TPDU
    /// size of 512 bytes. The TSAP values are set based on the properties of the supplied profile.</remarks>
    /// <param name="profile">The TSAP profile containing source and destination transport service access point (TSAP) values used to
    /// construct the connection request.</param>
    /// <returns>A byte array representing the connection request message, with TSAP fields set according to the provided
    /// profile.</returns>
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
    /// Creates and returns a byte array containing the communication setup parameters for the PLC connection.
    /// </summary>
    /// <remarks>The returned array includes protocol-specific configuration values, including the optimal PDU
    /// length for the target PLC type. This method is intended for internal use when establishing or configuring a PLC
    /// communication session.</remarks>
    /// <returns>A byte array representing the communication setup message to be sent to the PLC.</returns>
    private byte[] GetCommunicationSetupBytes()
    {
        byte[] bSend2 = [3, 0, 0, 25, 2, 240, 128, 50, 1, 0, 0, 4, 0, 0, 8, 0, 0, 240, 0, 0, 1, 0, 1, 0, 30];

        // Set optimal PDU length for the specific PLC type
        Word.ToByteArray(DataReadLength, bSend2, 23);

        return bSend2;
    }

    /// <summary>
    /// Records a successful send or receive operation, updating internal metrics and error counters.
    /// </summary>
    /// <param name="duration">The duration of the completed operation.</param>
    /// <param name="bytes">The number of bytes processed during the operation.</param>
    /// <param name="isReceive">true to record a receive operation; false to record a send operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordSuccessfulOperation(TimeSpan duration, int bytes, bool isReceive)
    {
        _lastSuccessfulOperation = DateTime.UtcNow;
        _consecutiveErrors = 0;
        _consecutiveAvailabilityFailures = 0;

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
    /// Records an error occurrence and updates internal error tracking state.
    /// </summary>
    /// <remarks>If the number of consecutive errors exceeds a predefined threshold, this method initiates a
    /// connection restart to recover from persistent failures.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordError()
    {
        _consecutiveErrors++;
        _metrics.RecordError();

        // Trigger connection restart if too many consecutive errors
        if (_consecutiveErrors == 6)
        {
            LogWarning($"Excessive failures detected: {_consecutiveErrors}. Restarting connection.");
            RestartConnection();
        }
    }

    /// <summary>
    /// Attempts to asynchronously restart the network connection after a failure.
    /// </summary>
    /// <remarks>This method initiates a background task to close the current socket, reset connection state,
    /// and re-establish the connection. If the object has been disposed, the operation is not performed. Any exceptions
    /// encountered during the restart process are logged. This method is intended for internal use and is not
    /// thread-safe.</remarks>
    private void RestartConnection() =>
        Task.Run(async () =>
        {
            if (_disposedValue)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                LogWarning("Restarting connection due to failures");
                var socket = _socket;
                _socket = null;
                _initComplete = false;
                _isConnected = false;
                CloseSocketOptimized(socket);

                await Task.Delay(1000).ConfigureAwait(false);

                if (!_disposedValue)
                {
                    _disposable?.Dispose();
                    _disposable = Connect.Subscribe();
                }
            }
            catch (Exception ex)
            {
                LogError($"Connection restart failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _restartInProgress, 0);
            }
        });

    /// <summary>
    /// Reports the current set of collected metrics to subscribed observers.
    /// </summary>
    /// <remarks>This method is typically intended to be used as a callback for timer or scheduling mechanisms
    /// that require a method signature accepting a state parameter. Any exceptions encountered during reporting are
    /// logged and do not propagate to the caller.</remarks>
    /// <param name="state">An optional state object provided by the timer or scheduler. This parameter is not used.</param>
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

    /// <summary>
    /// Determines the optimal data read length, in bytes, for the specified PLC type.
    /// </summary>
    /// <remarks>The returned value is based on typical performance characteristics and protocol limitations
    /// for each supported PLC type. Using the optimal read length can improve communication efficiency and reduce the
    /// number of required read operations.</remarks>
    /// <param name="plcType">The type of PLC for which to determine the optimal data read length.</param>
    /// <returns>A 16-bit unsigned integer representing the recommended number of bytes to read in a single operation for the
    /// specified PLC type.</returns>
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

    /// <summary>
    /// Closes and disposes the specified socket, suppressing any exceptions that may occur during shutdown or disposal.
    /// </summary>
    /// <remarks>This method attempts to gracefully shut down the socket if it is connected, then closes and
    /// disposes it. Any exceptions thrown during shutdown, close, or dispose operations are caught and ignored to
    /// ensure that the method does not throw.</remarks>
    /// <param name="socket">The socket to close and dispose. If null, the method performs no action.</param>
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

    /// <summary>
    /// Logs an error message to the debug output when a debugger is attached.
    /// </summary>
    /// <remarks>This method writes the error message to the debug output window only if a debugger is
    /// currently attached. It does not persist the message or display it in production environments.</remarks>
    /// <param name="message">The error message to log. Cannot be null.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogError(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    /// <summary>
    /// Writes an informational message to the debug output when a debugger is attached.
    /// </summary>
    /// <remarks>This method has no effect if a debugger is not attached. The message is prefixed with a
    /// timestamp and an [INFO] label for clarity in the debug output.</remarks>
    /// <param name="message">The informational message to write to the debug output.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance methods to satisfy StyleCop member ordering without large refactor in a hot codepath.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LogInfo(string message)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
        }
    }

    /// <summary>
    /// Writes a warning message to the debug output when a debugger is attached.
    /// </summary>
    /// <remarks>This method has no effect if a debugger is not attached. The message is prefixed with a
    /// timestamp and a warning label for easier identification in the debug output.</remarks>
    /// <param name="message">The warning message to write to the debug output.</param>
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
    /// Asynchronously checks whether the configured IP address is reachable using an optimized ping operation.
    /// </summary>
    /// <remarks>Returns <see langword="false"/> if the IP address is not set or is invalid, or if the ping
    /// operation fails due to network errors or exceptions.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the IP address
    /// responds successfully to a ping request; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> CheckAvailabilityOptimizedAsync()
    {
        if (string.IsNullOrWhiteSpace(IP))
        {
            return false;
        }

        if (_initComplete && _socket != null && CheckConnectionStatusOptimized(_socket) &&
            DateTime.UtcNow - _lastSuccessfulOperation <= RecentOperationAvailabilityWindow)
        {
            return true;
        }

        try
        {
            using var ping = new Ping();
            var result = await ping.SendPingAsync(IP, PingTimeoutMs).ConfigureAwait(false);
            if (result.Status == IPStatus.Success)
            {
                return true;
            }
        }
        catch (PingException)
        {
        }
        catch (Exception ex)
        {
            LogError($"Ping failed: {ex.Message}");
        }

        return await CheckPortAvailabilityAsync().ConfigureAwait(false);
    }

    private async Task<bool> CheckPortAvailabilityAsync()
    {
        using var probeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probeSocket.Blocking = false;

        try
        {
            var server = new IPEndPoint(IPAddress.Parse(IP), 102);
#if NETSTANDARD2_0
            var connectTask = Task.Factory.FromAsync(
                (callback, state) => probeSocket.BeginConnect(server, callback, state),
                probeSocket.EndConnect,
                null);
            var timeoutTask = Task.Delay(PortProbeTimeoutMs);
            var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
            return completedTask == connectTask && probeSocket.Connected;
#else
            using var cts = new CancellationTokenSource(PortProbeTimeoutMs);
            await probeSocket.ConnectAsync(server, cts.Token).ConfigureAwait(false);
            return probeSocket.Connected;
#endif
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the underlying socket connection is currently active, using an optimized check to minimize
    /// overhead.
    /// </summary>
    /// <remarks>This method performs a lightweight check to verify the connection status. If the connection
    /// appears active but has not been used for more than two minutes, an additional lightweight operation is performed
    /// to confirm connectivity. If the connection is found to be inactive and initialization is complete, the
    /// connection is automatically restarted. This method is intended for internal use to efficiently monitor
    /// connection health.</remarks>
    /// <returns>true if the connection is considered active; otherwise, false.</returns>
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
    /// Performs a lightweight check to determine whether the underlying socket connection is still valid.
    /// </summary>
    /// <remarks>This method attempts to set a socket option as a way to verify the connection's health
    /// without sending data. It does not guarantee that the connection is fully operational, but can be used as a quick
    /// check before performing more expensive operations.</remarks>
    /// <returns>true if the connection check succeeds; otherwise, false.</returns>
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
    /// Represents a Transport Service Access Point (TSAP) addressing profile used for establishing communication with
    /// Siemens S7 devices.
    /// </summary>
    /// <remarks>TSAP profiles define how endpoints are addressed when connecting to Siemens S7 PLCs.
    /// Predefined profiles such as PG, OP, and PGAlt are provided for common communication scenarios. Use the
    /// appropriate profile based on the type of device or connection required.</remarks>
    /// <param name="SrcHi">The high byte of the source TSAP address.</param>
    /// <param name="SrcLo">The low byte of the source TSAP address.</param>
    /// <param name="DstHi">The high byte of the destination TSAP address.</param>
    /// <param name="DstLo">A function that computes the low byte of the destination TSAP address based on the rack and slot numbers.</param>
    /// <param name="Name">The name that identifies the TSAP profile.</param>
    private readonly record struct TsapProfile(byte SrcHi, byte SrcLo, byte DstHi, Func<short, short, byte> DstLo, string Name)
    {
        /// <summary>
        /// Gets the TSAP profile for the "PG" (Programming Device) connection type.
        /// </summary>
        /// <remarks>This profile is typically used when establishing a connection to a PLC as a
        /// programming device. The PG profile may have different access rights or communication behavior compared to
        /// other TSAP profiles, depending on the PLC configuration.</remarks>
        public static TsapProfile PG => new(0x01, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "PG");

        /// <summary>
        /// Gets the TSAP profile for the "OP" (Operator Panel) communication type.
        /// </summary>
        /// <remarks>Use this profile when establishing a connection that requires the Operator Panel TSAP
        /// settings. The profile includes predefined parameters suitable for typical OP communication
        /// scenarios.</remarks>
        public static TsapProfile OP => new(0x02, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "OP");

        /// <summary>
        /// Gets the TSAP profile for the PGAlt (Programming Device Alternative) connection type.
        /// </summary>
        public static TsapProfile PGAlt => new(0x10, 0x00, 0x03, (rack, slot) => (byte)((rack * 2 * 16) + slot), "PGAlt");
    }
}
