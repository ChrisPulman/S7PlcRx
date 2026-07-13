// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
#endif
using DateTime = System.DateTime;
using TimeSpan = System.TimeSpan;
using Timer = System.Threading.Timer;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

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
    /// <summary>Defines the f ai l e d value.</summary>
    private const string Failed = nameof(Failed);

    /// <summary>Defines the successful operation status.</summary>
    private const string Success = nameof(Success);

    /// <summary>Message emitted when an operation requires a connected device.</summary>
    private const string DeviceNotConnectedMessage = "Device not connected";

    /// <summary>Defines the d ef au lt ti me o u t value.</summary>
    private const int DefaultTimeout = 10_000;

    /// <summary>Defines the i ni ti al re tr yd el ay mi ll is ec on d s value.</summary>
    private const int InitialRetryDelayMilliseconds = 250;

    /// <summary>Defines the a xr et ry de la ym il li se co n d s value.</summary>
    private const int MaxRetryDelayMilliseconds = 5_000;

    /// <summary>Defines the p in gt im eo ut m s value.</summary>
    private const int PingTimeoutMs = 2_000;

    /// <summary>Defines the p or tp ro be ti me ou t m s value.</summary>
    private const int PortProbeTimeoutMs = 750;

    /// <summary>Defines the a va il ab il it yf ai lu re th re sh o l d value.</summary>
    private const int AvailabilityFailureThreshold = 3;

    /// <summary>Defines the c on ne ct io nf ai lu re th re sh o l d value.</summary>
    private const int ConnectionFailureThreshold = 3;

    /// <summary>Connection monitoring and retry policy.</summary>
    private const int MetricsReportIntervalSeconds = 30;

    /// <summary>Defines the Retry Backoff Maximum Exponent value.</summary>
    private const int RetryBackoffMaximumExponent = 4;

    /// <summary>Defines the Initial Retry Attempts To Log value.</summary>
    private const int InitialRetryAttemptsToLog = 5;

    /// <summary>Defines the Retry Logging Interval value.</summary>
    private const int RetryLoggingInterval = 10;

    /// <summary>Defines the Availability Startup Probe Interval Milliseconds value.</summary>
    private const int AvailabilityStartupProbeIntervalMilliseconds = 250;

    /// <summary>Defines the Availability Startup Probe Count value.</summary>
    private const int AvailabilityStartupProbeCount = 8;

    /// <summary>Defines the Connection Startup Probe Interval Milliseconds value.</summary>
    private const int ConnectionStartupProbeIntervalMilliseconds = 50;

    /// <summary>Defines the Socket Poll Microseconds value.</summary>
    private const int SocketPollMicroseconds = 1_000;

    /// <summary>Defines the Connection Idle Check Minutes value.</summary>
    private const int ConnectionIdleCheckMinutes = 2;

    /// <summary>Defines the Connection Restart Delay Milliseconds value.</summary>
    private const int ConnectionRestartDelayMilliseconds = 1_000;

    /// <summary>Defines the Consecutive Error Restart Threshold value.</summary>
    private const int ConsecutiveErrorRestartThreshold = 6;

    /// <summary>ISO-on-TCP and S7 protocol framing.</summary>
    private const int S7TcpPort = 102;

    /// <summary>Defines the Tpkt Header Length value.</summary>
    private const int TpktHeaderLength = 4;

    /// <summary>Defines the Tpkt Length High Byte Offset value.</summary>
    private const int TpktLengthHighByteOffset = 2;

    /// <summary>Defines the Tpkt Length Low Byte Offset value.</summary>
    private const int TpktLengthLowByteOffset = 3;

    /// <summary>Defines the Bits Per Byte value.</summary>
    private const int BitsPerByte = 8;

    /// <summary>Defines the Cotp Data Header Length value.</summary>
    private const int CotpDataHeaderLength = 3;

    /// <summary>Defines the Iso Data Header Length value.</summary>
    private const int IsoDataHeaderLength = 7;

    /// <summary>Defines the Minimum Iso Packet Length value.</summary>
    private const int MinimumIsoPacketLength = 16;

    /// <summary>Defines the Connection Response Length value.</summary>
    private const int ConnectionResponseLength = 22;

    /// <summary>Defines the Communication Setup Response Length value.</summary>
    private const int CommunicationSetupResponseLength = 27;

    /// <summary>Defines the Handshake Receive Buffer Size value.</summary>
    private const int HandshakeReceiveBufferSize = 256;

    /// <summary>Defines the Minimum Negotiated Pdu Length value.</summary>
    private const ushort MinimumNegotiatedPduLength = 240;

    /// <summary>Defines the Maximum Negotiated Pdu Length value.</summary>
    private const ushort MaximumNegotiatedPduLength = 4_096;

    /// <summary>Defines the Negotiated Pdu Length Offset value.</summary>
    private const int NegotiatedPduLengthOffset = 25;

    /// <summary>Defines the Socket Buffer Pdu Multiplier value.</summary>
    private const int SocketBufferPduMultiplier = 2;

    /// <summary>Defines the S7 Response Return Code Offset value.</summary>
    private const int S7ResponseReturnCodeOffset = 21;

    /// <summary>Supported controller PDU sizes.</summary>
    private const ushort LogoPduLength = 240;

    /// <summary>Defines the Standard Pdu Length value.</summary>
    private const ushort StandardPduLength = 480;

    /// <summary>Defines the Extended Pdu Length value.</summary>
    private const ushort ExtendedPduLength = 960;

    /// <summary>Defines the High Performance Pdu Length value.</summary>
    private const ushort HighPerformancePduLength = 1_440;

    /// <summary>SZL request and response layout.</summary>
    private const int SzlBufferSize = 1_024;

    /// <summary>Defines the Szl Request Sequence Offset value.</summary>
    private const int SzlRequestSequenceOffset = 11;

    /// <summary>Defines the Szl Area Offset value.</summary>
    private const int SzlAreaOffset = 29;

    /// <summary>Defines the Szl Index Offset value.</summary>
    private const int SzlIndexOffset = 31;

    /// <summary>Defines the Szl Continuation Sequence Offset value.</summary>
    private const int SzlContinuationSequenceOffset = 24;

    /// <summary>Defines the Minimum Szl Response Length value.</summary>
    private const int MinimumSzlResponseLength = 32;

    /// <summary>Defines the Szl Error Code Offset value.</summary>
    private const int SzlErrorCodeOffset = 27;

    /// <summary>Defines the Szl Return Code Offset value.</summary>
    private const int SzlReturnCodeOffset = 29;

    /// <summary>Defines the S7 Return Code Success value.</summary>
    private const byte S7ReturnCodeSuccess = 0xff;

    /// <summary>Defines the Szl First Payload Offset value.</summary>
    private const int SzlFirstPayloadOffset = 41;

    /// <summary>Defines the Szl Continuation Payload Offset value.</summary>
    private const int SzlContinuationPayloadOffset = 37;

    /// <summary>Defines the Szl Data Length Offset value.</summary>
    private const int SzlDataLengthOffset = 31;

    /// <summary>Defines the Szl First Packet Metadata Length value.</summary>
    private const int SzlFirstPacketMetadataLength = 8;

    /// <summary>Defines the Szl Last Data Unit Offset value.</summary>
    private const int SzlLastDataUnitOffset = 26;

    /// <summary>Defines the Szl Sequence Offset value.</summary>
    private const int SzlSequenceOffset = 24;

    /// <summary>Defines the Szl Total Length Offset value.</summary>
    private const int SzlTotalLengthOffset = 37;

    /// <summary>S7 telegram byte values.</summary>
    private const byte TpktVersion = 0x03;

    /// <summary>Defines the Szl Telegram Length value.</summary>
    private const byte SzlTelegramLength = 0x21;

    /// <summary>Defines the Connection Request Telegram Length value.</summary>
    private const byte ConnectionRequestTelegramLength = 0x16;

    /// <summary>Defines the Communication Setup Telegram Length value.</summary>
    private const byte CommunicationSetupTelegramLength = 0x19;

    /// <summary>Defines the Cotp Data Header Size value.</summary>
    private const byte CotpDataHeaderSize = 0x02;

    /// <summary>Defines the Cotp Data Pdu Type value.</summary>
    private const byte CotpDataPduType = 0xf0;

    /// <summary>Defines the Cotp End Of Transmission Unit value.</summary>
    private const byte CotpEndOfTransmissionUnit = 0x80;

    /// <summary>Defines the Cotp Connection Request Header Size value.</summary>
    private const byte CotpConnectionRequestHeaderSize = 0x11;

    /// <summary>Defines the Cotp Connection Request Pdu Type value.</summary>
    private const byte CotpConnectionRequestPduType = 0xe0;

    /// <summary>Defines the Cotp Source Reference Low Byte value.</summary>
    private const byte CotpSourceReferenceLowByte = 0x2e;

    /// <summary>Defines the Cotp Source Tsap Parameter Code value.</summary>
    private const byte CotpSourceTsapParameterCode = 0xc1;

    /// <summary>Defines the Cotp Destination Tsap Parameter Code value.</summary>
    private const byte CotpDestinationTsapParameterCode = 0xc2;

    /// <summary>Defines the Cotp Tpdu Size Parameter Code value.</summary>
    private const byte CotpTpduSizeParameterCode = 0xc0;

    /// <summary>Defines the Cotp Tsap Parameter Length value.</summary>
    private const byte CotpTsapParameterLength = 0x02;

    /// <summary>Defines the Cotp Tpdu Size Parameter Length value.</summary>
    private const byte CotpTpduSizeParameterLength = 0x01;

    /// <summary>Defines the Cotp Tpdu Size512 Bytes value.</summary>
    private const byte CotpTpduSize512Bytes = 0x09;

    /// <summary>Defines the S7 Protocol Identifier value.</summary>
    private const byte S7ProtocolIdentifier = 0x32;

    /// <summary>Defines the S7 User Data Message Type value.</summary>
    private const byte S7UserDataMessageType = 0x07;

    /// <summary>Defines the S7 Job Message Type value.</summary>
    private const byte S7JobMessageType = 0x01;

    /// <summary>Defines the Szl First Pdu Reference value.</summary>
    private const byte SzlFirstPduReference = 0x05;

    /// <summary>Defines the Szl Continuation Pdu Reference value.</summary>
    private const byte SzlContinuationPduReference = 0x06;

    /// <summary>Defines the Szl First Parameter Length value.</summary>
    private const byte SzlFirstParameterLength = 0x08;

    /// <summary>Defines the Szl Continuation Parameter Length value.</summary>
    private const byte SzlContinuationParameterLength = 0x0c;

    /// <summary>Defines the Szl First Data Length value.</summary>
    private const byte SzlFirstDataLength = 0x08;

    /// <summary>Defines the Szl Continuation Data Length value.</summary>
    private const byte SzlContinuationDataLength = 0x04;

    /// <summary>Defines the S7 User Data Parameter Head value.</summary>
    private const byte S7UserDataParameterHead = 0x12;

    /// <summary>Defines the Szl First Parameter Payload Length value.</summary>
    private const byte SzlFirstParameterPayloadLength = 0x04;

    /// <summary>Defines the Szl Continuation Parameter Payload Length value.</summary>
    private const byte SzlContinuationParameterPayloadLength = 0x08;

    /// <summary>Defines the Szl Read Request value.</summary>
    private const byte SzlReadRequest = 0x11;

    /// <summary>Defines the Szl Read Response value.</summary>
    private const byte SzlReadResponse = 0x12;

    /// <summary>Defines the Szl Function Group value.</summary>
    private const byte SzlFunctionGroup = 0x44;

    /// <summary>Defines the Szl Subfunction value.</summary>
    private const byte SzlSubfunction = 0x01;

    /// <summary>Defines the Szl Continuation Flag value.</summary>
    private const byte SzlContinuationFlag = 0x01;

    /// <summary>Defines the S7 Octet String Transport Size value.</summary>
    private const byte S7OctetStringTransportSize = 0x09;

    /// <summary>Defines the Szl Request Data Length value.</summary>
    private const byte SzlRequestDataLength = 0x04;

    /// <summary>Defines the Szl Continuation Data Bit Length value.</summary>
    private const byte SzlContinuationDataBitLength = 0x0a;

    /// <summary>Defines the Communication Setup Pdu Reference value.</summary>
    private const byte CommunicationSetupPduReference = 0x04;

    /// <summary>Defines the Communication Setup Parameter Length value.</summary>
    private const byte CommunicationSetupParameterLength = 0x08;

    /// <summary>Defines the S7 Setup Communication Function value.</summary>
    private const byte S7SetupCommunicationFunction = 0xf0;

    /// <summary>Defines the Default Requested Pdu Length Low Byte value.</summary>
    private const byte DefaultRequestedPduLengthLowByte = 0x1e;

    /// <summary>Defines the Communication Setup Pdu Length Offset value.</summary>
    private const int CommunicationSetupPduLengthOffset = 23;

    /// <summary>Defines the Connection Request Source Tsap High Offset value.</summary>
    private const int ConnectionRequestSourceTsapHighOffset = 13;

    /// <summary>Defines the Connection Request Source Tsap Low Offset value.</summary>
    private const int ConnectionRequestSourceTsapLowOffset = 14;

    /// <summary>Defines the Connection Request Destination Tsap High Offset value.</summary>
    private const int ConnectionRequestDestinationTsapHighOffset = 17;

    /// <summary>Defines the Connection Request Destination Tsap Low Offset value.</summary>
    private const int ConnectionRequestDestinationTsapLowOffset = 18;

    /// <summary>TSAP address encoding.</summary>
    private const int RackAddressMultiplier = 2;

    /// <summary>Defines the Slots Per Rack Address Unit value.</summary>
    private const int SlotsPerRackAddressUnit = 16;

    /// <summary>Defines the Programming Device Source Tsap High Byte value.</summary>
    private const byte ProgrammingDeviceSourceTsapHighByte = 0x01;

    /// <summary>Defines the Operator Panel Source Tsap High Byte value.</summary>
    private const byte OperatorPanelSourceTsapHighByte = 0x02;

    /// <summary>Defines the Alternate Programming Device Source Tsap High Byte value.</summary>
    private const byte AlternateProgrammingDeviceSourceTsapHighByte = 0x10;

    /// <summary>Defines the Default Source Tsap Low Byte value.</summary>
    private const byte DefaultSourceTsapLowByte = 0x00;

    /// <summary>Defines the Default Destination Tsap High Byte value.</summary>
    private const byte DefaultDestinationTsapHighByte = 0x03;

    /// <summary>Stores the r ec en to pe ra ti on av ai la bi li ty wi nd o w value.</summary>
    private static readonly TimeSpan RecentOperationAvailabilityWindow = TimeSpan.FromSeconds(3);

    /// <summary>Stores the e tr ic ss ub je c t used by this instance.</summary>
    private readonly Signal<ConnectionMetrics> _metricsSubject = new();

    /// <summary>Stores the c on ne ct io nl o c k used by this instance.</summary>
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>Stores the shared byte-buffer pool used by this instance.</summary>
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    /// <summary>Stores the e tr i c s used by this instance.</summary>
    private readonly ConnectionMetrics _metrics = new();

    /// <summary>Stores the e tr ic st im e r used by this instance.</summary>
    private readonly Timer? _metricsTimer;

    /// <summary>Stores the s oc ke te xc ep ti on su bj e c t used by this instance.</summary>
    private Signal<Exception> _socketExceptionSubject = new();

    /// <summary>Stores the d is po sa b l e used by this instance.</summary>
    private IDisposable _disposable;

    /// <summary>Stores the d is po se dv al u e used by this instance.</summary>
    private bool _disposedValue;

    /// <summary>Stores the i ni tc om pl e t e used by this instance.</summary>
    private bool _initComplete;

    /// <summary>Stores the i sa va il ab l e used by this instance.</summary>
    private bool? _isAvailable;

    /// <summary>Stores the i sc on ne ct e d used by this instance.</summary>
    private bool? _isConnected;

    /// <summary>Stores the s oc k e t used by this instance.</summary>
    private Socket? _socket;

    /// <summary>Stores the l as ts uc ce ss fu lo pe ra ti o n used by this instance.</summary>
    private DateTime _lastSuccessfulOperation = DateTime.MinValue;

    /// <summary>Stores the number of consecutive socket errors.</summary>
    private int _consecutiveErrors;

    /// <summary>Stores the number of consecutive availability-check failures.</summary>
    private int _consecutiveAvailabilityFailures;

    /// <summary>Stores the number of consecutive connection failures.</summary>
    private int _consecutiveConnectionFailures;

    /// <summary>Indicates whether a restart operation is in progress.</summary>
    private int _restartInProgress;

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
        _metricsTimer = new(
            _ => ReportMetrics(),
            null,
            TimeSpan.FromSeconds(MetricsReportIntervalSeconds),
            TimeSpan.FromSeconds(MetricsReportIntervalSeconds));

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
                    if (ex is null)
                    {
                        return;
                    }

                    _metrics.RecordError();
                    LogError($"Socket exception: {ex.Message}");
                    obs.OnError(ex);
                }));
            }
            catch (ObjectDisposedException)
            {
                _socketExceptionSubject = new();
                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex is null)
                    {
                        return;
                    }

                    _metrics.RecordError();
                    LogError($"Socket exception: {ex.Message}");
                    obs.OnError(ex);
                }));
            }

            dis.Add(IsConnected.Subscribe(
                deviceConnected =>
                {
                    var isAvail = _isAvailable == true;
                    obs.OnNext(isAvail && deviceConnected);
                    if (!_initComplete || deviceConnected)
                    {
                        return;
                    }

                    CloseSocketOptimized(_socket);
                    _socket = null;
                    obs.OnError(new S7Exception(DeviceNotConnectedMessage));
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
                        if (_isAvailable is not null)
                        {
                            var isAvail = _isAvailable is not null && _isAvailable.HasValue && _isAvailable.Value;
                            if (isAvail)
                            {
                                if (!_initComplete && !await InitializeSiemensConnectionOptimizedAsync())
                                {
                                    CloseSocketOptimized(_socket);
                                    _socket = null;
                                    obs.OnError(new S7Exception(DeviceNotConnectedMessage));
                                    return;
                                }

                                var isCon = _isConnected is not null && _isConnected.HasValue && _isConnected.Value;
                                if (_initComplete && !isCon)
                                {
                                    CloseSocketOptimized(_socket);
                                    _socket = null;
                                    obs.OnError(new S7Exception(DeviceNotConnectedMessage));
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
        .RetryWithDelay(int.MaxValue, index =>
        {
            // Exponential backoff with cap: 1s, 2s, 4s, 8s, 16s, 30s, 30s...
            // Use bit shifting for better performance and prevent overflow
            var exponent = Math.Min(index, RetryBackoffMaximumExponent);
            var delayMilliseconds = Math.Min(InitialRetryDelayMilliseconds * (1 << exponent), MaxRetryDelayMilliseconds);

            // Log only first 5 attempts and then every 10th attempt to prevent log flooding
            if (index < InitialRetryAttemptsToLog || index % RetryLoggingInterval == 0)
            {
                LogWarning($"Connection attempt {index + 1} failed. Retrying in {delayMilliseconds}ms...");
            }

            return TimeSpan.FromMilliseconds(delayMilliseconds);
        })
        .ReplayLastOnSubscribe(false);

    /// <summary>Gets the IP address associated with the current instance.</summary>
    public string IP { get; }

    /// <summary>Gets the optimized data read length based on PLC type capabilities.</summary>
    public ushort DataReadLength { get; private set; }

    /// <summary>Gets an observable sequence that indicates whether the resource is currently available.</summary>
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

            // Fast probe (startup)
            SerialDisposable? timer = null;
            timer = new SerialDisposable
            {
                Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(AvailabilityStartupProbeIntervalMilliseconds)).Subscribe(async _ =>
                {
                    count++;
                    await ProbeAvailabilityAndNotifyAsync(obs).ConfigureAwait(false);

                    // After a few quick probes, back off to reduce ping noise.
                    if (count < AvailabilityStartupProbeCount)
                    {
                        return;
                    }

                    count = 0;
                    timer!.Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(async __ => await ProbeAvailabilityAndNotifyAsync(obs).ConfigureAwait(false));
                })
            };

            return timer;
        }).OnErrorRetry().ReplayLastOnSubscribe(false);

    /// <summary>Gets an observable sequence that indicates whether the connection to the remote endpoint is currently established.</summary>
    /// <remarks>The observable emits a value whenever the connection status changes. Subscribers receive <see
    /// langword="true"/> when the connection is established and <see langword="false"/> when it is lost. The sequence
    /// emits the current status immediately upon subscription and continues to provide updates as the connection state
    /// changes. The observable is shared among all subscribers and only emits distinct consecutive values.</remarks>
    public IObservable<bool> IsConnected =>
        Observable.Create<bool>(obs =>
        {
            _isConnected = null;

            // Faster startup: check frequently until connected, then slow down.
            SerialDisposable? timer = null;
            timer = new SerialDisposable
            {
                Disposable = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(ConnectionStartupProbeIntervalMilliseconds)).Subscribe(_ =>
                {
                    _isConnected = EvaluateConnectionStateWithHysteresis();
                    var isConnectedNow = _initComplete && _isConnected == true;
                    obs.OnNext(isConnectedNow);

                    if (!isConnectedNow)
                    {
                        return;
                    }

                    // Switch to steady-state checks.
                    timer!.Disposable = Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)).Subscribe(__ =>
                    {
                        _isConnected = EvaluateConnectionStateWithHysteresis();
                        obs.OnNext(_initComplete && _isConnected == true);
                    });
                })
            };

            return timer;
        }).OnErrorRetry().ReplayLastOnSubscribe(false).DistinctUntilChanged();

    /// <summary>Gets the type of PLC (Programmable Logic Controller) associated with this instance.</summary>
    public CpuType PLCType { get; }

    /// <summary>Gets the rack number associated with the device or connection.</summary>
    public short Rack { get; }

    /// <summary>Gets the slot number associated with this instance.</summary>
    public short Slot { get; }

    /// <summary>Gets an observable sequence that provides real-time connection metrics.</summary>
    /// <remarks>Subscribers receive updates whenever new connection metrics are available. The sequence
    /// completes when the underlying connection is closed or disposed.</remarks>
    public IObservable<ConnectionMetrics> Metrics => _metricsSubject;

    /// <summary>Receives data from the connected device and writes it into the specified buffer.</summary>
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

    /// <summary>Sends data to the connected device using the specified tag and buffer.</summary>
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

                if (tag is not null && Debugger.IsAttached)
                {
                    var result = sent == size ? Success : Failed;
                    Debug.WriteLine($"{DateTime.Now} Wrote Tag: {tag.Name} value: {tag.Value} {result} ({sent}/{size} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return sent;
            }

            RecordError();
            _socketExceptionSubject.OnNext(new S7Exception(DeviceNotConnectedMessage));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Retrieves System-Zustandsliste (SZL) data from the PLC for the specified SZL area and index.</summary>
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
        byte[] s7_SZL1 =
        [
            TpktVersion, 0, 0, SzlTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7UserDataMessageType, 0, 0, SzlFirstPduReference,
            0, 0, SzlFirstParameterLength, 0, SzlFirstDataLength,
            0, 1, S7UserDataParameterHead, SzlFirstParameterPayloadLength, SzlReadRequest,
            SzlFunctionGroup, SzlSubfunction, 0, S7ReturnCodeSuccess, S7OctetStringTransportSize,
            0, SzlRequestDataLength, 0, 0, 0, 0
        ];
        byte[] s7_SZL2 =
        [
            TpktVersion, 0, 0, SzlTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7UserDataMessageType, 0, 0, SzlContinuationPduReference,
            0, 0, SzlContinuationParameterLength, 0, SzlContinuationDataLength,
            0, 1, S7UserDataParameterHead, SzlContinuationParameterPayloadLength, SzlReadResponse,
            SzlFunctionGroup, SzlSubfunction, SzlContinuationFlag, 0, 0, 0, 0,
            SzlContinuationDataBitLength, 0, 0, 0
        ];

        var data = _bufferPool.Rent(SzlBufferSize);
        var resultData = _bufferPool.Rent(SzlBufferSize);

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
            do
            {
                if (first)
                {
                    Word.ToByteArray(++seqOut, s7_SZL1, SzlRequestSequenceOffset);
                    Word.ToByteArray(szlArea, s7_SZL1, SzlAreaOffset);
                    Word.ToByteArray(index, s7_SZL1, SzlIndexOffset);
                    _ = Send(tag, s7_SZL1, s7_SZL1.Length);
                }
                else
                {
                    Word.ToByteArray(++seqOut, s7_SZL2, SzlRequestSequenceOffset);
                    s7_SZL2[SzlContinuationSequenceOffset] = seqIn;
                    _ = Send(tag, s7_SZL2, s7_SZL2.Length);
                }

                length = ReceiveIsoData(tag, ref data);

                if (length > MinimumSzlResponseLength)
                {
                    lastError = ProcessSzlResponse(data, resultData, first, ref done, ref seqIn, ref lengthOfDataRead, ref offset);
                    if (lastError == 0)
                    {
                        first = false;
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

            return ([], 0);
        }
        finally
        {
            _bufferPool.Return(data);
            _bufferPool.Return(resultData);
        }
    }

    /// <summary>Receives ISO protocol data from the specified tag and stores the result in the provided byte array.</summary>
    /// <remarks>The method expects the incoming data to conform to the ISO protocol format. If the received
    /// data does not meet protocol requirements, the method returns 0 to indicate failure.</remarks>
    /// <param name="tag">The tag representing the communication endpoint from which to receive ISO data.</param>
    /// <param name="bytes">A reference to the byte array that receives the data. The array must be large enough to hold the received ISO
    /// data.</param>
    /// <returns>The total number of bytes received if the operation is successful; otherwise, 0 if an error occurs.</returns>
    internal int ReceiveIsoData(Tag tag, ref byte[] bytes)
    {
        var size = 0;
        var done = false;

        while (!done)
        {
            if (!TryReceiveIsoHeader(tag, bytes, out size, out done))
            {
                return 0;
            }
        }

        // Get PDU Type
        if (ReceiveExact(tag, bytes, CotpDataHeaderLength, TpktHeaderLength) != CotpDataHeaderLength)
        {
            return 0;
        }

        // Receive S7 ISO Payload
        return ReceiveExact(tag, bytes, size - IsoDataHeaderLength, IsoDataHeaderLength) == size - IsoDataHeaderLength ? size : 0;
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release
    /// only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
        {
            return;
        }

        if (disposing)
        {
            // Stop connection/retry loops before disposing subjects
            try
            {
                _disposable?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Subscription disposal failed: {ex.Message}");
            }

            _metricsTimer?.Dispose();
            CloseSocketOptimized(_socket);
            _socket = null;

            try
            {
                _socketExceptionSubject?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Socket exception subject disposal failed: {ex.Message}");
            }

            _metricsSubject?.Dispose();
            _connectionLock?.Dispose();
        }

        _disposedValue = true;
    }

    /// <summary>Processes a received SZL response packet.</summary>
    /// <param name="data">The response buffer.</param>
    /// <param name="resultData">The accumulated result buffer.</param>
    /// <param name="first">A value indicating whether this is the first packet.</param>
    /// <param name="done">A value indicating whether the response is complete.</param>
    /// <param name="sequenceIn">The incoming sequence value.</param>
    /// <param name="lengthOfDataRead">The accumulated data length.</param>
    /// <param name="offset">The accumulated result offset.</param>
    /// <returns>Zero when the packet is valid; otherwise, an S7 error code.</returns>
    private static int ProcessSzlResponse(
        byte[] data,
        byte[] resultData,
        bool first,
        ref bool done,
        ref byte sequenceIn,
        ref ushort lengthOfDataRead,
        ref int offset)
    {
        if (Word.FromByteArray(data, SzlErrorCodeOffset) != 0 || data[SzlReturnCodeOffset] != S7ReturnCodeSuccess)
        {
            return (int)ErrorCode.WrongVarFormat;
        }

        var sourceOffset = first ? SzlFirstPayloadOffset : SzlContinuationPayloadOffset;
        var szlDataLength = first
            ? Word.FromByteArray(data, SzlDataLengthOffset) - SzlFirstPacketMetadataLength
            : Word.FromByteArray(data, SzlDataLengthOffset);
        done = data[SzlLastDataUnitOffset] == 0;
        sequenceIn = data[SzlSequenceOffset];
        Array.Copy(data, sourceOffset, resultData, offset, szlDataLength);
        offset += szlDataLength;
        lengthOfDataRead = first
            ? Word.FromByteArray(data, SzlTotalLengthOffset)
            : (ushort)(lengthOfDataRead + szlDataLength);

        return 0;
    }

    /// <summary>Checks whether the socket is still connected.</summary>
    /// <param name="socket">The socket to check.</param>
    /// <returns><see langword="true"/> when the socket appears connected; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckConnectionStatusOptimized(Socket socket)
    {
        try
        {
            return socket.Connected &&
                !(socket.Poll(SocketPollMicroseconds, SelectMode.SelectRead) && socket.Available == 0);
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

    /// <summary>Determines the optimal data read length, in bytes, for the specified PLC type.</summary>
    /// <param name="plcType">The type of PLC for which to determine the optimal data read length.</param>
    /// <returns>The recommended number of bytes to read in a single operation for the specified PLC type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort GetOptimalDataReadLength(CpuType plcType) => plcType switch
    {
        CpuType.Logo0BA8 => LogoPduLength,
        CpuType.S7200 => StandardPduLength,
        CpuType.S7300 => StandardPduLength,
        CpuType.S7400 => ExtendedPduLength,
        CpuType.S71200 => ExtendedPduLength,
        CpuType.S71500 => HighPerformancePduLength,
        _ => StandardPduLength
    };

    /// <summary>Closes and disposes the specified socket.</summary>
    /// <param name="socket">The socket to close and dispose.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CloseSocketOptimized(Socket? socket)
    {
        if (socket is null)
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
                catch (Exception ex)
                {
                    LogWarning($"Socket shutdown failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogWarning($"Socket connection state check failed during close: {ex.Message}");
        }
        finally
        {
            try
            {
                socket.Close();
            }
            catch (Exception ex)
            {
                LogWarning($"Socket close failed: {ex.Message}");
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception ex)
            {
                LogWarning($"Socket dispose failed: {ex.Message}");
            }
        }
    }

    /// <summary>Logs an error message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The error message to log.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogError(string message)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[ERROR] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

    /// <summary>Writes an informational message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The informational message to write to the debug output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogInfo(string message)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[INFO] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

    /// <summary>Writes a warning message to the debug output when a debugger is attached.</summary>
    /// <param name="message">The warning message to write to the debug output.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWarning(string message)
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        Debug.WriteLine($"[WARN] {System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
    }

#if !NETFRAMEWORK
    /// <summary>Asynchronously receives a complete TPKT packet from the underlying socket into the specified buffer.</summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The buffer that receives the TPKT packet data.</param>
    /// <param name="expectedMin">The minimum expected length of the TPKT packet, in bytes.</param>
    /// <returns>The total number of bytes read into the buffer, or a value less than 4 if the TPKT header could not be read.</returns>
    private static async Task<int> ReceiveTpktExactModernAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        var headerRead = await ReceiveExactAsync(socket, buffer, TpktHeaderLength, 0).ConfigureAwait(false);
        if (headerRead != TpktHeaderLength)
        {
            return headerRead;
        }

        var length = (buffer[TpktLengthHighByteOffset] << BitsPerByte) | buffer[TpktLengthLowByteOffset];
        if (length < TpktHeaderLength || length > buffer.Length)
        {
            LogWarning($"Invalid TPKT length {length} for receive buffer {buffer.Length}");
            return 0;
        }

        if (length < expectedMin && expectedMin > 0)
        {
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}");
        }

        var remaining = length - TpktHeaderLength;
        if (remaining == 0)
        {
            return headerRead;
        }

        var bodyRead = await ReceiveExactAsync(socket, buffer, remaining, TpktHeaderLength).ConfigureAwait(false);
        return bodyRead <= 0 ? headerRead : headerRead + bodyRead;

        static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int size, int offset)
        {
            var total = 0;
            while (total < size)
            {
                var received = await socket.ReceiveAsync(buffer.AsMemory(offset + total, size - total), SocketFlags.None).ConfigureAwait(false);
                if (received <= 0)
                {
                    break;
                }

                total += received;
            }

            return total;
        }
    }
#endif

    /// <summary>Receives and validates an ISO packet header.</summary>
    /// <param name="tag">The related PLC tag.</param>
    /// <param name="bytes">The receive buffer.</param>
    /// <param name="size">The parsed packet size.</param>
    /// <param name="done">A value indicating whether the payload header has been reached.</param>
    /// <returns>true when the header is valid; otherwise, false.</returns>
    private bool TryReceiveIsoHeader(Tag tag, byte[] bytes, out int size, out bool done)
    {
        done = false;
        size = 0;
        if (ReceiveExact(tag, bytes, TpktHeaderLength) != TpktHeaderLength)
        {
            return false;
        }

        size = Word.FromByteArray(bytes, TpktLengthHighByteOffset);
        if (size == IsoDataHeaderLength)
        {
            return ReceiveExact(tag, bytes, CotpDataHeaderLength, TpktHeaderLength) == CotpDataHeaderLength;
        }

        if (size > DataReadLength + IsoDataHeaderLength || size < MinimumIsoPacketLength)
        {
            return false;
        }

        done = true;
        return true;
    }

    /// <summary>Stores the r ec ei ve r a w value.</summary>
    /// <param name="tag">The t a g value.</param>
    /// <param name="buffer">The b uf f e r value.</param>
    /// <param name="size">The s i z e value.</param>
    /// <param name="offset">The o ff s e t value.</param>
    /// <returns>The resulting value.</returns>
    private int ReceiveRaw(Tag? tag, byte[] buffer, int size, int offset = 0)
        => ReceiveCore(tag, buffer, size, offset, traceOperation: false);

    /// <summary>Stores the r ec ei ve co r e value.</summary>
    /// <param name="tag">The t a g value.</param>
    /// <param name="buffer">The b uf f e r value.</param>
    /// <param name="size">The s i z e value.</param>
    /// <param name="offset">The o ff s e t value.</param>
    /// <param name="traceOperation">The t ra ce op er at i o n value.</param>
    /// <returns>The resulting value.</returns>
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

                if (traceOperation && tag is not null && Debugger.IsAttached)
                {
                    var result = buffer[S7ResponseReturnCodeOffset] == S7ReturnCodeSuccess ? Success : Failed;
                    Debug.WriteLine($"{DateTime.Now} Read Tag: {tag.Name} value: {tag.Value} {result} ({received} bytes, {stopwatch.ElapsedMilliseconds}ms)");
                }

                return received;
            }

            RecordError();
            _socketExceptionSubject.OnNext(new S7Exception(DeviceNotConnectedMessage));
        }
        catch (Exception ex)
        {
            RecordError();
            _socketExceptionSubject.OnNext(ex);
        }

        return -1;
    }

    /// <summary>Attempts to establish and optimize a connection to a Siemens PLC using multiple TSAP profiles for maximum compatibility.</summary>
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

            if (_initComplete && _socket is not null && CheckConnectionStatusOptimized(_socket))
            {
                return true;
            }

            // Try known TSAP profiles to maximize multi-connection compatibility
            foreach (var profile in new[] { TsapProfile.PG, TsapProfile.OP, TsapProfile.PGAlt })
            {
                var attemptSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Enhanced socket configuration for optimal performance
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DefaultTimeout);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, DefaultTimeout);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                attemptSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

                // Set optimal buffer sizes based on PLC type
                var bufferSize = DataReadLength * SocketBufferPduMultiplier;
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, bufferSize);
                attemptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, bufferSize);

                var server = new IPEndPoint(IPAddress.Parse(IP), S7TcpPort);

