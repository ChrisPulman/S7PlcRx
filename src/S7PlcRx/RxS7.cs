// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using S7PlcRx.Core;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;

namespace S7PlcRx;

/// <summary>
/// Rx S7.
/// </summary>
/// <seealso cref="S7PlcRx.IRxS7" />
public class RxS7 : IRxS7
{
    private readonly S7SocketRx _socketRx;
    private readonly ISubject<Tag?> _dataRead = new Subject<Tag?>();
    private readonly CompositeDisposable _disposables = new();
    private readonly ISubject<string> _lastError = new Subject<string>();
    private readonly ISubject<ErrorCode> _lastErrorCode = new Subject<ErrorCode>();
    private readonly ISubject<PLCRequest> _pLCRequestSubject = new Subject<PLCRequest>();
    private readonly ISubject<string> _status = new Subject<string>();
    private readonly ISubject<long> _readTime = new Subject<long>();
    private readonly SemaphoreSlim _lock = new(1);
    private readonly SemaphoreSlim _lockTagList = new(1);
    private readonly object _socketLock = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly ISubject<bool> _paused = new Subject<bool>();
    private bool _isConnected;
    private bool _pause;

    /// <summary>
    /// Initializes a new instance of the <see cref="RxS7" /> class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="ip">The ip.</param>
    /// <param name="rack">The rack.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="watchDogAddress">The watch dog address.</param>
    /// <param name="interval">The interval to observe on.</param>
    public RxS7(CpuType type, string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100)
    {
        PLCType = type;
        IP = ip;
        Rack = rack;
        Slot = slot;
        WatchDogAddress = watchDogAddress!;

        // Create an observable socket
        _socketRx = new(IP, type, rack, slot);

        IsConnected = _socketRx.IsConnected;

        // Get the PLC connection status
        _disposables.Add(IsConnected.Subscribe(x =>
        {
            _isConnected = x;
            _status.OnNext($"{DateTime.Now} - PLC Connected Status: {x}");
        }));

        if (!string.IsNullOrWhiteSpace(watchDogAddress))
        {
            _disposables.Add(WatchDogObservable().Subscribe());
        }

        _disposables.Add(TagReaderObservable(interval).Subscribe());

        _disposables.Add(_pLCRequestSubject.Subscribe(request =>
        {
            if (request.Request == PLCRequestType.Write)
            {
                WriteString(request.Tag);
            }

            GetTagValue(request.Tag);
            _dataRead.OnNext(request.Tag);
        }));
    }

    /// <summary>
    /// Gets the data read.
    /// </summary>
    /// <value>The data read.</value>
    public IObservable<Tag?> ObserveAll =>
        _dataRead
            .AsObservable()
            .Publish()
            .RefCount();

    /// <summary>
    /// Gets a value indicating whether this instance is paused.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is paused; otherwise, <c>false</c>.
    /// </value>
    public IObservable<bool> IsPaused => _paused.DistinctUntilChanged().Publish().RefCount();

    /// <summary>
    /// Gets the ip address.
    /// </summary>
    /// <value>
    /// The ip address.
    /// </value>
    public string IP { get; }

    /// <summary>
    /// Gets the is connected.
    /// </summary>
    /// <value>
    /// The is connected.
    /// </value>
    public IObservable<bool> IsConnected { get; }

    /// <summary>
    /// Gets the last error.
    /// </summary>
    /// <value>The last error.</value>
    public IObservable<string> LastError => _lastError.Publish().RefCount();

    /// <summary>
    /// Gets the last error code registered when executing a function.
    /// </summary>
    /// <value>
    /// The last error code.
    /// </value>
    public IObservable<ErrorCode> LastErrorCode => _lastErrorCode.Publish().RefCount();

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
    /// Gets or sets a value indicating whether [show watch dog writing].
    /// </summary>
    /// <value><c>true</c> if [show watch dog writing]; otherwise, <c>false</c>.</value>
    public bool ShowWatchDogWriting { get; set; }

    /// <summary>
    /// Gets the slot.
    /// </summary>
    /// <value>The slot.</value>
    public short Slot { get; }

    /// <summary>
    /// Gets the status.
    /// </summary>
    /// <value>
    /// The status.
    /// </value>
    public IObservable<string> Status => _status.Publish().RefCount();

