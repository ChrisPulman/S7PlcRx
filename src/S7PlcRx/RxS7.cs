// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Globalization;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Core;
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Core;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
#endif

using DateTime = System.DateTime;
using TimeSpan = System.TimeSpan;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>
/// Provides an observable, reactive interface for reading from and writing to Siemens S7 PLCs, supporting tag-based
/// access, status monitoring, and asynchronous operations.
/// </summary>
/// <remarks>The RxS7 class enables integration with Siemens S7 programmable logic controllers (PLCs) using a
/// tag-based model and reactive programming patterns. It exposes observables for PLC data, connection status, errors,
/// and operational metrics, allowing clients to subscribe to real-time updates. The class supports both synchronous and
/// asynchronous read/write operations, as well as advanced features such as watchdog monitoring and batch variable
/// access. Thread safety is maintained for concurrent operations. Dispose the instance when no longer needed to release
/// resources and terminate background operations.</remarks>
public class RxS7 : IRxS7
{
    /// <summary>Stores the s oc ke t r x used by this instance.</summary>
    private readonly S7SocketRx _socketRx;

    /// <summary>Stores the d at ar e a d used by this instance.</summary>
    private readonly Signal<Tag?> _dataRead = new();

    /// <summary>Stores the d is po sa bl e s used by this instance.</summary>
    private readonly CompositeDisposable _disposables = [];

    /// <summary>Stores the l as te rr o r used by this instance.</summary>
    private readonly Signal<string> _lastError = new();

    /// <summary>Stores the l as te rr or co d e used by this instance.</summary>
    private readonly Signal<ErrorCode> _lastErrorCode = new();

    /// <summary>Stores the p lc re qu es ts ub je c t used by this instance.</summary>
    private readonly Signal<PLCRequest> _plcRequestSubject = new();

    /// <summary>Stores the s ta t u s used by this instance.</summary>
    private readonly Signal<string> _status = new();

    /// <summary>Stores the r ea dt i m e used by this instance.</summary>
    private readonly Signal<long> _readTime = new();

    /// <summary>Stores the l o c k used by this instance.</summary>
    private readonly SemaphoreSlim _lock = new(1);

    /// <summary>Stores the l oc kt ag li s t used by this instance.</summary>
    private readonly SemaphoreSlim _lockTagList = new(1);

    /// <summary>Stores the lock used to serialize direct socket interactions.</summary>
#if NET8_0
    private readonly object _socketLock = new();
#else
    private readonly Lock _socketLock = new();
#endif

    /// <summary>Stores the s to pw at c h used by this instance.</summary>
    private readonly Stopwatch _stopwatch = new();

    /// <summary>Stores the p au s e d used by this instance.</summary>
    private readonly Signal<bool> _paused = new();

    /// <summary>Stores the l as tc on ne ct ed at u t c used by this instance.</summary>
    private DateTime _lastConnectedAtUtc = DateTime.MinValue;

    /// <summary>Stores the p au s e used by this instance.</summary>
    private bool _pause;

    /// <summary>
    /// Initializes a new instance of the <see cref="RxS7"/> class and establishes a connection to a Siemens S7 PLC with optional.
    /// watchdog monitoring and periodic tag reading.
    /// </summary>
    /// <remarks>If a valid watchdog address is provided, the constructor enables periodic writing to the
    /// specified address to support external watchdog monitoring. Tag reading and connection status monitoring are
    /// started automatically upon construction.</remarks>
    /// <param name="type">The type of the PLC CPU to connect to.</param>
    /// <param name="ip">The IP address of the PLC to connect to.</param>
    /// <param name="rack">The rack number of the PLC hardware configuration.</param>
    /// <param name="slot">The slot number of the PLC CPU module.</param>
    /// <param name="watchDogAddress">The address of the watchdog tag in the PLC memory. Must be a DBW address or null to disable watchdog monitoring.</param>
    /// <param name="interval">The interval, in milliseconds, at which tag values are read from the PLC. Must be greater than 0.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog address during each watchdog cycle.</param>
    /// <param name="watchDogInterval">The interval, in seconds, at which the watchdog value is written. Must be greater than 0 if watchdog monitoring
    /// is enabled.</param>
    /// <exception cref="ArgumentException">Thrown if watchDogAddress is provided and is not a valid DBW address.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if watchDogInterval is less than 1 when watchdog monitoring is enabled.</exception>
    public RxS7(CpuType type, string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 10)
    {
        PLCType = type;
        IP = ip;
        Rack = rack;
        Slot = slot;

        // Create an observable socket
        _socketRx = new(IP, type, rack, slot);

        IsConnected = _socketRx.IsConnected;

        // Get the PLC connection status
        _disposables.Add(IsConnected.Subscribe(x =>
        {
            IsConnectedValue = x;
            if (x)
            {
                _lastConnectedAtUtc = DateTime.UtcNow;
            }

            _status.OnNext($"{DateTime.Now} - PLC Connected Status: {x}");
        }));

        if (!string.IsNullOrWhiteSpace(watchDogAddress))
        {
            if (!watchDogAddress.Contains("DBW", StringComparison.Ordinal))
            {
                throw new ArgumentException("WatchDogAddress must be a DBW address.", nameof(watchDogAddress));
            }

            WatchDogAddress = watchDogAddress;
            if (watchDogInterval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(watchDogInterval), "WatchDogInterval must be greater than 0.");
            }

            WatchDogWritingTime = watchDogInterval;
            WatchDogValueToWrite = watchDogValueToWrite;
            _disposables.Add(WatchDogObservable().Subscribe());
        }

        _disposables.Add(TagReaderObservable(interval).Subscribe());