#if NETFRAMEWORK
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
                if (await PerformOptimizedHandshakeAsync(attemptSocket, profile).ConfigureAwait(false))
                {
                    var oldSocket = _socket;
                    _socket = attemptSocket;
                    CloseSocketOptimized(oldSocket);
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
                    _ = _connectionLock.Release();
                }
            }
            catch (ObjectDisposedException)
            {
                // Teardown race: dispose may win while an async connect is completing.
            }
        }
    }

    /// <summary>Performs an optimized asynchronous handshake using the specified TSAP profile.</summary>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="profile">The TSAP profile to use for the handshake operation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the handshake
    /// succeeds; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeAsync(Socket socket, TsapProfile profile)
    {
        var receiveBuffer = _bufferPool.Rent(HandshakeReceiveBufferSize);
        try
        {
#if NETFRAMEWORK
            return await PerformOptimizedHandshakeNetStandardAsync(socket, receiveBuffer, profile).ConfigureAwait(false);
#else
            return await PerformOptimizedHandshakeModernAsync(socket, receiveBuffer, profile).ConfigureAwait(false);
#endif
        }
        finally
        {
            _bufferPool.Return(receiveBuffer);
        }
    }

#if NETFRAMEWORK
    /// <summary>Receives a complete TPKT packet on .NET Framework using the legacy socket async pattern.</summary>
    /// <param name="socket">The connected socket to read from.</param>
    /// <param name="buffer">The destination buffer that receives the packet bytes.</param>
    /// <param name="expectedMin">The minimum expected packet length in bytes.</param>
    /// <returns>The number of bytes read, or a value less than four when the TPKT header could not be read.</returns>
    private async Task<int> ReceiveTpktExactNetStandardAsync(Socket socket, byte[] buffer, int expectedMin)
    {
        GC.KeepAlive(this);

        // Read TPKT header (4 bytes)
        var read = await ReceiveExactAsync(socket, buffer, TpktHeaderLength, 0).ConfigureAwait(false);
        if (read != TpktHeaderLength)
        {
            return read;
        }

        var length = (buffer[TpktLengthHighByteOffset] << BitsPerByte) | buffer[TpktLengthLowByteOffset];
        if (length < TpktHeaderLength || length > buffer.Length)
        {
            LogWarning($"Invalid TPKT length {length} for receive buffer {buffer.Length}");
            return 0;
        }

        if (length < expectedMin && expectedMin > 0)
        {
            // Try to continue anyway, but report
            LogWarning($"TPKT length {length} smaller than expected {expectedMin}");
        }

        var remaining = length - TpktHeaderLength;
        if (remaining == 0)
        {
            return read;
        }

        var bodyRead = await ReceiveExactAsync(socket, buffer, remaining, TpktHeaderLength).ConfigureAwait(false);
        return bodyRead <= 0 ? read : read + bodyRead;

        static async Task<int> ReceiveExactAsync(Socket socket, byte[] buffer, int size, int offset)
        {
            var total = 0;
            while (total < size)
            {
                var received = await Task.Factory.FromAsync(
                    (callback, state) => socket.BeginReceive(buffer, offset + total, size - total, SocketFlags.None, callback, state),
                    socket.EndReceive,
                    null).ConfigureAwait(false);

                if (received <= 0)
                {
                    break;
                }

                total += received;
            }

            return total;
        }
    }

    /// <summary>Performs the legacy asynchronous S7 handshake sequence on .NET Framework sockets.</summary>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="receiveBuffer">The shared buffer that receives handshake responses.</param>
    /// <param name="profile">The TSAP profile that defines the connection parameters.</param>
    /// <returns><see langword="true"/> when the handshake completes successfully; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeNetStandardAsync(Socket socket, byte[] receiveBuffer, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var connectionRequest = GetConnectionRequestBytes(profile);
            var sentTask = Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(connectionRequest, 0, connectionRequest.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
            var sent = await sentTask.ConfigureAwait(false);
            if (sent != connectionRequest.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactNetStandardAsync(socket, receiveBuffer, ConnectionResponseLength).ConfigureAwait(false);

            if (received < ConnectionResponseLength)
            {
                LogError($"Invalid connection response length {received}");
                return false;
            }

            // Step 3: Communication setup request
            var communicationSetupRequest = GetCommunicationSetupBytes();
            sentTask = Task.Factory.FromAsync(
                (callback, state) => socket.BeginSend(communicationSetupRequest, 0, communicationSetupRequest.Length, SocketFlags.None, callback, state),
                socket.EndSend,
                null);
            sent = await sentTask.ConfigureAwait(false);
            if (sent != communicationSetupRequest.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactNetStandardAsync(socket, receiveBuffer, CommunicationSetupResponseLength).ConfigureAwait(false);
            if (received < CommunicationSetupResponseLength)
            {
                LogError($"Invalid communication setup response length {received}");
                return false;
            }

            UpdateNegotiatedPduLength(receiveBuffer, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }
#else
    /// <summary>Performs an optimized asynchronous handshake sequence with a remote endpoint using the modern socket API.</summary>
    /// <remarks>This method sends and receives protocol-specific handshake messages to establish a
    /// connection. If any step in the handshake fails, the method logs an error and returns <see langword="false"/>.
    /// The method does not throw exceptions for handshake failures; instead, it returns <see langword="false"/> to
    /// indicate failure.</remarks>
    /// <param name="socket">The connected socket used for the handshake.</param>
    /// <param name="receiveBuffer">A buffer used to receive handshake response data from the remote endpoint. Must be large enough to hold the
    /// expected handshake messages.</param>
    /// <param name="profile">The connection profile containing parameters required for the handshake process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the handshake
    /// completes successfully; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> PerformOptimizedHandshakeModernAsync(Socket socket, byte[] receiveBuffer, TsapProfile profile)
    {
        try
        {
            // Step 1: Initial connection request
            var connectionRequest = GetConnectionRequestBytes(profile);
            var sent = await socket.SendAsync(connectionRequest, SocketFlags.None).ConfigureAwait(false);
            if (sent != connectionRequest.Length)
            {
                LogError("Failed to send initial connection request");
                return false;
            }

            // Step 2: Receive connection response (TPKT length based)
            var received = await ReceiveTpktExactModernAsync(socket, receiveBuffer, ConnectionResponseLength).ConfigureAwait(false);
            if (received < ConnectionResponseLength)
            {
                LogError($"Invalid connection response length {received}");
                return false;
            }

            // Step 3: Communication setup request
            var communicationSetupRequest = GetCommunicationSetupBytes();
            sent = await socket.SendAsync(communicationSetupRequest, SocketFlags.None).ConfigureAwait(false);
            if (sent != communicationSetupRequest.Length)
            {
                LogError("Failed to send communication setup request");
                return false;
            }

            // Step 4: Receive communication setup response (TPKT length based)
            received = await ReceiveTpktExactModernAsync(socket, receiveBuffer, CommunicationSetupResponseLength).ConfigureAwait(false);
            if (received < CommunicationSetupResponseLength)
            {
                LogError($"Invalid communication setup response length {received}");
                return false;
            }

            UpdateNegotiatedPduLength(receiveBuffer, received);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Handshake failed: {ex.Message}");
            return false;
        }
    }

#endif

    /// <summary>Stores the p ro be av ai la bi li ty an dn ot if ya sy n c value.</summary>
    /// <param name="observer">The o bs er v e r value.</param>
    /// <returns>The resulting value.</returns>
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

    /// <summary>Stores the e va lu at ec on ne ct io ns ta te wi th hy st er es i s value.</summary>
    /// <returns>The resulting value.</returns>
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

        if (!_initComplete || _consecutiveConnectionFailures != ConnectionFailureThreshold)
        {
            return false;
        }

        LogWarning($"Connection probe failed {_consecutiveConnectionFailures} times consecutively. Restarting connection.");
        RestartConnection();
        return false;
    }

    /// <summary>Stores the u pd at en eg ot ia te dp du le ng t h value.</summary>
    /// <param name="response">The r es po n s e value.</param>
    /// <param name="responseLength">The r es po ns el en g t h value.</param>
    private void UpdateNegotiatedPduLength(byte[] response, int responseLength)
    {
        if (responseLength < CommunicationSetupResponseLength)
        {
            return;
        }

        var negotiatedPduLength = Word.FromByteArray(response, NegotiatedPduLengthOffset);
        if (negotiatedPduLength is < MinimumNegotiatedPduLength or > MaximumNegotiatedPduLength)
        {
            return;
        }

        if (negotiatedPduLength != DataReadLength)
        {
            LogInfo($"Negotiated PDU length updated to {negotiatedPduLength} bytes.");
        }

        DataReadLength = negotiatedPduLength;
    }

    /// <summary>Receives exactly the requested number of bytes unless the socket closes first.</summary>
    /// <param name="tag">The tag associated with the receive operation.</param>
    /// <param name="buffer">The receive buffer.</param>
    /// <param name="size">The number of bytes to receive.</param>
    /// <param name="offset">The buffer offset.</param>
    /// <returns>The number of bytes received.</returns>
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

    /// <summary>Builds a connection request message as a byte array using the specified TSAP profile settings.</summary>
    /// <remarks>The generated message is compatible with S7-1200, S7-300, and S7-400 devices, using a TPDU
    /// size of 512 bytes. The TSAP values are set based on the properties of the supplied profile.</remarks>
    /// <param name="profile">The TSAP profile containing source and destination transport service access point (TSAP) values used to
    /// construct the connection request.</param>
    /// <returns>A byte array representing the connection request message, with TSAP fields set according to the provided
    /// profile.</returns>
    private byte[] GetConnectionRequestBytes(TsapProfile profile)
    {
        byte[] connectionRequest =
        [
            TpktVersion, 0, 0, ConnectionRequestTelegramLength,
            CotpConnectionRequestHeaderSize, CotpConnectionRequestPduType,
            0, 0, 0, CotpSourceReferenceLowByte, 0,
            CotpSourceTsapParameterCode, CotpTsapParameterLength,
            ProgrammingDeviceSourceTsapHighByte, DefaultSourceTsapLowByte,
            CotpDestinationTsapParameterCode, CotpTsapParameterLength,
            DefaultDestinationTsapHighByte, 0,
            CotpTpduSizeParameterCode, CotpTpduSizeParameterLength, CotpTpduSize512Bytes
        ];

        // Use TPDU size 512 (0x09) for S7-1200/300/400 compatibility
        // Source TSAP (C1)
        connectionRequest[ConnectionRequestSourceTsapHighOffset] = profile.SrcHi;
        connectionRequest[ConnectionRequestSourceTsapLowOffset] = profile.SrcLo;

        // Destination TSAP (C2)
        connectionRequest[ConnectionRequestDestinationTsapHighOffset] = profile.DstHi;
        connectionRequest[ConnectionRequestDestinationTsapLowOffset] = profile.DstLo(Rack, Slot);

        return connectionRequest;
    }

    /// <summary>Creates and returns a byte array containing the communication setup parameters for the PLC connection.</summary>
    /// <remarks>The returned array includes protocol-specific configuration values, including the optimal PDU
    /// length for the target PLC type. This method is intended for internal use when establishing or configuring a PLC
    /// communication session.</remarks>
    /// <returns>A byte array representing the communication setup message to be sent to the PLC.</returns>
    private byte[] GetCommunicationSetupBytes()
    {
        byte[] communicationSetupRequest =
        [
            TpktVersion, 0, 0, CommunicationSetupTelegramLength,
            CotpDataHeaderSize, CotpDataPduType, CotpEndOfTransmissionUnit,
            S7ProtocolIdentifier, S7JobMessageType, 0, 0, CommunicationSetupPduReference,
            0, 0, CommunicationSetupParameterLength, 0, 0,
            S7SetupCommunicationFunction, 0, 0, 1, 0, 1, 0, DefaultRequestedPduLengthLowByte
        ];

        // Set optimal PDU length for the specific PLC type
        Word.ToByteArray(DataReadLength, communicationSetupRequest, CommunicationSetupPduLengthOffset);

        return communicationSetupRequest;
    }

    /// <summary>Records a successful send or receive operation, updating internal metrics and error counters.</summary>
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

    /// <summary>Records an error occurrence and updates internal error tracking state.</summary>
    /// <remarks>If the number of consecutive errors exceeds a predefined threshold, this method initiates a
    /// connection restart to recover from persistent failures.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordError()
    {
        _consecutiveErrors++;
        _metrics.RecordError();

        // Trigger connection restart if too many consecutive errors
        if (_consecutiveErrors != ConsecutiveErrorRestartThreshold)
        {
            return;
        }

        LogWarning($"Excessive failures detected: {_consecutiveErrors}. Restarting connection.");
        RestartConnection();
    }

    /// <summary>Attempts to asynchronously restart the network connection after a failure.</summary>
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

                await Task.Delay(ConnectionRestartDelayMilliseconds).ConfigureAwait(false);

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
                _ = Interlocked.Exchange(ref _restartInProgress, 0);
            }
        });

    /// <summary>Reports the current set of collected metrics to subscribed observers.</summary>
    /// <remarks>Any exceptions encountered during reporting are logged and do not propagate to the caller.</remarks>
    private void ReportMetrics()
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

    /// <summary>Asynchronously checks whether the configured IP address is reachable using an optimized ping operation.</summary>
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

        if (_initComplete && _socket is not null && CheckConnectionStatusOptimized(_socket) &&
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

    /// <summary>Stores the c he ck po rt av ai la bi li ty as y n c value.</summary>
    /// <returns>The resulting value.</returns>
    private async Task<bool> CheckPortAvailabilityAsync()
    {
        using var probeSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probeSocket.Blocking = false;

        try
        {
            var server = new IPEndPoint(IPAddress.Parse(IP), S7TcpPort);
#if NETFRAMEWORK
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

    /// <summary>Determines whether the underlying socket connection is currently active, using an optimized check to minimize overhead.</summary>
    /// <remarks>This method performs a lightweight check to verify the connection status. If the connection
    /// appears active but has not been used for more than two minutes, an additional lightweight operation is performed
    /// to confirm connectivity. If the connection is found to be inactive and initialization is complete, the
    /// connection is automatically restarted. This method is intended for internal use to efficiently monitor
    /// connection health.</remarks>
    /// <returns>true if the connection is considered active; otherwise, false.</returns>
    private bool CheckConnectionStatusOptimized()
    {
        if (_socket is null)
        {
            return false;
        }

        try
        {
            var isConnected = _socket.Connected &&
                            !(_socket.Poll(SocketPollMicroseconds, SelectMode.SelectRead) && _socket.Available == 0);

            return isConnected && DateTime.UtcNow - _lastSuccessfulOperation > TimeSpan.FromMinutes(ConnectionIdleCheckMinutes)
                ? PerformLightweightConnectionCheck()
                : isConnected;
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

    /// <summary>Performs a lightweight check to determine whether the underlying socket connection is still valid.</summary>
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
        /// <summary>Gets the TSAP profile for the "PG" (Programming Device) connection type.</summary>
        /// <remarks>This profile is typically used when establishing a connection to a PLC as a
        /// programming device. The PG profile may have different access rights or communication behavior compared to
        /// other TSAP profiles, depending on the PLC configuration.</remarks>
        public static TsapProfile PG => new(
            ProgrammingDeviceSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(PG));

        /// <summary>Gets the TSAP profile for the "OP" (Operator Panel) communication type.</summary>
        /// <remarks>Use this profile when establishing a connection that requires the Operator Panel TSAP
        /// settings. The profile includes predefined parameters suitable for typical OP communication
        /// scenarios.</remarks>
        public static TsapProfile OP => new(
            OperatorPanelSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(OP));

        /// <summary>Gets the TSAP profile for the PGAlt (Programming Device Alternative) connection type.</summary>
        public static TsapProfile PGAlt => new(
            AlternateProgrammingDeviceSourceTsapHighByte,
            DefaultSourceTsapLowByte,
            DefaultDestinationTsapHighByte,
            (rack, slot) => (byte)((rack * RackAddressMultiplier * SlotsPerRackAddressUnit) + slot),
            nameof(PGAlt));
    }
}