    /// <summary>
    /// Gets the tag list. A) Name, B) Address, C) Value.
    /// </summary>
    /// <value>The tag list.</value>
    public Tags TagList { get; } = new();

    /// <summary>
    /// Gets the watch dog address.
    /// </summary>
    /// <value>The watch dog address.</value>
    public string WatchDogAddress { get; }

    /// <summary>
    /// Gets or sets the watch dog value to write.
    /// </summary>
    /// <value>The watch dog value to write.</value>
    public ushort WatchDogValueToWrite { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the watch dog writing time. (Seconds).
    /// </summary>
    /// <value>The watch dog writing time. (Seconds).</value>
    public int WatchDogWritingTime { get; set; } = 10;

    /// <summary>
    /// Gets a value indicating whether gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the read time.
    /// </summary>
    /// <value>
    /// The read time.
    /// </value>
    public IObservable<long> ReadTime => _readTime.Publish().RefCount();

    /// <summary>
    /// Observes the specified variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <returns>An Observable of T.</returns>
    public IObservable<T?> Observe<T>(string? variable) =>
        ObserveAll
            .Where(t => TagValueIsValid<T>(t, variable))
            .Select(t => (T?)t?.Value)
            .Retry()
            .Publish()
            .RefCount();

    /// <summary>
    /// Reads the specified variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable to read.</param>
    /// <returns>A value of T.</returns>
    public async Task<T?> Value<T>(string? variable)
    {
        _pause = true;
        _ = await _paused.Where(x => x).FirstAsync();
        var tag = TagList[variable!];
        GetTagValue(tag);
        _pause = false;
        return TagValueIsValid<T>(tag) ? (T?)tag?.Value : default;
    }

    /// <summary>
    /// Writes the specified value to the PLC Tag.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value to write.</param>
    public void Value<T>(string? variable, T? value)
    {
        var tag = TagList[variable!];
        if (tag != null && value != null && tag.Type == typeof(T))
        {
            tag.NewValue = value;
            Write(tag);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes a C# class to a DB in the PLC.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="classValue">The class to be written.</param>
    /// <param name="db">Db address.</param>
    /// <param name="startByteAdr">Start bytes on the PLC.</param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    internal bool WriteClass(Tag tag, object classValue, int db, int startByteAdr = 0)
    {
        if (classValue == null)
        {
            return false;
        }

        var bytes = Class.ToBytes(classValue).ToList();
        return WriteMultipleBytes(tag, bytes, db, startByteAdr);
    }

    /// <summary>
    /// Adds the update tag item.
    /// </summary>
    /// <param name="tag">The tag.</param>
    internal void AddUpdateTagItem(Tag tag)
    {
        if (string.IsNullOrWhiteSpace(tag?.Address))
        {
            throw new TagAddressOutOfRangeException(tag);
        }

        _lockTagList.Wait();
        if (TagList[tag!.Name!] is Tag tagExists)
        {
            tagExists.Name = tag.Name;
            tagExists.Value = tag.Value;
            tagExists.Address = tag.Address;
            tagExists.Type = tag.Type;
            tagExists.ArrayLength = tag.ArrayLength;
        }
        else
        {
            TagList.Add(tag);
        }

        _lockTagList.Release();
    }

    internal void RemoveTagItem(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentNullException(nameof(tagName));
        }

        _lockTagList.Wait();
        if (TagList.ContainsKey(tagName!))
        {
            TagList.Remove(tagName!);
        }

        _lockTagList.Release();
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                _disposables.Dispose();
                try
                {
                    _lock.Wait();
                    _lock.Dispose();
                }
                catch
                {//// ignored
                }

                try
                {
                    _lockTagList.Wait();
                    _lockTagList.Dispose();
                }
                catch
                {//// ignored
                }

                _socketRx?.Dispose();
            }

            IsDisposed = true;
        }
    }