        _disposables.Add(_plcRequestSubject.Subscribe(request =>
        {
            if (request.Request == PLCRequestType.Write)
            {
                _ = WriteString(request.Tag);
            }

            GetTagValue(request.Tag);
            _dataRead.OnNext(request.Tag);
        }));
    }

    /// <summary>Gets an observable sequence that emits all tag updates as they occur.</summary>
    /// <remarks>Each observer receives tag updates in real time as they are published. The sequence is shared
    /// among all subscribers, and subscriptions are managed automatically. Observers may receive null values if a tag
    /// is removed or unavailable.</remarks>
    public IObservable<Tag?> ObserveAll =>
        _dataRead
            .Publish()
            .RefCount();

    /// <summary>Gets an observable sequence that indicates whether the operation is currently paused.</summary>
    /// <remarks>The returned observable emits a value of <see langword="true"/> when the operation enters a
    /// paused state, and <see langword="false"/> when it resumes. Subscribers receive updates only when the paused
    /// state changes. The sequence is shared among all subscribers.</remarks>
    public IObservable<bool> IsPaused => _paused.DistinctUntilChanged().Publish().RefCount();

    /// <summary>Gets the IP address associated with the current instance.</summary>
    public string IP { get; }

    /// <summary>Gets an observable sequence that indicates whether the connection is currently established.</summary>
    /// <remarks>Subscribers receive updates whenever the connection state changes. The sequence emits <see
    /// langword="true"/> when connected and <see langword="false"/> when disconnected.</remarks>
    public IObservable<bool> IsConnected { get; }

    /// <summary>Gets a value indicating whether the connection is currently established.</summary>
    public bool IsConnectedValue { get; private set; }

    /// <summary>Gets an observable sequence that provides the most recent error messages encountered by the component.</summary>
    /// <remarks>Subscribers receive error messages as they occur. The sequence is shared among all
    /// subscribers, and each subscriber receives messages from the point of subscription onward.</remarks>
    public IObservable<string> LastError => _lastError.Publish().RefCount();

    /// <summary>Gets an observable sequence that emits the most recent error code reported by the system.</summary>
    /// <remarks>Subscribers receive updates whenever a new error code is reported. The sequence is shared
    /// among all subscribers and only remains active while there is at least one active subscription.</remarks>
    public IObservable<ErrorCode> LastErrorCode => _lastErrorCode.Publish().RefCount();

    /// <summary>Gets the type of PLC (Programmable Logic Controller) associated with this instance.</summary>
    public CpuType PLCType { get; }

    /// <summary>Gets the rack number associated with the device or component.</summary>
    public short Rack { get; }

    /// <summary>Gets or sets a value indicating whether WatchDog writing output is displayed.</summary>
    public bool ShowWatchDogWriting { get; set; }

    /// <summary>Gets the slot number associated with this instance.</summary>
    public short Slot { get; }

    /// <summary>Gets an observable sequence that provides status updates as strings.</summary>
    /// <remarks>Subscribers receive status updates as they occur. The observable sequence is shared among all
    /// subscribers, and subscriptions are managed automatically. Status updates are pushed to observers in real
    /// time.</remarks>
    public IObservable<string> Status => _status.Publish().RefCount();

    /// <summary>Gets the collection of tags associated with the current instance.</summary>
    public Tags TagList { get; } = [];

    /// <summary>Gets the network address of the WatchDog service, if configured.</summary>
    public string? WatchDogAddress { get; }

    /// <summary>Gets or sets the value to be written to the watchdog timer.</summary>
    public ushort WatchDogValueToWrite { get; set; } = 4500;

    /// <summary>Gets the interval, in seconds, that the watchdog uses when writing status updates.</summary>
    public int WatchDogWritingTime { get; } = 10;

    /// <summary>Gets a value indicating whether gets a value that indicates whether the object is disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>Gets an observable sequence that emits the current read time in ticks whenever a read operation occurs.</summary>
    /// <remarks>The observable sequence is shared among all subscribers. Each subscriber receives
    /// notifications when a read operation is performed, with the value representing the read time in ticks. The
    /// sequence completes when the underlying source completes.</remarks>
    public IObservable<long> ReadTime => _readTime.Publish().RefCount();

    /// <summary>
    /// Returns an observable sequence that emits the current and future values of the specified variable, cast to the
    /// specified type.
    /// </summary>
    /// <remarks>The returned observable is hot and shared among all subscribers. Subscribers receive the most
    /// recent value and all subsequent updates. If the variable does not exist or its value cannot be cast to the
    /// specified type, the observable emits null.</remarks>
    /// <typeparam name="T">The type to which the variable's value is cast in the observable sequence.</typeparam>
    /// <param name="variable">The name of the variable to observe. If null, all variables are observed.</param>
    /// <returns>An observable sequence of values of type T, or null if the variable's value is not present or cannot be cast to
    /// T. The sequence emits a new value each time the variable changes.</returns>
    public IObservable<T?> Observe<T>(string? variable) =>
        ObserveAll
            .Where(t => TagValueIsValid<T>(t, variable))
            .Select(t => (T?)t?.Value)
            .OnErrorRetry()
            .Publish()
            .RefCount();

    /// <summary>Asynchronously retrieves the value of the specified variable, cast to the specified type, if available.</summary>
    /// <remarks>If the variable's type is not known, it is set to the requested type <typeparamref name="T"/>
    /// before retrieving the value. The method waits for an internal pause condition to be met before
    /// proceeding.</remarks>
    /// <typeparam name="T">The type to which the variable's value is cast and returned.</typeparam>
    /// <param name="variable">The name of the variable whose value is to be retrieved. Can be null.</param>
    /// <returns>A value of type <typeparamref name="T"/> if the variable exists and can be cast to the specified type;
    /// otherwise, <see langword="default"/>.</returns>
    public async Task<T?> Value<T>(string? variable)
    {
        _pause = true;
        try
        {
            _ = await _paused.Where(x => x).FirstAsync();
            var tag = TagList[variable!];
            if (tag?.Type == typeof(object))
            {
                tag.Type = typeof(T);
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                GetTagValue(tag);
                if (TagValueIsValid<T>(tag))
                {
                    return (T?)tag?.Value;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            return default;
        }
        finally
        {
            _pause = false;
        }
    }

    /// <summary>Asynchronously retrieves the value of the specified variable, pausing polling operations during the read.</summary>
    /// <remarks>Polling is temporarily paused while the value is being read to ensure consistency. If no
    /// polling is active, the method may wait briefly before proceeding. The method is cancellation-friendly and will
    /// respond promptly to cancellation requests.</remarks>
    /// <typeparam name="T">The expected type of the variable's value to retrieve.</typeparam>
    /// <param name="variable">The name of the variable whose value is to be retrieved. Cannot be null, empty, or consist only of white-space
    /// characters.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the specified variable
    /// cast to type T, or the default value of T if the variable is not found or cannot be cast.</returns>
    /// <exception cref="ArgumentNullException">Thrown if variable is null, empty, or consists only of white-space characters.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
    public async Task<T?> ValueAsync<T>(string? variable, CancellationToken cancellationToken)
    {
        if (HasNoText(variable))
        {
            throw new ArgumentNullException(nameof(variable));
        }

        _pause = true;
        try
        {
            // Wait until the poll loop observes the paused state.
            // If nothing is polling, the observable might never emit; in that case, proceed after a short delay.
            // This keeps behavior compatible while still being cancellation-friendly.
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
#if NET8_0_OR_GREATER
                await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
#else
                using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());
#endif
                using var sub = _paused.Where(x => x).Take(1).Subscribe(_ => tcs.TrySetResult(true), ex => tcs.TrySetException(ex));
                await tcs.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pause wait failed; direct read fallback will be used. {ex}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var tag = TagList[variable!];
            if (tag?.Type == typeof(object))
            {
                tag.Type = typeof(T);
            }

            for (var attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GetTagValue(tag);
                if (TagValueIsValid<T>(tag))
                {
                    return (T?)tag?.Value;
                }

                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }

            return default;
        }
        finally
        {
            _pause = false;
        }
    }

    /// <summary>Sets the value of the specified variable if it exists and the value is compatible with the variable's type.</summary>
    /// <remarks>If the variable does not exist or the value is null, this method does nothing. The value is
    /// only set if its type matches the variable's expected type or if the type parameter is object.</remarks>
    /// <typeparam name="T">The type of the value to assign to the variable.</typeparam>
    /// <param name="variable">The name of the variable whose value is to be set. Cannot be null.</param>
    /// <param name="value">The value to assign to the variable. Must be compatible with the variable's type.</param>
    public void Value<T>(string? variable, T? value)
    {
        var tag = TagList[variable!];
        if (tag is null || value is null || (typeof(object) != typeof(T) && tag.Type != typeof(T)))
        {
            return;
        }

        tag.NewValue = value;
        Write(tag);
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Retrieves detailed information about the connected CPU as an observable sequence.</summary>
    /// <remarks>The method waits until a connection is established before retrieving CPU information. If the
    /// required data is not immediately available, the method will retry until successful or until the subscription is
    /// disposed. The order and content of the returned string array correspond to specific CPU information fields. This
    /// method is intended for use in reactive programming scenarios where CPU information is needed
    /// asynchronously.</remarks>
    /// <returns>An observable sequence that emits a string array containing CPU information fields, such as the AS name, module
    /// name, copyright, serial number, module type name, order code, and version numbers. The sequence completes after
    /// emitting the data.</returns>
    public IObservable<string[]> GetCpuInfo() =>
        Observable.Create<string[]>(obs =>
        {
            SingleAssignmentDisposable? d = null;
            d = new SingleAssignmentDisposable
            {
                Disposable = IsConnected.Where(x => x).Take(1).Subscribe(async _ =>
                {
                    errorCpuData:
                    var cpuData = _socketRx.GetSZLData(28);
                    if (cpuData.data.Length == 0 && !d!.IsDisposed)
                    {
                        await Task.Delay(10).ConfigureAwait(true);
                        goto errorCpuData;
                    }

                    errororder:
                    var orderCode = _socketRx.GetSZLData(17);
                    if (orderCode.data.Length == 0 && !d!.IsDisposed)
                    {
                        await Task.Delay(10).ConfigureAwait(true);
                        goto errororder;
                    }

                    if (cpuData.data.Length >= 204 && orderCode.data.Length >= 25)
                    {
                        var l = new List<string>
                        {
                            PlcTypes.String.FromByteArray(cpuData.data, 2, 24).Replace("\0", string.Empty), // AS Name
                            PlcTypes.String.FromByteArray(cpuData.data, 36, 24).Replace("\0", string.Empty), // Module Name
                            PlcTypes.String.FromByteArray(cpuData.data, 104, 26).Replace("\0", string.Empty), // Copyright
                            PlcTypes.String.FromByteArray(cpuData.data, 138, 24).Replace("\0", string.Empty), // Serial Number
                            PlcTypes.String.FromByteArray(cpuData.data, 172, 32).Replace("\0", string.Empty), // Module Type Name
                            PlcTypes.String.FromByteArray(orderCode.data, 2, 20).Replace("\0", string.Empty), // Order Code
                            $"V1: {orderCode.data[orderCode.size - 3]}", // Version 1
                            $"V2: {orderCode.data[orderCode.size - 2]}", // Version 2
                            $"V3: {orderCode.data[orderCode.size - 1]}", // Version 3
                        };
                        obs.OnNext([.. l]);
                        obs.OnCompleted();
                        return;
                    }

                    if (cpuData.data.Length < 204)
                    {
                        goto errorCpuData;
                    }

                    if (orderCode.data.Length >= 25)
                    {
                        return;
                    }

                    goto errororder;
                })
            };

            return d;
        });

    /// <summary>Writes the serialized representation of a class object to the specified data block at the given byte address.</summary>
    /// <param name="tag">The tag that identifies the target location for the write operation.</param>
    /// <param name="classValue">The class object to serialize and write. Cannot be null.</param>
    /// <param name="db">The number of the data block to which the class data will be written.</param>
    /// <param name="startByteAdr">The starting byte address within the data block at which to begin writing. The default is 0.</param>
    /// <returns>true if the class data was successfully written; otherwise, false.</returns>
    internal bool WriteClass(Tag tag, object classValue, int db, int startByteAdr = 0)
    {
        if (classValue is null)
        {
            return false;
        }

        var bytes = new byte[(int)Class.GetClassSize(classValue)];
        _ = Class.ToBytes(classValue, bytes);
        return WriteMultipleBytes(tag, [.. bytes], db, startByteAdr);
    }

    /// <summary>Adds a new tag to the collection or updates an existing tag with the same name.</summary>
    /// <param name="tag">The tag to add or update. The tag's Address property must not be null, empty, or consist only of white-space
    /// characters.</param>
    /// <exception cref="TagAddressOutOfRangeException">Thrown if the tag's Address property is null, empty, or consists only of white-space characters.</exception>
    internal void AddUpdateTagItemInternal(Tag tag)
    {
        if (tag is null || string.IsNullOrWhiteSpace(tag.Name))
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new TagAddressOutOfRangeException(tag);
        }

        var tagName = tag.Name!;
        _lockTagList.Wait();
        try
        {
            if (TagList[tagName] is Tag tagExists)
            {
                tagExists.Name = tagName;
                tagExists.Value = tag.Value;
                tagExists.Address = tag.Address;
                tagExists.Type = tag.Type;
                tagExists.ArrayLength = tag.ArrayLength;
            }
            else
            {
                TagList.Add(tag);
            }
        }
        finally
        {
            _ = _lockTagList.Release();
        }
    }

    /// <summary>Removes the tag item with the specified name from the collection, if it exists.</summary>
    /// <param name="tagName">The name of the tag item to remove. Cannot be null, empty, or consist only of white-space characters.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tagName"/> is null, empty, or consists only of white-space characters.</exception>
    internal void RemoveTagItemInternal(string tagName)
    {
        if (HasNoText(tagName))
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        _lockTagList.Wait();
        try
        {
            if (TagList.ContainsKey(tagName!))
            {
                TagList.Remove(tagName!);
            }
        }
        finally
        {
            _ = _lockTagList.Release();
        }
    }

    /// <summary>Reads multiple variables from the PLC in a single operation and returns their values as a dictionary.</summary>
    /// <remarks>The returned dictionary uses case-insensitive keys based on the tag names. If a variable
    /// cannot be read, its value in the dictionary will be <see langword="null"/>. The method returns <see
    /// langword="null"/> if the input list is null, empty, or contains invalid tags. This method is not thread-safe and
    /// should be called with appropriate synchronization if used concurrently.</remarks>
    /// <param name="tags">A list of <see cref="Tag"/> objects specifying the variables to read. Each tag must have a valid name, address,
    /// and array length.</param>
    /// <returns>A dictionary mapping tag names to their corresponding values, or <see langword="null"/> if the read operation
    /// fails or if the input is invalid.</returns>
    internal Dictionary<string, object?>? ReadMultiVar(IReadOnlyList<Tag> tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return null;
        }

        lock (_socketLock)
        {
            var pool = ArrayPool<byte>.Shared;
            var receiveBuffer = pool.Rent(_socketRx.DataReadLength + 256);
            var parsed = new List<S7MultiVar.ReadResult>();
            try
            {
                if (!TryBuildMultiVarReadItems(tags, out var items, out var varTypes, out var arrayLengths))
                {
                    return null;
                }

                var request = S7MultiVar.BuildReadVarRequest(items);
                if (!TrySendMultiVarRequest(_socketRx, tags[0], request, receiveBuffer))
                {
                    return null;
                }

                parsed.AddRange(S7MultiVar.ParseReadVarResponse(receiveBuffer, items, pool));
                return parsed.Count == 0 ? null : CreateMultiVarReadResult(tags, parsed, varTypes, arrayLengths, ParseBytes);
            }
            catch (Exception ex)
            {
                _lastErrorCode.OnNext(ErrorCode.ReadData);
                _lastError.OnNext(ex.Message);
                return null;
            }
            finally
            {
                foreach (var r in parsed)
                {
                    if (r.RentedBuffer is not null)
                    {
                        pool.Return(r.RentedBuffer);
                    }
                }

                pool.Return(receiveBuffer);
            }
        }
    }

    /// <summary>Attempts to write the specified collection of tags to the connected device in a single multi-variable operation.</summary>
    /// <remarks>This method performs a batch write operation, sending all tag values in a single request. If
    /// any tag is invalid or the write operation fails for any tag, the method returns false. The operation is not
    /// atomic; partial writes may occur if an error is encountered during the process.</remarks>
    /// <param name="tags">A read-only list of <see cref="Tag"/> objects representing the variables to write. Each tag must have a valid
    /// name, address, and new value. The list cannot be null or empty.</param>
    /// <returns>true if all tags are written successfully; otherwise, false.</returns>
    internal bool WriteMultiVar(IReadOnlyList<Tag> tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return false;
        }

        lock (_socketLock)
        {
            var receiveBuffer = new byte[1024];
            try
            {
                if (!TryBuildMultiVarWriteItems(tags, out var items))
                {
                    return false;
                }

                var request = S7MultiVar.BuildWriteVarRequest(items);
                return TrySendMultiVarRequest(_socketRx, tags[0], request, receiveBuffer) &&
                    AreMultiVarWriteResultsSuccessful(receiveBuffer, items.Count);
            }
            catch (Exception ex)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(ex.Message);
                return false;
            }
        }
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        if (!disposing)
        {
            return;
        }

        _disposables.Dispose();
        DisposeSemaphore(_lock, "Lock");
        DisposeSemaphore(_lockTagList, "Tag list lock");
        _dataRead.Dispose();
        _lastError.Dispose();
        _socketRx.Dispose();
        _lastErrorCode.Dispose();
        _paused.Dispose();
        _plcRequestSubject.Dispose();
        _status.Dispose();
        _readTime.Dispose();
    }

    /// <summary>Returns whether the value is null, empty, or contains only whitespace.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>true when no non-whitespace text is present; otherwise, false.</returns>
    private static bool HasNoText(string? value)
    {
        if (value is null)
        {
            return true;
        }

        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Disposes a semaphore while preserving debug diagnostics.</summary>
    /// <param name="semaphore">The semaphore to dispose.</param>
    /// <param name="name">The semaphore diagnostic name.</param>
    private static void DisposeSemaphore(SemaphoreSlim semaphore, string name)
    {
        try
        {
            semaphore.Wait();
            semaphore.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{name} disposal failed. {ex}");
        }
    }

    /// <summary>Builds read-var request items from tags.</summary>
    /// <param name="tags">The tags to read.</param>
    /// <param name="items">The generated read items.</param>
    /// <param name="varTypes">The parsed variable types.</param>
    /// <param name="arrayLengths">The tag array lengths.</param>
    /// <returns>true when all tags are valid; otherwise, false.</returns>
    private static bool TryBuildMultiVarReadItems(
        IReadOnlyList<Tag> tags,
        out List<S7MultiVar.ReadItem> items,
        out VarType[] varTypes,
        out int[] arrayLengths)
    {
        items = new(tags.Count);
        varTypes = new VarType[tags.Count];
        arrayLengths = new int[tags.Count];

        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (tag is null || string.IsNullOrWhiteSpace(tag.Name) || !TryParseDbAddressForMultiVar(tag, out var db, out var startByte, out var varType, out var countBytes))
            {
                return false;
            }

            varTypes[i] = varType;
            arrayLengths[i] = tag.ArrayLength!.Value;
            items.Add(new S7MultiVar.ReadItem(DataType.DataBlock, db, startByte, countBytes, tag.Name!));
        }

        return true;
    }

    /// <summary>Builds write-var request items from tags.</summary>
    /// <param name="tags">The tags to write.</param>
    /// <param name="items">The generated write items.</param>
    /// <returns>true when all tags are valid and serializable; otherwise, false.</returns>
    private static bool TryBuildMultiVarWriteItems(IReadOnlyList<Tag> tags, out List<S7MultiVar.WriteItem> items)
    {
        items = new(tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (tag is null || string.IsNullOrWhiteSpace(tag.Name) || tag.NewValue is null)
            {
                return false;
            }

            if (!TryParseDbAddressForMultiVar(tag, out var db, out var startByte, out _, out var countBytes))
            {
                return false;
            }

            if (!TrySerializeTagNewValue(tag, out var transportSize, out var data))
            {
                return false;
            }

            items.Add(new S7MultiVar.WriteItem(DataType.DataBlock, db, startByte, countBytes, transportSize, data, tag.Name!));
        }

        return true;
    }

    /// <summary>Checks parsed multi-var write response results.</summary>
    /// <param name="receiveBuffer">The response buffer.</param>
    /// <param name="itemCount">The expected item count.</param>
    /// <returns>true when all write results succeeded; otherwise, false.</returns>
    private static bool AreMultiVarWriteResultsSuccessful(byte[] receiveBuffer, int itemCount)
    {
        var results = S7MultiVar.ParseWriteVarResponse(receiveBuffer, itemCount);
        if (results.Count != itemCount)
        {
            return false;
        }

        foreach (var result in results)
        {
            if (result.ReturnCode != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sends a multi-var request and receives the response.</summary>
    /// <param name="socketRx">The socket wrapper to use.</param>
    /// <param name="tag">The tag used for error context.</param>
    /// <param name="request">The request buffer.</param>
    /// <param name="receiveBuffer">The receive buffer.</param>
    /// <returns>true when the request was sent and a response was received; otherwise, false.</returns>
    private static bool TrySendMultiVarRequest(S7SocketRx socketRx, Tag tag, byte[] request, byte[] receiveBuffer)
    {
        if (request.Length == 0)
        {
            return false;
        }

        var sent = socketRx.Send(tag, request, request.Length);
        if (sent != request.Length)
        {
            return false;
        }

        Array.Clear(receiveBuffer, 0, receiveBuffer.Length);
        _ = socketRx.Receive(tag, receiveBuffer, receiveBuffer.Length);
        return true;
    }

    /// <summary>Creates a dictionary from parsed multi-var read results.</summary>
    /// <param name="tags">The original tags.</param>
    /// <param name="parsed">The parsed read results.</param>
    /// <param name="varTypes">The variable types.</param>
    /// <param name="arrayLengths">The array lengths.</param>
    /// <param name="parseBytes">The byte parser.</param>
    /// <returns>The read result dictionary.</returns>
    private static Dictionary<string, object?> CreateMultiVarReadResult(
        IReadOnlyList<Tag> tags,
        List<S7MultiVar.ReadResult> parsed,
        VarType[] varTypes,
        int[] arrayLengths,
        Func<VarType, byte[], int, object?> parseBytes)
    {
        var dict = new Dictionary<string, object?>(tags.Count, StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < tags.Count && i < parsed.Count; i++)
        {
            var result = parsed[i];
            if (result.ReturnCode != 0xFF || result.Data.IsEmpty)
            {
                dict[tags[i].Name!] = default;
                continue;
            }

            dict[tags[i].Name!] = parseBytes(varTypes[i], result.Data.ToArray(), arrayLengths[i]);
        }

        return dict;
    }

    /// <summary>Attempts to serialize the new value of the specified tag into a byte array suitable for transport.</summary>
    /// <remarks>The method supports serialization for common primitive types, arrays of bytes, and strings.
    /// If the tag's type is not supported or the new value is null, the method returns false and outputs an empty
    /// array.</remarks>
    /// <param name="tag">The tag whose new value is to be serialized. The tag's type and new value determine the serialization format.</param>
    /// <param name="transportSize">When this method returns, contains the transport size code associated with the serialized data, if serialization
    /// succeeds.</param>
    /// <param name="data">When this method returns, contains the serialized byte array representation of the tag's new value, if
    /// serialization succeeds; otherwise, an empty array.</param>
    /// <returns>true if the tag's new value was successfully serialized; otherwise, false.</returns>
    private static bool TrySerializeTagNewValue(Tag tag, out byte transportSize, out byte[] data)
    {
        transportSize = 2;
        data = [];

        return tag.NewValue is not null &&
            (TrySerializeScalarTagValue(tag, out data) || TrySerializeArrayTagValue(tag, out data));
    }

    /// <summary>Attempts to serialize a scalar tag value.</summary>
    /// <param name="tag">The tag to serialize.</param>
    /// <param name="data">The serialized bytes.</param>
    /// <returns>true when serialization succeeds; otherwise, false.</returns>
    private static bool TrySerializeScalarTagValue(Tag tag, out byte[] data)
    {
        data = [];
        switch (tag.Type.Name)
        {
            case "Boolean" or "Byte":
                {
                    data = [(byte)Convert.ChangeType(tag.NewValue, typeof(byte))!];
                    return true;
                }

            case "Int16" or "short":
                {
                    data = Int.ToByteArray((short)tag.NewValue!);
                    return true;
                }

            case "UInt16" or "ushort":
                {
                    data = Word.ToByteArray((ushort)Convert.ChangeType(tag.NewValue, typeof(ushort))!);
                    return true;
                }

            case "Int32" or "int":
                {
                    data = DInt.ToByteArray((int)tag.NewValue!);
                    return true;
                }

            case "UInt32" or "uint":
                {
                    data = DWord.ToByteArray((uint)Convert.ChangeType(tag.NewValue, typeof(uint))!);
                    return true;
                }

            case "Single":
                {
                    data = Real.ToByteArray((float)tag.NewValue!);
                    return true;
                }

            case "Double":
                {
                    data = LReal.ToByteArray((double)tag.NewValue!);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Attempts to serialize an array or string tag value.</summary>
    /// <param name="tag">The tag to serialize.</param>
    /// <param name="data">The serialized bytes.</param>
    /// <returns>true when serialization succeeds; otherwise, false.</returns>
    private static bool TrySerializeArrayTagValue(Tag tag, out byte[] data)
    {
        data = [];
        switch (tag.Type.Name)
        {
            case "Byte[]":
                {
                    data = (byte[])tag.NewValue!;
                    return true;
                }

            case "Int16[]" or "short[]":
                {
                    data = Int.ToByteArray((short[])tag.NewValue!);
                    return true;
                }

            case "UInt16[]" or "ushort[]":
                {
                    data = Word.ToByteArray((ushort[])tag.NewValue!);
                    return true;
                }

            case "Int32[]" or "int[]":
                {
                    data = DInt.ToByteArray((int[])tag.NewValue!);
                    return true;
                }

            case "UInt32[]" or "uint[]":
                {
                    data = DWord.ToByteArray((uint[])Convert.ChangeType(tag.NewValue, typeof(uint[]))!);
                    return true;
                }

            case "Single[]":
                {
                    data = Real.ToByteArray((float[])tag.NewValue!);
                    return true;
                }

            case "Double[]":
                {
                    data = LReal.ToByteArray((double[])tag.NewValue!);
                    return true;
                }

            case "String":
                {
                    data = PlcTypes.String.ToByteArray(tag.NewValue as string);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>
    /// Attempts to convert the specified read-only character span representation of a number to its 32-bit signed
    /// integer equivalent.
    /// </summary>
    /// <remarks>The conversion uses the invariant culture and expects the input to be in a valid integer
    /// format. No exception is thrown if the conversion fails.</remarks>
    /// <param name="s">A read-only span of characters that contains the number to convert.</param>
    /// <param name="value">When this method returns, contains the 32-bit signed integer value equivalent to the number contained in
    /// <paramref name="s"/>, if the conversion succeeded, or zero if the conversion failed. This parameter is passed
    /// uninitialized.</param>
    /// <returns><see langword="true"/> if the conversion succeeded; otherwise, <see langword="false"/>.</returns>
    private static bool TryParseInt(ReadOnlySpan<char> s, out int value) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>Attempts to parse a data block address for a multi-variable tag and extract its components.</summary>
    /// <remarks>The method supports addresses in the format "DBx.DBByy", "DBx.DBWyy", or "DBx.DBDyy", where x
    /// is the data block number and yy is the byte offset. The variable type is inferred from both the address and the
    /// tag's Type property. This method does not throw exceptions for invalid input; instead, it returns false and sets
    /// output values to their defaults.</remarks>
    /// <param name="tag">The tag containing the address and type information to parse. The tag's Address property must be a non-empty
    /// string, and ArrayLength must have a value.</param>
    /// <param name="db">When this method returns, contains the parsed data block number if parsing succeeds; otherwise, zero.</param>
    /// <param name="startByte">When this method returns, contains the starting byte offset within the data block if parsing succeeds;
    /// otherwise, zero.</param>
    /// <param name="varType">When this method returns, contains the variable type determined from the address and tag type if parsing
    /// succeeds; otherwise, <see cref="VarType.Byte"/>.</param>
    /// <param name="countBytes">When this method returns, contains the total number of bytes required for the variable or array if parsing
    /// succeeds; otherwise, zero.</param>
    /// <returns>true if the address was successfully parsed and all output values were set; otherwise, false.</returns>
    private static bool TryParseDbAddressForMultiVar(Tag tag, out int db, out int startByte, out VarType varType, out int countBytes)
    {
        db = 0;
        startByte = 0;
        varType = VarType.Byte;
        countBytes = 0;

        if (tag.ArrayLength is null || !TryParseDbAddressParts(tag.Address, out db, out var dbType, out startByte))
        {
            return false;
        }

        switch (dbType)
        {
            case "DBB":
                {
                    varType = VarType.Byte;
                    break;
                }

            case "DBW":
                {
                    varType = tag.Type == typeof(short) || tag.Type == typeof(short[]) ? VarType.Int : VarType.Word;
                    break;
                }

            case "DBD":
                {
                    varType = GetDbdMultiVarType(tag.Type);
                    break;
                }

            default:
                {
                    return false;
                }
        }

        countBytes = VarTypeToByteLength(varType, tag.ArrayLength.Value);
        return countBytes > 0;
    }

    /// <summary>Attempts to parse the DB number, type, and start byte from an S7 DB address.</summary>
    /// <param name="address">The source address.</param>
    /// <param name="db">The parsed DB number.</param>
    /// <param name="dbType">The parsed DB type prefix.</param>
    /// <param name="startByte">The parsed start byte.</param>
    /// <returns>true when the address parts are valid; otherwise, false.</returns>
    private static bool TryParseDbAddressParts(string? address, out int db, out string dbType, out int startByte)
    {
        db = 0;
        dbType = string.Empty;
        startByte = 0;

        if (address is null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalized = address.Replace(" ", string.Empty).ToUpperInvariant();
        if (!normalized.AsSpan().StartsWith("DB".AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        var parts = normalized.Split(['.']);
        if (parts.Length < 2 || !TryParseInt(parts[0].AsSpan(2), out db))
        {
            return false;
        }

        var item = parts[1];
        if (item.Length < 3 || !TryParseInt(item.AsSpan(3), out startByte))
        {
            return false;
        }

        dbType = item[..3];
        return true;
    }

    /// <summary>Gets the multi-var type for a DBD address from the tag value type.</summary>
    /// <param name="tagType">The tag value type.</param>
    /// <returns>The multi-var type.</returns>
    private static VarType GetDbdMultiVarType(Type tagType) => tagType switch
    {
        _ when tagType == typeof(double) || tagType == typeof(double[]) => VarType.LReal,
        _ when tagType == typeof(float) || tagType == typeof(float[]) => VarType.Real,
        _ when tagType == typeof(int) || tagType == typeof(int[]) => VarType.DInt,
        _ => VarType.DWord,
    };

    /// <summary>
    /// Determines whether the specified tag is non-null and its type and value are compatible with the specified type
    /// parameter.
    /// </summary>
    /// <remarks>If the type parameter is object, any non-null tag is considered valid regardless of its type
    /// or value.</remarks>
    /// <typeparam name="T">The type to validate against the tag's type and value.</typeparam>
    /// <param name="tag">The tag to validate. May be null.</param>
    /// <returns>true if the tag is not null and its type and value match the specified type parameter; otherwise, false.</returns>
    private static bool TagValueIsValid<T>(Tag? tag) => tag is not null && (typeof(T) == typeof(object) || (tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T)));

    /// <summary>
    /// Determines whether the specified tag's name matches the given variable and its type and value are compatible
    /// with the specified type parameter.
    /// </summary>
    /// <remarks>If T is object, only the name comparison is performed. For other types, both the tag's type
    /// and the runtime type of its value must match T.</remarks>
    /// <typeparam name="T">The type to compare against the tag's type and value.</typeparam>
    /// <param name="tag">The tag to validate. May be null.</param>
    /// <param name="variable">The variable name to compare with the tag's name. The comparison is case-insensitive. May be null.</param>
    /// <returns>true if the tag's name matches the variable and the tag's type and value are compatible with type T; otherwise,
    /// false.</returns>
    private static bool TagValueIsValid<T>(Tag? tag, string? variable) => string.Equals(tag?.Name, variable, StringComparison.InvariantCultureIgnoreCase) && (typeof(T) == typeof(object) || (tag?.Type == typeof(T) && tag.Value?.GetType() == typeof(T)));

    /// <summary>
    /// Creates a request package for reading data from a PLC using the specified data type, data block, start address,
    /// and count.
    /// </summary>
    /// <remarks>The structure of the request package varies depending on the specified data type. For Timer
    /// and Counter types, addressing and formatting differ from other data types. Ensure that the parameters are within
    /// valid ranges supported by the PLC protocol.</remarks>
    /// <param name="dataType">The type of data to read from the PLC. Determines the format and addressing of the request.</param>
    /// <param name="db">The data block number from which to read. Must be a non-negative integer.</param>
    /// <param name="startByteAdr">The starting byte address within the data block from which to begin reading. Must be a non-negative integer.</param>
    /// <param name="count">The number of data items to read. Must be a positive integer. The default value is 1.</param>
    /// <returns>A ByteArray containing the constructed request package to be sent to the PLC for reading the specified data.</returns>
    private static ByteArray CreateReadDataRequestPackage(DataType dataType, int db, int startByteAdr, int count = 1)
    {
        // single data register = 12
        var package = new ByteArray(12);
        package.Add([18, 10, 16]);
        switch (dataType)
        {
            case DataType.Timer or DataType.Counter:
                {
                    package.Add((byte)dataType);
                    break;
                }

            default:
                {
                    package.Add(2);
                    break;
                }
        }

        package.Add(Word.ToByteArray((ushort)count));
        package.Add(Word.ToByteArray((ushort)db));
        package.Add((byte)dataType);
        var overflow = (int)(startByteAdr * 8 / 65_535U); // handles words with address bigger than 8191
        package.Add((byte)overflow);
        switch (dataType)
        {
            case DataType.Timer or DataType.Counter:
                {
                    package.Add(Word.ToByteArray((ushort)startByteAdr));
                    break;
                }

            default:
                {
                    package.Add(Word.ToByteArray((ushort)(startByteAdr * 8)));
                    break;
                }
        }

        return package;
    }

    /// <summary>Creates a header package for a specified number of requests.</summary>
    /// <remarks>The returned header package is formatted with a fixed header size and includes fields that
    /// depend on the specified amount. This method is intended for use in constructing protocol-compliant request
    /// headers.</remarks>
    /// <param name="amount">The number of requests to include in the header package. Must be greater than or equal to 1. The default value
    /// is 1.</param>
    /// <returns>A ByteArray containing the constructed header package for the specified number of requests.</returns>
    private static ByteArray ReadHeaderPackage(int amount = 1)
    {
        // header size = 19 bytes
        var package = new ByteArray(19);
        package.Add([3, 0, 0]);

        // complete package size
        package.Add((byte)(19 + (12 * amount)));
        package.Add([2, 240, 128, 50, 1, 0, 0, 0, 0]);

        // data part size
        package.Add(Word.ToByteArray((ushort)(2 + (amount * 12))));
        package.Add([0, 0, 4]);

        // amount of requests
        package.Add((byte)amount);

        return package;
    }

    /// <summary>Calculates the total number of bytes required to represent a value or array of the specified variable type.</summary>
    /// <remarks>This method is typically used to determine buffer sizes or offsets when working with raw data
    /// representations of various variable types. The calculation depends on the size of each type and the number of
    /// elements specified.</remarks>
    /// <param name="varType">The variable type for which to determine the byte length.</param>
    /// <param name="varCount">The number of elements of the specified variable type. Must be greater than or equal to 1.</param>
    /// <returns>The total number of bytes required to store the specified number of elements of the given variable type. Returns
    /// 0 if the variable type is not recognized.</returns>
    private static int VarTypeToByteLength(VarType varType, int varCount = 1) => varType switch
    {
        VarType.Bit => varCount, // TODO
        VarType.Byte => (varCount < 1) ? 1 : varCount,
        VarType.String => varCount,
        VarType.Word or VarType.Timer or VarType.Int or VarType.Counter => varCount * 2,
        VarType.DWord or VarType.DInt or VarType.Real => varCount * 4,
        VarType.LReal => varCount * 8,
        _ => 0,
    };

    /// <summary>
    /// Writes a single variable from the PLC, takes in input strings like "DB1.DBX0.0",
    /// "DB20.DBD200", "MB20", "T45", etc. If the write was not successful, check LastErrorCode
    /// or LastErrorString.
    /// </summary>
    /// <param name="tag">The tag. For the Address Input strings like "DB1.DBX0.0", "DB20.DBD200", "MB20", "T45", etc.</param>
    private void Write(Tag tag)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(tag.Address);
#else
        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new ArgumentNullException(nameof(tag.Address));
        }
#endif

        if (tag.NewValue is null)
        {
            throw new ArgumentNullException(nameof(tag.NewValue));
        }

        _plcRequestSubject.OnNext(new(PLCRequestType.Write, tag));
    }

    /// <summary>
    /// Writes up to 200 bytes to the PLC and returns NoError if successful. You must specify
    /// the memory area type, memory are address, byte start address and bytes count. If the
    /// read was not successful, check LastErrorCode or LastErrorString.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="dataType">
    /// Data type of the memory area, can be DB, Timer, Counter, Memory, Input, Output.
    /// </param>
    /// <param name="db">
    /// Address of the memory area (if you want to read DB1, this is set to 1). This must be set
    /// also for other memory area types: counters, timers,etc.
    /// </param>
    /// <param name="startByteAdr">
    /// Start byte address. If you want to read DB1.DBW200, this is 200.
    /// </param>
    /// <param name="value">
    /// Bytes to write. The length of this parameter can't be higher than 200. If you need more,
    /// use recursion.
    /// </param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    private bool WriteBytes(Tag tag, DataType dataType, int db, int startByteAdr, byte[] value)
    {
        lock (_socketLock)
        {
            var receiveBuffer = new byte[1024];
            try
            {
                var varCount = value.Length;

                // first create the header
                var packageSize = 35 + value.Length;
                var package = new ByteArray(packageSize);

                package.Add([3, 0, 0]);
                package.Add((byte)packageSize);
                package.Add([2, 240, 128, 50, 1, 0, 0]);
                package.Add(Word.ToByteArray((ushort)(varCount - 1)));
                package.Add([0, 14]);
                package.Add(Word.ToByteArray((ushort)(varCount + 4)));
                package.Add([5, 1, 18, 10, 16, 2]);
                package.Add(Word.ToByteArray((ushort)varCount));
                package.Add(Word.ToByteArray((ushort)db));
                package.Add((byte)dataType);
                var overflow = (int)(startByteAdr * 8 / 0xffffU); // handles words with address bigger than 8191
                package.Add((byte)overflow);
                package.Add(Word.ToByteArray((ushort)(startByteAdr * 8)));
                package.Add([0, 4]);
                package.Add(Word.ToByteArray((ushort)(varCount * 8)));

                // now join the header and the data
                package.Add(value);

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return false;
                }

                var result = _socketRx.Receive(tag, receiveBuffer, 1024);

                if (receiveBuffer[21] != 0xff)
                {
                    _lastErrorCode.OnNext(ErrorCode.WriteData);
                    _lastError.OnNext($"Tag {tag.Name} failed to write - {nameof(ErrorCode.WrongNumberReceivedBytes)} code {receiveBuffer[21]}");
                    return false;
                }

                _lastErrorCode.OnNext(ErrorCode.NoError);
                return true;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return false;
            }
        }
    }

    /// <summary>Attempts to read a value for the specified tag and assigns it to the tag if successful.</summary>
    /// <remarks>If the tag cannot be read or an error occurs, the tag's value is not modified.</remarks>
    /// <param name="tag">The tag for which to retrieve and assign a value. If <paramref name="tag"/> is <see langword="null"/>, no action
    /// is taken.</param>
    private void GetTagValue(Tag? tag)
    {
        var result = Read(tag);
        if (tag is null || result is null || result is ErrorCode)
        {
            return;
        }

        tag.Value = result;
    }

    /// <summary>Parses a byte array into an object of the specified variable type and count.</summary>
    /// <remarks>The returned object type depends on the specified <paramref name="varType"/> and <paramref
    /// name="varCount"/>. For example, if <paramref name="varType"/> is <c>Word</c> and <paramref name="varCount"/> is
    /// 1, a single <c>Word</c> object is returned; if <paramref name="varCount"/> is greater than 1, an array of
    /// <c>Word</c> objects is returned. For <c>Bit</c> type, a <see cref="bool"/> is returned. If an error
    /// occurs during parsing, the method returns <see langword="null"/>.</remarks>
    /// <param name="varType">The type of variable to parse the byte array as. Determines the interpretation of the data in <paramref
    /// name="bytes"/>.</param>
    /// <param name="bytes">The byte array containing the raw data to parse. If <see langword="null"/>, the method returns <see
    /// langword="null"/>.</param>
    /// <param name="varCount">The number of variables to parse from the byte array. Must be 1 for scalar values; greater than 1 returns an
    /// array of values.</param>
    /// <returns>An object representing the parsed value(s) according to <paramref name="varType"/> and <paramref
    /// name="varCount"/>. Returns a single value if <paramref name="varCount"/> is 1, or an array of values if greater
    /// than 1. Returns <see langword="null"/> if <paramref name="bytes"/> is <see langword="null"/> or if the type is
    /// not recognized.</returns>
    private object? ParseBytes(VarType varType, byte[] bytes, int varCount)
    {
        try
        {
            return bytes is null ? default : RxS7ValueHelpers.ParseNonNullBytes(varType, bytes, varCount);
        }
        catch (Exception ex)
        {
            _lastError.OnNext(ex.Message);
        }

        return default;
    }

    /// <summary>
    /// Read and decode a certain number of bytes of the "VarType" provided. This can be used to
    /// read multiple consecutive variables of the same type (Word, DWord, Int, etc). If the
    /// read was not successful, check LastErrorCode or LastErrorString.
    /// </summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="tag">The tag.</param>
    /// <param name="dataType">
    /// Data type of the memory area, can be DB, Timer, Counter, Memory, Input, Output.
    /// </param>
    /// <param name="db">
    /// Address of the memory area (if you want to read DB1, this is set to 1). This must be set
    /// also for other memory area types: counters, timers,etc.
    /// </param>
    /// <param name="startByteAdr">
    /// Start byte address. If you want to read DB1.DBW200, this is 200.
    /// </param>
    /// <param name="varType">Type of the variable/s that you are reading.</param>
    /// <returns>An object.</returns>
    private T? Read<T>(Tag tag, DataType dataType, int db, int startByteAdr, VarType varType)
    {
        try
        {
            _lock.Wait();
            var cntBytes = VarTypeToByteLength(varType, tag.ArrayLength!.Value);
            var bytes = ReadMultipleBytes(tag, dataType, db, startByteAdr, cntBytes);
            return bytes?.Length > 0 ? (T?)ParseBytes(varType, bytes!, tag.ArrayLength!.Value) : default;
        }
        catch (Exception ex)
        {
            _lastError.OnNext(ex.Message);
        }
        finally
        {
            _ = _lock.Release();
        }

        return default;
    }

    /// <summary>
    /// Reads the value from the PLC at the address specified by the given tag, interpreting the data type and memory
    /// area based on the tag's address and type.
    /// </summary>
    /// <remarks>The method determines the PLC memory area and data type to read based on the format of the
    /// tag's Address property and the Type property. Supported areas include data blocks, inputs, outputs, memory,
    /// timers, and counters. The caller should ensure that the tag's Type matches the expected data at the specified
    /// address. If the address format is invalid or unsupported, the method returns <see langword="false"/> and sets
    /// error information.</remarks>
    /// <param name="tag">The tag that specifies the PLC address and expected data type to read. The tag's Address property must not be
    /// null, empty, or whitespace.</param>
    /// <returns>An object containing the value read from the PLC at the specified address. The type of the returned object
    /// depends on the tag's Type property and the address format. Returns <see langword="false"/> if the read operation
    /// fails due to an invalid address or format.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="tag"/> is null or if its Address property is null, empty, or consists only of
    /// whitespace.</exception>
    private object? Read(Tag? tag)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(tag?.Address);
#else
        if (string.IsNullOrWhiteSpace(tag?.Address))
        {
            throw new ArgumentNullException(nameof(tag));
        }
#endif

        // remove spaces
        var correctVariable = tag!.Address!.ToUpper().Replace(" ", string.Empty);

        try
        {
            return correctVariable[..2] switch
            {
                "DB" => ReadDataBlockAddress(tag, correctVariable),
                "EB" => ReadByteAddress(tag, DataType.Input, int.Parse(correctVariable[2..])),
                "EW" => ReadWordAddress(tag, DataType.Input, int.Parse(correctVariable[2..]), VarType.Word, VarType.Word),
                "ED" => ReadAreaDWordAddress(tag, DataType.Input, int.Parse(correctVariable[2..])),
                "AB" => ReadByteAddress(tag, DataType.Output, int.Parse(correctVariable[2..])),
                "AW" => ReadWordAddress(tag, DataType.Output, int.Parse(correctVariable[2..]), VarType.Word, VarType.Word),
                "AD" => ReadAreaDWordAddress(tag, DataType.Output, int.Parse(correctVariable[2..])),
                "MB" => ReadByteAddress(tag, DataType.Memory, int.Parse(correctVariable[2..])),
                "MW" => ReadWordAddress(tag, DataType.Memory, int.Parse(correctVariable[2..]), VarType.Word, VarType.Word),
                "MD" => ReadMemoryDWordAddress(tag, int.Parse(correctVariable[2..])),
                _ => ReadSpecialOrBitAddress(tag, correctVariable),
            };
        }
        catch
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext("The variable'" + tag.Address + "' could not be read. Please check the syntax and try again.");
            return false;
        }
    }

    /// <summary>Reads a data block address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The value read from the data block address.</returns>
    private object? ReadDataBlockAddress(Tag tag, string correctVariable)
    {
        var strings = correctVariable.Split(['.']);
        if (strings.Length < 2)
        {
            throw new ArgumentException($"Cannot parse DB address '{correctVariable}'.", nameof(tag));
        }

        var dbNumber = int.Parse(strings[0][2..]);
        var dbType = strings[1][..3];
        var dbIndex = int.Parse(strings[1][3..]);
        return dbType switch
        {
            "DBB" => ReadByteAddress(tag, DataType.DataBlock, dbIndex, dbNumber),
            "DBW" => ReadWordAddress(tag, DataType.DataBlock, dbIndex, VarType.Int, VarType.Word, dbNumber),
            "DBD" => ReadDataBlockDWordAddress(tag, dbNumber, dbIndex),
            "DBX" => ReadDataBlockBitAddress(tag, strings, dbNumber, dbIndex),
            _ => throw new ArgumentException($"Unable to parse DB address type '{dbType}'.", nameof(tag)),
        };
    }

    /// <summary>Reads a byte address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <param name="db">The data block number.</param>
    /// <returns>The byte value or array.</returns>
    private object? ReadByteAddress(Tag tag, DataType dataType, int startByteAdr, int db = 0) =>
        tag.Type == typeof(byte[])
            ? Read<byte[]>(tag, dataType, db, startByteAdr, VarType.Byte)
            : Read<byte>(tag, dataType, db, startByteAdr, VarType.Byte);

    /// <summary>Reads a word address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <param name="signedVarType">The variable type for signed values.</param>
    /// <param name="unsignedVarType">The variable type for unsigned values.</param>
    /// <param name="db">The data block number.</param>
    /// <returns>The word value or array.</returns>
    private object? ReadWordAddress(Tag tag, DataType dataType, int startByteAdr, VarType signedVarType, VarType unsignedVarType, int db = 0)
    {
        if (tag.Type == typeof(short[]))
        {
            return Read<short[]>(tag, dataType, db, startByteAdr, signedVarType);
        }

        if (tag.Type == typeof(short))
        {
            return Read<short>(tag, dataType, db, startByteAdr, signedVarType);
        }

        return tag.Type == typeof(ushort[])
            ? Read<ushort[]>(tag, dataType, db, startByteAdr, unsignedVarType)
            : Read<ushort>(tag, dataType, db, startByteAdr, unsignedVarType);
    }

    /// <summary>Reads a data block double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The double-word value or array.</returns>
    private object? ReadDataBlockDWordAddress(Tag tag, int db, int startByteAdr)
    {
        if (tag.Type == typeof(double))
        {
            return Read<double>(tag, DataType.DataBlock, db, startByteAdr, VarType.LReal);
        }

        if (tag.Type == typeof(double[]))
        {
            return Read<double[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.LReal);
        }

        if (tag.Type == typeof(float))
        {
            return Read<float>(tag, DataType.DataBlock, db, startByteAdr, VarType.Real);
        }

        if (tag.Type == typeof(float[]))
        {
            return Read<float[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.Real);
        }

        if (tag.Type == typeof(int))
        {
            return Read<int>(tag, DataType.DataBlock, db, startByteAdr, VarType.DInt);
        }

        if (tag.Type == typeof(int[]))
        {
            return Read<int[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.DInt);
        }

        return tag.Type == typeof(uint[])
            ? Read<uint[]>(tag, DataType.DataBlock, db, startByteAdr, VarType.DWord)
            : Read<uint>(tag, DataType.DataBlock, db, startByteAdr, VarType.DWord);
    }

    /// <summary>Reads a non-data-block double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data area type.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The double-word value or array.</returns>
    private object? ReadAreaDWordAddress(Tag tag, DataType dataType, int startByteAdr)
    {
        if (tag.Type == typeof(uint[]))
        {
            return Read<uint[]>(tag, dataType, 0, startByteAdr, VarType.DWord);
        }

        if (tag.Type == typeof(int[]))
        {
            return Read<int[]>(tag, dataType, 0, startByteAdr, VarType.DWord);
        }

        return tag.Type == typeof(int)
            ? Read<int>(tag, dataType, 0, startByteAdr, VarType.DWord)
            : Read<uint>(tag, dataType, 0, startByteAdr, VarType.DWord);
    }

    /// <summary>Reads a memory double-word address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="startByteAdr">The byte offset.</param>
    /// <returns>The memory double-word value or array.</returns>
    private object? ReadMemoryDWordAddress(Tag tag, int startByteAdr) =>
        tag.Type == typeof(double[])
            ? Read<double[]>(tag, DataType.Memory, 0, startByteAdr, VarType.LReal)
            : Read<double>(tag, DataType.Memory, 0, startByteAdr, VarType.LReal);

    /// <summary>Reads a data block bit address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="strings">The parsed address components.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="byteOffset">The byte offset.</param>
    /// <returns>The bit value.</returns>
    private bool ReadDataBlockBitAddress(Tag tag, string[] strings, int db, int byteOffset)
    {
        var bitOffset = int.Parse(strings[2]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, DataType.DataBlock, db, byteOffset, VarType.Byte);
        return RxS7ValueHelpers.GetBit(value, bitOffset);
    }

    /// <summary>Reads a timer, counter, or bit address outside data blocks.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The value read from the address.</returns>
    private object? ReadSpecialOrBitAddress(Tag tag, string correctVariable)
    {
        return correctVariable[..1] switch
        {
            "E" or "I" => ReadBitAddress(tag, correctVariable, DataType.Input),
            "A" or "O" => ReadBitAddress(tag, correctVariable, DataType.Output),
            "M" => ReadBitAddress(tag, correctVariable, DataType.Memory),
            "T" => ReadTimerAddress(tag, correctVariable),
            "Z" or "C" => ReadCounterAddress(tag, correctVariable),
            _ => throw new ArgumentException($"Unknown variable type {correctVariable[..1]}.", nameof(tag)),
        };
    }

    /// <summary>Reads a bit address from the specified data area.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <param name="dataType">The data area type.</param>
    /// <returns>The bit value.</returns>
    private bool ReadBitAddress(Tag tag, string correctVariable, DataType dataType)
    {
        var addressLocation = correctVariable[1..];
        var decimalPointIndex = addressLocation.IndexOf('.');
        if (decimalPointIndex == -1)
        {
            throw new ArgumentException($"Cannot parse variable {correctVariable}. Input, Output, Memory Address, Timer, and Counter types require bit-level addressing (e.g. I0.1).", nameof(tag));
        }

        var byteOffset = int.Parse(addressLocation[..decimalPointIndex]);
        var bitOffset = int.Parse(addressLocation[(decimalPointIndex + 1)..]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, dataType, 0, byteOffset, VarType.Byte);
        return RxS7ValueHelpers.GetBit(value, bitOffset);
    }

    /// <summary>Reads a timer address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The timer value or array.</returns>
    private object? ReadTimerAddress(Tag tag, string correctVariable) =>
        tag.Type == typeof(double[])
            ? Read<double[]>(tag, DataType.Timer, 0, int.Parse(correctVariable[2..]), VarType.Timer)
            : Read<double>(tag, DataType.Timer, 0, int.Parse(correctVariable[1..]), VarType.Timer);

    /// <summary>Reads a counter address.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="correctVariable">The normalized variable address.</param>
    /// <returns>The counter value or array.</returns>
    private object? ReadCounterAddress(Tag tag, string correctVariable)
    {
        if (tag.Type == typeof(ushort[]))
        {
            return Read<ushort[]>(tag, DataType.Counter, 0, int.Parse(correctVariable[2..]), VarType.Counter);
        }

        if (tag.Type == typeof(short[]))
        {
            return Read<short[]>(tag, DataType.Counter, 0, int.Parse(correctVariable[2..]), VarType.Counter);
        }

        return tag.Type == typeof(short)
            ? Read<short>(tag, DataType.Counter, 0, int.Parse(correctVariable[2..]), VarType.Counter)
            : Read<ushort>(tag, DataType.Counter, 0, int.Parse(correctVariable[1..]), VarType.Counter);
    }

    /// <summary>Reads a sequence of bytes from the specified data block and address using the given tag and data type.</summary>
    /// <remarks>If the read operation fails or an error occurs, the method returns null and updates the error
    /// state. The method is thread-safe.</remarks>
    /// <param name="tag">The tag that identifies the target device or connection for the read operation.</param>
    /// <param name="dataType">The data type to use when reading from the data block. Determines how the data is interpreted.</param>
    /// <param name="db">The number of the data block to read from.</param>
    /// <param name="startByteAdr">The zero-based starting byte address within the data block from which to begin reading.</param>
    /// <param name="count">The number of bytes to read from the specified address. Must be greater than zero.</param>
    /// <returns>A byte array containing the data read from the specified location, or null if the read operation fails.</returns>
    private byte[]? ReadBytes(Tag tag, DataType dataType, int db, int startByteAdr, int count)
    {
        lock (_socketLock)
        {
            try
            {
                var bytes = new byte[count];
                const int packageSize = 31;
                var package = new ByteArray(packageSize);
                package.Add(ReadHeaderPackage());
                package.Add(CreateReadDataRequestPackage(dataType, db, startByteAdr, count));

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return default;
                }

                var receiveSize = Math.Max(_socketRx.DataReadLength + 256, count + 256);
                var receiveBuffer = new byte[receiveSize];
                var result = _socketRx.ReceiveIsoData(tag, ref receiveBuffer);
                if (result < 25)
                {
                    return default;
                }

                if (receiveBuffer[21] != 0xFF)
                {
                    return default;
                }

                var availableDataLength = RxS7ValueHelpers.GetReadResponseDataLengthBytes(receiveBuffer, result);
                if (availableDataLength <= 0)
                {
                    return default;
                }

                var bytesToCopy = Math.Min(count, availableDataLength);
                Array.Copy(receiveBuffer, 25, bytes, 0, bytesToCopy);
                if (bytesToCopy == count)
                {
                    return bytes;
                }

                var trimmed = new byte[bytesToCopy];
                Array.Copy(bytes, 0, trimmed, 0, bytesToCopy);
                return trimmed;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return default;
            }
        }
    }

    /// <summary>Reads a specified number of bytes from the given data block and starting address using the provided tag and data type.</summary>
    /// <remarks>The method attempts to read the requested bytes in chunks, retrying up to three times per
    /// chunk if necessary. If any chunk cannot be read after retries, the method returns an empty array. The returned
    /// array may be empty if the operation fails.</remarks>
    /// <param name="tag">The tag that identifies the target device or memory area to read from.</param>
    /// <param name="dataType">The data type that determines how the bytes are interpreted during the read operation.</param>
    /// <param name="db">The number of the data block from which to read.</param>
    /// <param name="startByteAdr">The zero-based starting byte address within the data block.</param>
    /// <param name="numBytes">The total number of bytes to read from the specified starting address.</param>
    /// <returns>A byte array containing the data read from the specified location. Returns an empty array if the read operation
    /// fails.</returns>
    private byte[] ReadMultipleBytes(Tag tag, DataType dataType, int db, int startByteAdr, int numBytes)
    {
        try
        {
            var resultBytes = new List<byte>();
            var index = startByteAdr;
            var maxChunkSize = Math.Max(1, _socketRx.DataReadLength - 32);
            var chunkSize = tag.Type == typeof(byte[]) ? Math.Min(480, maxChunkSize) : maxChunkSize;
            while (numBytes > 0)
            {
                var maxToRead = Math.Min(numBytes, chunkSize);
                var bytes = ReadBytesWithRetries(tag, dataType, db, index, maxToRead);

                if (bytes is null || bytes.Length == 0)
                {
                    if (maxToRead > 1)
                    {
                        chunkSize = Math.Max(1, maxToRead / 2);
                        continue;
                    }

                    _lastErrorCode.OnNext(ErrorCode.ReadData);
                    _lastError.OnNext($"Tag {tag.Name} failed to read - unable to read chunk at DB{db}.DBB{index}.");
                    return [];
                }

                resultBytes.AddRange(bytes);
                numBytes -= bytes.Length;
                index += bytes.Length;

                if (bytes.Length < maxToRead && maxToRead > 1)
                {
                    chunkSize = Math.Max(1, bytes.Length);
                }
            }

            return [.. resultBytes];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Reads a byte chunk with a small retry window.</summary>
    /// <param name="tag">The tag to read.</param>
    /// <param name="dataType">The data type.</param>
    /// <param name="db">The data block number.</param>
    /// <param name="index">The byte index.</param>
    /// <param name="maxToRead">The maximum bytes to read.</param>
    /// <returns>The read bytes, or null when all attempts fail.</returns>
    private byte[]? ReadBytesWithRetries(Tag tag, DataType dataType, int db, int index, int maxToRead)
    {
        for (var i = 0; i < 3; i++)
        {
            var bytes = ReadBytes(tag, dataType, db, index, maxToRead);
            if (bytes?.Length > 0)
            {
                return bytes;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an observable sequence that periodically reads tags at the specified interval, emitting a notification
    /// each time the read operation is performed.
    /// </summary>
    /// <remarks>The observable automatically manages connection state and pauses polling if no tags are
    /// available or polling is paused. Errors encountered during tag reading are handled internally and do not
    /// terminate the observable sequence. The returned observable is shared among all subscribers and begins polling
    /// when the first subscription is made.</remarks>
    /// <param name="interval">The polling interval, in milliseconds, between consecutive tag read operations. Must be greater than zero.</param>
    /// <returns>An observable sequence that emits a value each time the tag reading process completes. The sequence completes
    /// when unsubscribed.</returns>
    private IObservable<Unit> TagReaderObservable(double interval) =>
        Observable.Create<Unit>(__ =>
            {
                var tim = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                    .Subscribe(async _ =>
                    {
                        if (!IsConnectedValue)
                        {
                            return;
                        }

                        if (DateTime.UtcNow - _lastConnectedAtUtc < TimeSpan.FromMilliseconds(250))
                        {
                            NotifyPaused(true);
                            return;
                        }

                        var tagList = new List<Tag>();
                        foreach (var item in TagList)
                        {
                            if (item is Tag { DoNotPoll: false } tag)
                            {
                                tagList.Add(tag);
                            }
                        }

                        if (tagList.Count == 0 || _pause)
                        {
                            NotifyPaused(true);
                            return;
                        }

                        _stopwatch.Restart();
                        NotifyPaused(false);
                        try
                        {
                            foreach (var tag in tagList)
                            {
                                try
                                {
                                    while (!IsConnectedValue)
                                    {
                                        await Task.Delay(10);
                                    }

                                    _plcRequestSubject.OnNext(new PLCRequest(PLCRequestType.Read, tag));
                                }
                                catch (Exception ex)
                                {
                                    _lastError.OnNext(ex.Message);
                                    _status.OnNext($"{tag.Name} could not be read from {tag.Address}. Error: " + ex);
                                }
                            }
                        }
                        finally
                        {
                            _stopwatch.Stop();
                            _readTime.OnNext(_stopwatch.ElapsedTicks);
                        }
                    });

                return new SingleAssignmentDisposable { Disposable = tim };
            }).OnErrorRetry().Publish().RefCount();

    /// <summary>Notifies pause observers while tolerating timer callbacks racing with disposal.</summary>
    /// <param name="isPaused">The pause state to publish.</param>
    private void NotifyPaused(bool isPaused)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            _paused.OnNext(isPaused);
        }
        catch (ObjectDisposedException)
        {
            // A timer callback can overlap disposal; in that case the pause notification is no longer observable.
        }
    }

    /// <summary>
    /// Creates an observable sequence that periodically writes a watchdog value to the configured address, enabling
    /// external monitoring of connection health.
    /// </summary>
    /// <remarks>The observable will not emit any values if the watchdog address is null or whitespace. The
    /// sequence automatically retries on errors and is shared among all subscribers. The observable is considered
    /// active as long as there is at least one subscription.</remarks>
    /// <returns>An observable sequence that completes if the watchdog address is not defined, or emits a value each time the
    /// watchdog is written.</returns>
    private IObservable<Unit> WatchDogObservable() =>
        Observable.Create<Unit>(obs =>
        {
            if (string.IsNullOrWhiteSpace(WatchDogAddress))
            {
                // disable watchdog if not defined
                obs.OnCompleted();
                return Disposable.Empty;
            }

            // Setup the watchdog
            _ = this.AddUpdateTagItem<ushort>("WatchDog", WatchDogAddress!).SetTagPollIng(false);

            var tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(WatchDogWritingTime)).OnErrorRetry().Subscribe(_ =>
            {
                if (!IsConnectedValue)
                {
                    return;
                }

                Value("WatchDog", WatchDogValueToWrite);
                if (!ShowWatchDogWriting)
                {
                    return;
                }

                _status.OnNext($"{DateTime.Now} - WatchDog writing {WatchDogValueToWrite} to {WatchDogAddress}");
            });

            return new SingleAssignmentDisposable { Disposable = tim };
        }).OnErrorRetry().Publish().RefCount();

    /// <summary>
    /// Takes in input an object and tries to parse it to an array of values. This can be used
    /// to write many data, all of the same type. You must specify the memory area type, memory
    /// are address, byte start address and bytes count. If the read was not successful, check
    /// LastErrorCode or LastErrorString.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="dataType">
    /// Data type of the memory area, can be DB, Timer, Counter, Memory, Input, Output.
    /// </param>
    /// <param name="db">
    /// Address of the memory area (if you want to read DB1, this is set to 1). This must be set
    /// also for other memory area types: counters, timers,etc.
    /// </param>
    /// <param name="startByteAdr">
    /// Start byte address. If you want to read DB1.DBW200, this is 200.
    /// </param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    private bool Write(Tag tag, DataType dataType, int db, int startByteAdr)
    {
        if (tag.NewValue is null)
        {
            return false;
        }

        if (!TrySerializeTagNewValue(tag, out _, out var package))
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            return false;
        }

        return package.Length > 200 && dataType == DataType.DataBlock
            ? WriteMultipleBytes(tag, [.. package], db, startByteAdr)
            : WriteBytes(tag, dataType, db, startByteAdr, package);
    }

    /// <summary>Writes a sequence of bytes to the specified data block of a tag, starting at the given byte address.</summary>
    /// <remarks>The method writes the bytes in chunks, with each chunk containing up to 200 bytes. If an
    /// error occurs during writing, the operation stops and returns false.</remarks>
    /// <param name="tag">The tag to which the bytes will be written.</param>
    /// <param name="bytes">The list of bytes to write to the data block. Cannot be null or empty.</param>
    /// <param name="db">The data block number within the tag where the bytes will be written.</param>
    /// <param name="startByteAdr">The starting byte address within the data block at which to begin writing. Defaults to 0.</param>
    /// <returns>true if all bytes are written successfully; otherwise, false.</returns>
    private bool WriteMultipleBytes(Tag tag, List<byte> bytes, int db, int startByteAdr = 0)
    {
        var errCode = false;
        var index = startByteAdr;
        try
        {
            while (bytes.Count > 0)
            {
                var maxToWrite = Math.Min(bytes.Count, 200);
                var part = new byte[maxToWrite];
                bytes.CopyTo(0, part, 0, maxToWrite);
                errCode = WriteBytes(tag, DataType.DataBlock, db, index, part);
                bytes.RemoveRange(0, maxToWrite);
                index += maxToWrite;
                if (!errCode)
                {
                    break;
                }
            }
        }
        catch (Exception exc)
        {
            _lastErrorCode.OnNext(ErrorCode.WriteData);
            _lastError.OnNext("An error occurred while writing data:" + exc.Message);
        }

        return errCode;
    }

    /// <summary>Attempts to write the value of the specified tag to the appropriate PLC memory area based on its address format.</summary>
    /// <remarks>Supported address formats include data blocks (e.g., DBB, DBW, DBD, DBX, DBS), inputs (E or
    /// I), outputs (A or O), memory (M), timers (T), and counters (C or Z). The method validates address syntax and bit
    /// ranges, and updates error state if parsing fails. If the tag is null or the address is invalid, the method
    /// returns false and sets error information.</remarks>
    /// <param name="tag">The tag containing the address and value to write. The address must be in a supported PLC address format. Cannot
    /// be null.</param>
    /// <returns>true if the value was successfully written to the PLC; otherwise, false.</returns>
    private bool WriteString(Tag? tag)
    {
        if (tag is null)
        {
            return false;
        }

        var tagAddress = tag.Address!.ToUpper();
        tagAddress = tagAddress.Replace(" ", string.Empty); // Remove spaces

        try
        {
            return tagAddress[..2] switch
            {
                "DB" => WriteDataBlockAddress(tag, tagAddress),
                "EB" or "EW" or "ED" => Write(tag, DataType.Input, 0, int.Parse(tagAddress[2..])),
                "AB" or "AW" or "AD" => Write(tag, DataType.Output, 0, int.Parse(tagAddress[2..])),
                "MB" or "MW" or "MD" => Write(tag, DataType.Memory, 0, int.Parse(tagAddress[2..])),
                _ => WriteSpecialOrBitAddress(tag, tagAddress),
            };
        }
        catch (Exception exc)
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext("The variable'" + tag + "' could not be parsed. Please check the syntax and try again.\nException: " + exc.Message);
            return false;
        }
    }

    /// <summary>Writes a data block address.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteDataBlockAddress(Tag tag, string tagAddress)
    {
        var strings = tagAddress.Split(['.']);
        if (strings.Length < 2)
        {
            throw new ArgumentException($"Cannot parse DB address '{tagAddress}'.", nameof(tag));
        }

        var dbNumber = int.Parse(strings[0][2..]);
        var dbType = strings[1][..3];
        var dbIndex = int.Parse(strings[1][3..]);
        return dbType switch
        {
            "DBB" or "DBW" or "DBD" or "DBS" => Write(tag, DataType.DataBlock, dbNumber, dbIndex),
            "DBX" => WriteDataBlockBitAddress(tag, strings, dbNumber, dbIndex),
            _ => throw new ArgumentException($"Addressing Error: Unable to parse address {dbType}. Supported formats include DBB (BYTE), DBW (WORD), DBD (DWORD), DBX (BITWISE), DBS (STRING).", nameof(tag)),
        };
    }

    /// <summary>Writes a data block bit address.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="strings">The parsed address parts.</param>
    /// <param name="dbNumber">The data block number.</param>
    /// <param name="byteOffset">The byte offset.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteDataBlockBitAddress(Tag tag, string[] strings, int dbNumber, int byteOffset)
    {
        var bitOffset = int.Parse(strings[2]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);
        var value = Read<byte>(tag, DataType.DataBlock, dbNumber, byteOffset, VarType.Byte);
        tag.NewValue = RxS7ValueHelpers.SetBit(value, bitOffset, Convert.ToInt32(tag.NewValue, CultureInfo.InvariantCulture) == 1);
        return Write(tag, DataType.DataBlock, dbNumber, byteOffset);
    }

    /// <summary>Writes a timer, counter, or bit address outside data blocks.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteSpecialOrBitAddress(Tag tag, string tagAddress) => tagAddress[..1] switch
    {
        "E" or "I" => WriteBitAddress(tag, tagAddress, DataType.Input),
        "A" or "O" => WriteBitAddress(tag, tagAddress, DataType.Output),
        "M" => WriteBitAddress(tag, tagAddress, DataType.Memory),
        "T" => Write(tag, DataType.Timer, 0, int.Parse(tagAddress[1..])),
        "Z" or "C" => Write(tag, DataType.Counter, 0, int.Parse(tagAddress[1..])),
        _ => throw new ArgumentException($"Unknown variable type {tagAddress[..1]}.", nameof(tag)),
    };

    /// <summary>Writes a bit address from the specified data area.</summary>
    /// <param name="tag">The tag to write.</param>
    /// <param name="tagAddress">The normalized tag address.</param>
    /// <param name="writeDataType">The data area type.</param>
    /// <returns>true if the write succeeds; otherwise, false.</returns>
    private bool WriteBitAddress(Tag tag, string tagAddress, DataType writeDataType)
    {
        var addressLocation = tagAddress[1..];
        var decimalPointIndex = addressLocation.IndexOf('.');
        if (decimalPointIndex == -1)
        {
            throw new ArgumentException($"Cannot parse variable {addressLocation}. Input, Output, Memory Address, Timer, and Counter types require bit-level addressing (e.g. I0.1).", nameof(tag));
        }

        var byteOffset = int.Parse(addressLocation[..decimalPointIndex]);
        var bitOffset = int.Parse(addressLocation[(decimalPointIndex + 1)..]);
        RxS7ValueHelpers.EnsureBitOffsetIsValid(bitOffset, tag);

        var value = Read<byte>(tag, writeDataType, 0, byteOffset, VarType.Byte);
        tag.NewValue = RxS7ValueHelpers.ApplyBitWriteValue(value, tag.NewValue!, bitOffset);
        return Write(tag, writeDataType, 0, byteOffset);
    }
}