    private static bool TagValueIsValid<T>(Tag? tag) => tag != null && tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T);

    private static bool TagValueIsValid<T>(Tag? tag, string? variable) => tag?.Name == variable && tag?.Type == typeof(T) && tag.Value?.GetType() == typeof(T);

    private static ByteArray CreateReadDataRequestPackage(DataType dataType, int db, int startByteAdr, int count = 1)
    {
        // single data register = 12
        var package = new ByteArray(12);
        package.Add(new byte[] { 18, 10, 16 });
        switch (dataType)
        {
            case DataType.Timer:
            case DataType.Counter:
                package.Add((byte)dataType);
                break;

            default:
                package.Add(2);
                break;
        }

        package.Add(Word.ToByteArray((ushort)count));
        package.Add(Word.ToByteArray((ushort)db));
        package.Add((byte)dataType);
        var overflow = (int)(startByteAdr * 8 / 65535U); // handles words with address bigger than 8191
        package.Add((byte)overflow);
        switch (dataType)
        {
            case DataType.Timer:
            case DataType.Counter:
                package.Add(Word.ToByteArray((ushort)startByteAdr));
                break;

            default:
                package.Add(Word.ToByteArray((ushort)(startByteAdr * 8)));
                break;
        }

        return package;
    }

    /// <summary>
    /// Gets the Tag address as a string.
    /// </summary>
    /// <param name="dataType">Type of the data.</param>
    /// <param name="startAddress">The start address.</param>
    /// <param name="type">The type.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The Tag as a string.</returns>
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable RCS1213 // Remove unused member declaration.
    private static string GetTagAddress(DataType dataType, int startAddress, VarType type, int offset = 1)
#pragma warning restore RCS1213 // Remove unused member declaration.
#pragma warning restore IDE0051 // Remove unused private members
    {
        var description = dataType switch
        {
            DataType.Input => "I",
            DataType.Output => "O",
            DataType.Memory => "M",
            DataType.DataBlock => "DB",
            DataType.Timer => "T",
            DataType.Counter => "C",
            _ => string.Empty,
        };

        description += type switch
        {
            VarType.Bit => dataType == DataType.DataBlock ? $"X{startAddress}.{offset}" : $"{startAddress}.{offset}",
            VarType.Byte => dataType == DataType.Input || dataType == DataType.Output ? $"{startAddress}" : $"B{startAddress}",
            VarType.Word => $"W{startAddress}",
            VarType.DWord => $"D{startAddress}",
            VarType.Int => $"W{startAddress}",
            VarType.DInt or VarType.Real => $"D{startAddress}",
            VarType.String => $"X{startAddress}-{offset}",
            _ => $"{startAddress}",
        };
        return description;
    }

    private static ByteArray ReadHeaderPackage(int amount = 1)
    {
        // header size = 19 bytes
        var package = new ByteArray(19);
        package.Add(new byte[] { 3, 0, 0 });

        // complete package size
        package.Add((byte)(19 + (12 * amount)));
        package.Add(new byte[] { 2, 240, 128, 50, 1, 0, 0, 0, 0 });

        // data part size
        package.Add(Word.ToByteArray((ushort)(2 + (amount * 12))));
        package.Add(new byte[] { 0, 0, 4 });

        // amount of requests
        package.Add((byte)amount);

        return package;
    }

    private static int VarTypeToByteLength(VarType varType, int varCount = 1) => varType switch
    {
        VarType.Bit => varCount, // TODO
        VarType.Byte => (varCount < 1) ? 1 : varCount,
        VarType.String => varCount,
        VarType.Word or VarType.Timer or VarType.Int or VarType.Counter => varCount * 2,
        VarType.DWord or VarType.DInt or VarType.Real => varCount * 4,
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
        if (string.IsNullOrWhiteSpace(tag.Address))
        {
            throw new ArgumentNullException(nameof(tag.Address));
        }

        if (tag.NewValue == null)
        {
            throw new ArgumentNullException(nameof(tag.NewValue));
        }

        _pLCRequestSubject.OnNext(new(PLCRequestType.Write, tag));
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
            var bReceive = new byte[1024];
            try
            {
                var varCount = value.Length;

                // first create the header
                var packageSize = 35 + value.Length;
                var package = new ByteArray(packageSize);

                package.Add(new byte[] { 3, 0, 0 });
                package.Add((byte)packageSize);
                package.Add(new byte[] { 2, 240, 128, 50, 1, 0, 0 });
                package.Add(Word.ToByteArray((ushort)(varCount - 1)));
                package.Add(new byte[] { 0, 14 });
                package.Add(Word.ToByteArray((ushort)(varCount + 4)));
                package.Add(new byte[] { 5, 1, 18, 10, 16, 2 });
                package.Add(Word.ToByteArray((ushort)varCount));
                package.Add(Word.ToByteArray((ushort)db));
                package.Add((byte)dataType);
                var overflow = (int)(startByteAdr * 8 / 0xffffU); // handles words with address bigger than 8191
                package.Add((byte)overflow);
                package.Add(Word.ToByteArray((ushort)(startByteAdr * 8)));
                package.Add(new byte[] { 0, 4 });
                package.Add(Word.ToByteArray((ushort)(varCount * 8)));

                // now join the header and the data
                package.Add(value);

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return false;
                }

                var result = _socketRx.Receive(tag, bReceive, 1024);

                if (bReceive[21] != 0xff)
                {
                    _lastErrorCode.OnNext(ErrorCode.WriteData);
                    _lastError.OnNext($"Tag {tag.Name} failed to write - {nameof(ErrorCode.WrongNumberReceivedBytes)} code {bReceive[21]}");
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

    private void GetTagValue(Tag? tag)
    {
        var result = Read(tag);
        if (tag == null || result == null || result is ErrorCode)
        {
            return;
        }

        tag.Value = result;
    }

    private object? ParseBytes(VarType varType, byte[] bytes, int varCount)
    {
        try
        {
            if (bytes == null)
            {
                return default;
            }

            switch (varType)
            {
                case VarType.Byte:
                    if (varCount == 1)
                    {
                        return bytes[0];
                    }

                    return bytes;

                case VarType.Word:
                    if (varCount == 1)
                    {
                        return Word.FromByteArray(bytes);
                    }

                    return Word.ToArray(bytes);

                case VarType.Int:
                    if (varCount == 1)
                    {
                        return Int.FromByteArray(bytes);
                    }

                    return Int.ToArray(bytes);

                case VarType.DWord:
                    if (varCount == 1)
                    {
                        return DWord.FromByteArray(bytes);
                    }

                    return DWord.ToArray(bytes);

                case VarType.DInt:
                    if (varCount == 1)
                    {
                        return DInt.FromByteArray(bytes);
                    }

                    return DInt.ToArray(bytes);

                case VarType.Real:
                    if (varCount == 1)
                    {
                        return Real.FromByteArray(bytes);
                    }

                    return Real.ToArray(bytes);

                case VarType.String:
                    return PlcTypes.String.FromByteArray(bytes);

                case VarType.Timer:
                    if (varCount == 1)
                    {
                        return PlcTypes.Timer.FromByteArray(bytes);
                    }

                    return PlcTypes.Timer.ToArray(bytes);

                case VarType.Counter:
                    if (varCount == 1)
                    {
                        return Counter.FromByteArray(bytes);
                    }

                    return Counter.ToArray(bytes);

                case VarType.Bit:

                    // TODO: fix Bit
                    return default;

                default:
                    return default;
            }
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
            _lock.Release();
        }

        return default;
    }

    /// <summary>
    /// Reads the specified tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>An object.</returns>
    private object? Read(Tag? tag)
    {
        if (string.IsNullOrWhiteSpace(tag?.Address))
        {
            throw new ArgumentNullException(nameof(tag.Address));
        }

        DataType dataType;
        int dB;
        int mByte;
        int mBit;

        BitArray objBoolArray;

        // remove spaces
        var correctVariable = tag!.Address!.ToUpper().Replace(" ", string.Empty);

        try
        {
            switch (correctVariable!.Substring(0, 2))
            {
                case "DB":
                    var strings = correctVariable.Split(new char[] { '.' });
                    if (strings.Length < 2)
                    {
                        throw new Exception();
                    }

                    dB = int.Parse(strings[0].Substring(2));
                    var dbType = strings[1].Substring(0, 3);
                    var dbIndex = int.Parse(strings[1].Substring(3));

                    switch (dbType)
                    {
                        case "DBB":
                            if (tag.Type == typeof(byte[]))
                            {
                                return Read<byte[]>(tag, DataType.DataBlock, dB, dbIndex, VarType.Byte);
                            }

                            // TODO: fix string
                            ////if (tag.Type == typeof(string))
                            ////{
                            ////    return Read<string[]>(tag, DataType.DataBlock, dB, dbIndex, VarType.String);
                            ////}

                            return Read<byte>(tag, DataType.DataBlock, dB, dbIndex, VarType.Byte);

                        case "DBW":
                            if (tag.Type == typeof(ushort[]))
                            {
                                return Read<ushort[]>(tag, DataType.DataBlock, dB, dbIndex, VarType.Word);
                            }

                            return Read<ushort>(tag, DataType.DataBlock, dB, dbIndex, VarType.Word);

                        case "DBD":
                            if (tag.Type == typeof(double))
                            {
                                return Read<double>(tag, DataType.DataBlock, dB, dbIndex, VarType.Real);
                            }

                            if (tag.Type == typeof(double[]))
                            {
                                return Read<double[]>(tag, DataType.DataBlock, dB, dbIndex, VarType.Real);
                            }

                            if (tag.Type == typeof(uint[]))
                            {
                                return Read<uint>(tag, DataType.DataBlock, dB, dbIndex, VarType.DWord);
                            }

                            return Read<uint>(tag, DataType.DataBlock, dB, dbIndex, VarType.DWord);

                        case "DBX":
                            mByte = dbIndex;
                            mBit = int.Parse(strings[2]);
                            if (mBit > 7)
                            {
                                throw new Exception();
                            }

                            var obj2 = Read<byte>(tag, DataType.DataBlock, dB, mByte, VarType.Byte);
                            objBoolArray = new BitArray(new byte[] { obj2 });
                            return objBoolArray[mBit];

                        default:
                            throw new Exception();
                    }

                case "EB":

                    // Input byte
                    if (tag.Type == typeof(byte[]))
                    {
                        return Read<byte[]>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);
                    }

                    return Read<byte>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);

                case "EW":

                    // Input word
                    if (tag.Type == typeof(ushort[]))
                    {
                        return Read<ushort[]>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);
                    }

                    return Read<ushort>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);

                case "ED":

                    // Input double-word
                    if (tag.Type == typeof(uint[]))
                    {
                        return Read<uint[]>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord);
                    }

                    return Read<uint>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord);

                case "AB":

                    // Output byte
                    if (tag.Type == typeof(byte[]))
                    {
                        return Read<byte[]>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);
                    }

                    return Read<byte>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);

                case "AW":

                    // Output word
                    if (tag.Type == typeof(ushort[]))
                    {
                        return Read<ushort[]>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);
                    }

                    return Read<ushort>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);

                case "AD":

                    // Output double-word
                    if (tag.Type == typeof(uint[]))
                    {
                        return Read<uint[]>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord);
                    }

                    return Read<uint>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord);

                case "MB":

                    // Memory byte
                    if (tag.Type == typeof(byte[]))
                    {
                        return Read<byte[]>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);
                    }

                    return Read<byte>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte);

                case "MW":

                    // Memory word
                    if (tag.Type == typeof(ushort[]))
                    {
                        return Read<ushort[]>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);
                    }

                    return Read<ushort>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Word);

                case "MD":

                    // Memory double-word
                    if (tag.Type == typeof(double[]))
                    {
                        return Read<double[]>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Real);
                    }

                    return Read<double>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Real);

                default:
                    switch (correctVariable.Substring(0, 1))
                    {
                        case "E":
                        case "I":

                            // Input
                            dataType = DataType.Input;
                            break;

                        case "A":
                        case "O":

                            // Output
                            dataType = DataType.Output;
                            break;

                        case "M":

                            // Memory
                            dataType = DataType.Memory;
                            break;

                        case "T":

                            // Timer
                            if (tag.Type == typeof(double[]))
                            {
                                return Read<double[]>(tag, DataType.Timer, 0, int.Parse(correctVariable.Substring(2)), VarType.Timer);
                            }

                            return Read<double>(tag, DataType.Timer, 0, int.Parse(correctVariable.Substring(1)), VarType.Timer);

                        case "Z":
                        case "C":

                            // Counter
                            if (tag.Type == typeof(ushort[]))
                            {
                                return Read<ushort[]>(tag, DataType.Counter, 0, int.Parse(correctVariable.Substring(2)), VarType.Counter);
                            }

                            return Read<ushort>(tag, DataType.Counter, 0, int.Parse(correctVariable.Substring(1)), VarType.Counter);

                        default:
                            throw new Exception();
                    }

                    var txt2 = correctVariable.Substring(1);
                    if (!txt2.Contains('.'))
                    {
                        throw new Exception();
                    }

                    mByte = int.Parse(txt2.Substring(0, txt2.IndexOf(".")));
                    mBit = int.Parse(txt2.Substring(txt2.IndexOf(".") + 1));
                    if (mBit > 7)
                    {
                        throw new Exception();
                    }

                    var obj3 = Read<byte>(tag, dataType, 0, mByte, VarType.Byte);
                    objBoolArray = new BitArray(new byte[] { obj3 });
                    return objBoolArray[mBit];
            }
        }
        catch
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext("The variable'" + tag.Address + "' could not be read. Please check the syntax and try again.");
            return false;
        }
    }

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

                var bReceive = new byte[1024];
                var result = _socketRx.Receive(tag, bReceive, 1024);
                if (bReceive[21] != 0xff)
                {
                    if (bReceive[21] != 0)
                    {
                        _lastErrorCode.OnNext(ErrorCode.ReadData);
                        _lastError.OnNext($"Tag {tag.Name} failed to read - {nameof(ErrorCode.WrongNumberReceivedBytes)} code {bReceive[21]}");
                    }

                    return default;
                }

                Array.Copy(bReceive, 25, bytes, 0, count);

                return bytes;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return default;
            }
        }
    }

    private byte[] ReadMultipleBytes(Tag tag, DataType dataType, int db, int startByteAdr, int numBytes)
    {
        var resultBytes = new List<byte>();
        var index = startByteAdr;
        while (numBytes > 0)
        {
            // Allow 32 bytes for the header
            var maxToRead = Math.Min(numBytes, _socketRx.DataReadLength - 32);
            var bytes = default(byte[]);
            for (var i = 0; i < 3; i++)
            {
                bytes = ReadBytes(tag, dataType, db, index, maxToRead);
                if (bytes != null)
                {
                    break;
                }
            }

            if (bytes == null)
            {
                return Array.Empty<byte>();
            }

            resultBytes.AddRange(bytes);
            numBytes -= maxToRead;
            index += maxToRead;
        }

        return resultBytes.ToArray();
    }

    private IObservable<Unit> TagReaderObservable(double interval) =>
        Observable.Create<Unit>(__ =>
            {
                var tim = Observable.Interval(TimeSpan.FromMilliseconds(interval))
                    .Subscribe(async _ =>
                    {
                        if (_isConnected)
                        {
                            var tagList = TagList.ToList().Where(t => !t.DoNotPoll);
                            if (!tagList.Any() || _pause)
                            {
                                _paused.OnNext(true);
                                return;
                            }

                            await _lockTagList.WaitAsync();
                            _stopwatch.Restart();
                            _paused.OnNext(false);
                            foreach (var tag in tagList)
                            {
                                if (tag.DoNotPoll)
                                {
                                    continue;
                                }

                                try
                                {
                                    while (!_isConnected)
                                    {
                                        await Task.Delay(10);
                                    }

                                    _pLCRequestSubject.OnNext(new PLCRequest(PLCRequestType.Read, tag));
                                }
                                catch (Exception ex)
                                {
                                    _lastError.OnNext(ex.Message);
                                    _status.OnNext($"{tag.Name} could not be read from {tag.Address}. Error: " + ex.ToString());
                                }
                            }

                            _stopwatch.Stop();
                            _readTime.OnNext(_stopwatch.ElapsedTicks);
                            _lockTagList.Release();
                        }
                    });

                return new SingleAssignmentDisposable { Disposable = tim };
            }).Retry().Publish().RefCount();

    private IObservable<Unit> WatchDogObservable() =>
        Observable.Create<Unit>(obs =>
        {
            if (string.IsNullOrWhiteSpace(WatchDogAddress))
            {
                // disable watchdog if not defined
                obs.OnCompleted();
                return Task.CompletedTask;
            }

            // Setup the watchdog
            var wd = new Tag("WatchDog", WatchDogAddress, WatchDogValueToWrite, typeof(ushort));

            AddUpdateTagItem(wd);
            var tim = Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(WatchDogWritingTime)).Retry().Subscribe(_ =>
            {
                if (_isConnected)
                {
                    wd.Value = WatchDogValueToWrite;
                    _pLCRequestSubject.OnNext(new PLCRequest(PLCRequestType.Write, wd));
                    if (ShowWatchDogWriting)
                    {
                        _status.OnNext($"{DateTime.Now} - Watch Dog writing {wd.Value} to {wd.Address}");
                    }
                }
            });

            return new SingleAssignmentDisposable { Disposable = tim };
        }).Retry().Publish().RefCount();

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
    /// <param name="value">
    /// Bytes to write. The length of this parameter can't be higher than 200. If you need more,
    /// use recursion.
    /// </param>
    /// <returns>NoError if it was successful, or the error is specified.</returns>
    private bool Write(Tag tag, DataType dataType, int db, int startByteAdr, object? value = null)
    {
        if (tag.NewValue == null && value == null)
        {
            return false;
        }

        byte[] package;
        switch (tag.Type.Name)
        {
            case "Byte":
                package = new byte[] { (byte)(value ?? Convert.ChangeType(tag.NewValue, typeof(byte)))! };
                break;

            case "Int16":
                package = Int.ToByteArray((short)tag.NewValue!);
                break;

            case "UInt16":
                if (value == null)
                {
                    return false;
                }

                var parsed = ushort.Parse(value.ToString()!);
                var vOut = BitConverter.GetBytes(parsed);
                package = new byte[] { vOut[1], vOut[0] };
                break;

            case "ushort":
                package = Word.ToByteArray((ushort)Convert.ChangeType(tag.NewValue, typeof(ushort))!);
                break;

            case "Int32":
                package = DInt.ToByteArray((int)tag.NewValue!);
                break;

            case "uint":
                package = DWord.ToByteArray((uint)Convert.ChangeType(tag.NewValue, typeof(uint))!);
                break;

            case "Double":
                package = Real.ToByteArray((double)tag.NewValue!);
                break;

            case "Byte[]":
                if (value == null)
                {
                    return false;
                }

                package = (byte[])value;
                break;

            case "Int16[]":
                package = Int.ToByteArray((short[])tag.NewValue!);
                break;

            case "ushort[]":
                if (value == null)
                {
                    return false;
                }

                package = Word.ToByteArray((ushort[])value);
                break;

            case "Int32[]":
                package = DInt.ToByteArray((int[])tag.NewValue!);
                break;

            case "uint[]":
                package = DWord.ToByteArray((uint[])Convert.ChangeType(tag.NewValue, typeof(uint[]))!);
                break;

            case "Double[]":
                package = Real.ToByteArray((double[])tag.NewValue!);
                break;

            case "String":
                if (value == null)
                {
                    return false;
                }

                package = PlcTypes.String.ToByteArray(tag.NewValue! as string);
                break;

            default:
                _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
                return false;
        }

        return WriteBytes(tag, dataType, db, startByteAdr, package);
    }

    private bool WriteMultipleBytes(Tag tag, List<byte> bytes, int db, int startByteAdr = 0)
    {
        var errCode = false;
        var index = startByteAdr;
        try
        {
            while (bytes.Count > 0)
            {
                var maxToWrite = Math.Min(bytes.Count, 200);
                var part = bytes.ToList().GetRange(0, maxToWrite);
                errCode = WriteBytes(tag, DataType.DataBlock, db, index, part.ToArray());
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

    private bool WriteString(Tag? tag)
    {
        if (tag == null)
        {
            return false;
        }

        DataType mDataType;
        int mDB;
        int mByte;
        int mBit;

        string addressLocation;
        byte @byte;

        var tagAddress = tag.Address!.ToUpper();
        tagAddress = tagAddress.Replace(" ", string.Empty); // Remove spaces

        try
        {
            switch (tagAddress.Substring(0, 2))
            {
                case "DB":
                    var strings = tagAddress.Split(new char[] { '.' });
                    if (strings.Length < 2)
                    {
                        throw new Exception();
                    }

                    mDB = int.Parse(strings[0].Substring(2));
                    var dbType = strings[1].Substring(0, 3);
                    var dbIndex = int.Parse(strings[1].Substring(3));

                    switch (dbType)
                    {
                        case "DBB":
                        case "DBW":
                        case "DBD":
                        case "DBS":
                            return Write(tag, DataType.DataBlock, mDB, dbIndex);

                        case "DBX":
                            mByte = dbIndex;
                            mBit = int.Parse(strings[2]);
                            if (mBit > 7)
                            {
                                throw new Exception(string.Format("Addressing Error: You can only reference bitwise locations 0-7. Address {0} is invalid", mBit));
                            }

                            var b = Read<byte>(tag, DataType.DataBlock, mDB, mByte, VarType.Byte);
                            if (Convert.ToInt32(tag.NewValue) == 1)
                            {
                                b = (byte)(b | (byte)Math.Pow(2, mBit)); // set bit
                            }
                            else
                            {
                                b = (byte)(b & (b ^ (byte)Math.Pow(2, mBit))); // reset bit
                            }

                            return Write(tag, DataType.DataBlock, mDB, mByte, b);

                        default:
                            throw new Exception(string.Format("Addressing Error: Unable to parse address {0}. Supported formats include DBB (BYTE), DBW (WORD), DBD (DWORD), DBX (BITWISE), DBS (STRING).", dbType));
                    }

                case "EB":
                case "EW":
                case "ED":
                    return Write(tag, DataType.Input, 0, int.Parse(tagAddress.Substring(2)));

                case "AB":
                case "AW":
                case "AD":
                    return Write(tag, DataType.Output, 0, int.Parse(tagAddress.Substring(2)));

                case "MB":
                case "MW":
                case "MD":
                    return Write(tag, DataType.Memory, 0, int.Parse(tagAddress.Substring(2)));

                default:
                    switch (tagAddress.Substring(0, 1))
                    {
                        case "E":
                        case "I":

                            // Input
                            mDataType = DataType.Input;
                            break;

                        case "A":
                        case "O":

                            // Output
                            mDataType = DataType.Output;
                            break;

                        case "M":

                            // Memory
                            mDataType = DataType.Memory;
                            break;

                        case "T":

                            // Timer
                            return Write(tag, DataType.Timer, 0, int.Parse(tagAddress.Substring(1)));

                        case "Z":
                        case "C":

                            // Counter
                            return Write(tag, DataType.Counter, 0, int.Parse(tagAddress.Substring(1)));

                        default:
                            throw new Exception(string.Format("Unknown variable type {0}.", tagAddress.Substring(0, 1)));
                    }

                    addressLocation = tagAddress.Substring(1);
                    var decimalPointIndex = addressLocation.IndexOf(".");
                    if (decimalPointIndex == -1)
                    {
                        throw new Exception(string.Format("Cannot parse variable {0}. Input, Output, Memory Address, Timer, and Counter types require bit-level addressing (e.g. I0.1).", addressLocation));
                    }

                    mByte = int.Parse(addressLocation.Substring(0, decimalPointIndex));
                    mBit = int.Parse(addressLocation.Substring(decimalPointIndex + 1));
                    if (mBit > 7)
                    {
                        throw new Exception(string.Format("Addressing Error: You can only reference bitwise locations 0-7. Address {0} is invalid", mBit));
                    }

                    @byte = Read<byte>(tag, mDataType, 0, mByte, VarType.Byte);

                    var parsedBool = false;

                    if (bool.TryParse(tag.NewValue!.ToString(), out parsedBool))
                    {
                        if (parsedBool)
                        {
                            @byte = (byte)(@byte | (byte)Math.Pow(2, mBit));      // Set bit
                        }
                        else
                        {
                            @byte = (byte)(@byte & (@byte ^ (byte)Math.Pow(2, mBit))); // Reset bit
                        }
                    }

                    var parsedInt = -1;

                    if (int.TryParse(tag.NewValue.ToString(), out parsedInt))
                    {
                        if (parsedInt == 1)
                        {
                            @byte = (byte)(@byte | (byte)Math.Pow(2, mBit)); // Set bit
                        }
                        else
                        {
                            @byte = (byte)(@byte & (@byte ^ (byte)Math.Pow(2, mBit))); // Reset bit
                        }
                    }

                    return Write(tag, mDataType, 0, mByte, @byte);
            }
        }
        catch (Exception exc)
        {
            _lastErrorCode.OnNext(ErrorCode.WrongVarFormat);
            _lastError.OnNext("The variable'" + tag + "' could not be parsed. Please check the syntax and try again.\nException: " + exc.Message);
            return false;
        }
    }
}
