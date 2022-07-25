// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using S7PlcRx.Core;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;

namespace S7PlcRx
{
    /// <summary>
    /// Rx S7.
    /// </summary>
    public class RxS7 : IRxS7
    {
        private readonly S7SocketRx _socketRx;
        private readonly ISubject<Tag> _dataRead = new Subject<Tag>();
        private readonly CompositeDisposable _disposables = new();
        private readonly ISubject<string> _lastError = new Subject<string>();
        private readonly ISubject<ErrorCode> _lastErrorCode = new Subject<ErrorCode>();
        private readonly ISubject<PLCRequest> _pLCRequestSubject = new Subject<PLCRequest>();
        private readonly ISubject<bool> _pLCstatus = new Subject<bool>();
        private readonly ISubject<Unit> _restartReadCycle = new Subject<Unit>();
        private readonly ISubject<string> _status = new Subject<string>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private bool _isConnected;

        /// <summary>
        /// Initializes a new instance of the <see cref="RxS7" /> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="ip">The ip.</param>
        /// <param name="rack">The rack.</param>
        /// <param name="slot">The slot.</param>
        /// <param name="watchDogAddress">The watch dog address.</param>
        public RxS7(CpuType type, string ip, short rack, short slot, string? watchDogAddress = null)
        {
            PLCType = type;
            IP = ip;
            Rack = rack;
            Slot = slot;
            WatchDogAddress = watchDogAddress!;

            // Create an observable socket
            _socketRx = new(IP, type, rack, slot);

            IsConnected = _socketRx.IsConnected.Publish().RefCount();

            // Get the PLC connection status
            _disposables.Add(IsConnected.Subscribe(x =>
            {
                if (x)
                {
                    _restartReadCycle.OnNext(default);
                }

                _isConnected = x;
                _pLCstatus.OnNext(x);
            }));

            if (!string.IsNullOrWhiteSpace(watchDogAddress))
            {
                _disposables.Add(WatchDogObservable().Subscribe());
            }

            _disposables.Add(TagReaderObservable().Subscribe());

            _disposables.Add(_pLCRequestSubject.Subscribe(request =>
            {
                switch (request.Request)
                {
                    case PLCRequestType.Read:
                        GetTagValue(request.Tag);
                        _dataRead.OnNext(request.Tag);
                        break;

                    case PLCRequestType.Write:
                        WriteString(request.Tag);
                        GetTagValue(request.Tag);
                        _dataRead.OnNext(request.Tag);
                        break;

                    case PLCRequestType.Restart:

                        // Finished Reading list - restart
                        _dataRead.OnNext(request.Tag);
                        _restartReadCycle.OnNext(default);
                        break;
                }
            }));
        }

        /// <summary>
        /// Gets the data read.
        /// </summary>
        /// <value>The data read.</value>
        public IObservable<Tag> ObserveAll => _dataRead.AsObservable();

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
        public IObservable<string> LastError => _lastError.AsObservable();

        /// <summary>
        /// Gets the last error code registered when executing a function.
        /// </summary>
        /// <value>
        /// The last error code.
        /// </value>
        public IObservable<ErrorCode> LastErrorCode => _lastErrorCode.AsObservable();

        /// <summary>
        /// Gets the PLC status.
        /// </summary>
        /// <value>
        /// The PLC status.
        /// </value>
        public IObservable<bool> PLCStatus => _pLCstatus.AsObservable();

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
        public IObservable<string> Status => _status.AsObservable();

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
        /// Gets or sets the watch dog writing time. (Sec).
        /// </summary>
        /// <value>The watch dog writing time. (Sec).</value>
        public int WatchDogWritingTime { get; set; } = 10;

        /// <summary>
        /// Gets a value indicating whether gets a value that indicates whether the object is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Observes the specified variable.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="variable">The variable.</param>
        /// <returns>An Observable of T.</returns>
        public IObservable<T?> Observe<T>(string? variable) =>
            ObserveAll.Where(t => t.Name == variable && t.Value.GetType() == typeof(T)).Select(t => (T?)t.Value).Retry();

        /// <summary>
        /// Values the specified variable.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="variable">The variable.</param>
        /// <returns>A value of T.</returns>
        public T? Value<T>(string? variable) =>
            (T?)TagList[variable!]?.Value;

        /// <summary>
        /// Values the specified variable.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
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
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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
        internal void AddUpdateTagItem(Tag tag!!)
        {
            if (string.IsNullOrWhiteSpace(tag.Address))
            {
                throw new TagAddressOutOfRangeException(tag);
            }

            _lock.Wait();
            if (TagList[tag.Name!] is Tag tagExists)
            {
                tagExists.Name = tag.Name;
                tagExists.Value = tag.Value;
                tagExists.Address = tag.Address;
                tagExists.Type = tag.Type;
            }
            else
            {
                TagList.Add(tag);
            }

            _lock.Release();
        }

        internal void RemoveTagItem(string tagName!!)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentNullException(nameof(tagName));
            }

            _lock.Wait();
            if (TagList.ContainsKey(tagName!))
            {
                TagList.Remove(tagName!);
            }

            _lock.Release();
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
                    _lock.Dispose();
                    _disposables.Dispose();
                    _socketRx?.Dispose();
                }

                IsDisposed = true;
            }
        }

        private static ByteArray CreateReadDataRequestPackage(DataType dataType, int db, int startByteAdr, int count = 1)
        {
            // single data register = 12
            var package = new ByteArray(12);
            package.Add(new byte[] { 0x12, 0x0a, 0x10 });
            switch (dataType)
            {
                case DataType.Timer:
                case DataType.Counter:
                    package.Add((byte)dataType);
                    break;

                default:
                    package.Add(0x02);
                    break;
            }

            package.Add(Word.ToByteArray((ushort)count));
            package.Add(Word.ToByteArray((ushort)db));
            package.Add((byte)dataType);
            var overflow = (int)(startByteAdr * 8 / 0xffffU); // handles words with address bigger than 8191
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
        private static string GetTagAddress(DataType dataType, int startAddress, VarType type, int offset = 1)
        {
            var description = string.Empty;
            switch (dataType)
            {
                case DataType.Input:
                    description = "I";
                    break;

                case DataType.Output:
                    description = "O";
                    break;

                case DataType.Memory:
                    description = "M";
                    break;

                case DataType.DataBlock:
                    description = "DB";
                    break;

                case DataType.Timer:
                    description = "T";
                    break;

                case DataType.Counter:
                    description = "C";
                    break;
            }

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
            package.Add(new byte[] { 0x03, 0x00, 0x00 });

            // complete package size
            package.Add((byte)(19 + (12 * amount)));
            package.Add(new byte[] { 0x02, 0xf0, 0x80, 0x32, 0x01, 0x00, 0x00, 0x00, 0x00 });

            // data part size
            package.Add(Word.ToByteArray((ushort)(2 + (amount * 12))));
            package.Add(new byte[] { 0x00, 0x00, 0x04 });

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
            var bReceive = new byte[513];
            try
            {
                var varCount = value.Length;

                // first create the header
                var packageSize = 35 + value.Length;
                var package = new ByteArray(packageSize);

                package.Add(new byte[] { 3, 0, 0 });
                package.Add((byte)packageSize);
                package.Add(new byte[] { 2, 0xf0, 0x80, 0x32, 1, 0, 0 });
                package.Add(Word.ToByteArray((ushort)(varCount - 1)));
                package.Add(new byte[] { 0, 0x0e });
                package.Add(Word.ToByteArray((ushort)(varCount + 4)));
                package.Add(new byte[] { 0x05, 0x01, 0x12, 0x0a, 0x10, 0x02 });
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

                var result = _socketRx.Receive(tag, bReceive, 512);

                if (bReceive[21] != 0xff)
                {
                    throw new Exception(nameof(ErrorCode.WrongNumberReceivedBytes));
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

        private void GetTagValue(Tag tag)
        {
            var result = Read(tag);
            if (result == null || result is ErrorCode)
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
        /// <param name="varCount">Number of.</param>
        /// <returns>An object.</returns>
        private T? Read<T>(Tag tag, DataType dataType, int db, int startByteAdr, VarType varType, int varCount)
        {
            try
            {
                var cntBytes = VarTypeToByteLength(varType, varCount);
                var bytes = ReadBytes(tag, dataType, db, startByteAdr, cntBytes);
                return bytes == null ? default : (T?)ParseBytes(varType, bytes, varCount);
            }
            catch (Exception ex)
            {
                _lastError.OnNext(ex.Message);
            }

            return default;
        }

        /// <summary>
        /// Reads the specified tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns>An object.</returns>
        private object? Read(Tag tag)
        {
            if (string.IsNullOrWhiteSpace(tag.Address))
            {
                throw new ArgumentNullException(nameof(tag.Address));
            }

            DataType dataType;
            int dB;
            int mByte;
            int mBit;

            BitArray objBoolArray;

            // remove spaces
            var correctVariable = tag.Address!.ToUpper().Replace(" ", string.Empty);

            try
            {
                switch (correctVariable.Substring(0, 2))
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
                                return Read<byte>(tag, DataType.DataBlock, dB, dbIndex, VarType.Byte, 1);

                            case "DBW":
                                return Read<ushort>(tag, DataType.DataBlock, dB, dbIndex, VarType.Word, 1);

                            case "DBD":
                                if (tag.Type == typeof(double))
                                {
                                    return Read<double>(tag, DataType.DataBlock, dB, dbIndex, VarType.Real, 1);
                                }

                                return Read<uint>(tag, DataType.DataBlock, dB, dbIndex, VarType.DWord, 1);

                            case "DBX":
                                mByte = dbIndex;
                                mBit = int.Parse(strings[2]);
                                if (mBit > 7)
                                {
                                    throw new Exception();
                                }

                                var obj2 = Read<byte>(tag, DataType.DataBlock, dB, mByte, VarType.Byte, 1);
                                objBoolArray = new BitArray(new byte[] { obj2 });
                                return objBoolArray[mBit];

                            default:
                                throw new Exception();
                        }

                    case "EB":

                        // Input byte
                        return Read<byte>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte, 1);

                    case "EW":

                        // Input word
                        return Read<ushort>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.Word, 1);

                    case "ED":

                        // Input double-word
                        return Read<uint>(tag, DataType.Input, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord, 1);

                    case "AB":

                        // Output byte
                        return Read<byte>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte, 1);

                    case "AW":

                        // Output word
                        return Read<ushort>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.Word, 1);

                    case "AD":

                        // Output double-word
                        return Read<uint>(tag, DataType.Output, 0, int.Parse(correctVariable.Substring(2)), VarType.DWord, 1);

                    case "MB":

                        // Memory byte
                        return Read<byte>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Byte, 1);

                    case "MW":

                        // Memory word
                        return Read<ushort>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Word, 1);

                    case "MD":

                        // Memory double-word
                        return Read<double>(tag, DataType.Memory, 0, int.Parse(correctVariable.Substring(2)), VarType.Real, 1);

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
                                return Read<double>(tag, DataType.Timer, 0, int.Parse(correctVariable.Substring(1)), VarType.Timer, 1);

                            case "Z":
                            case "C":

                                // Counter
                                return Read<ushort>(tag, DataType.Counter, 0, int.Parse(correctVariable.Substring(1)), VarType.Counter, 1);

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

                        var obj3 = Read<byte>(tag, dataType, 0, mByte, VarType.Byte, 1);
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
            try
            {
                var bytes = new byte[count];

                // first create the header
                const int packageSize = 31;
                var package = new ByteArray(packageSize);
                package.Add(ReadHeaderPackage());

                // package.Add(0x02); // data type
                package.Add(CreateReadDataRequestPackage(dataType, db, startByteAdr, count));

                var sent = _socketRx.Send(tag, package.Array, package.Array.Length);
                if (package.Array.Length != sent)
                {
                    return default;
                }

                var bReceive = new byte[512];
                var result = _socketRx.Receive(tag, bReceive, 512);
                if (bReceive[21] != 0xff)
                {
                    throw new Exception(nameof(ErrorCode.WrongNumberReceivedBytes));
                }

                for (var cnt = 0; cnt < count; cnt++)
                {
                    bytes[cnt] = bReceive[cnt + 25];
                }

                return bytes;
            }
            catch (Exception exc)
            {
                _lastErrorCode.OnNext(ErrorCode.WriteData);
                _lastError.OnNext(exc.Message);
                return default;
            }
        }

        /// <summary>
        /// Reads all the bytes needed to fill a class in C#, starting from a certain address, and
        /// set all the properties values to the value that are read from the PLC. This reads only
        /// properties, it doesn't read private variable or public variable without {get;set;} specified.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <param name="sourceClass">Instance of the class that will store the values.</param>
        /// <param name="db">Index of the DB; es.: 1 is for DB1.</param>
        /// <param name="startByteAdr">
        /// Start byte address. If you want to read DB1.DBW200, this is 200.
        /// </param>
        private void ReadClass(Tag tag, object sourceClass, int db, int startByteAdr = 0)
        {
            var classType = sourceClass.GetType();
            var numBytes = Class.GetClassSize(classType);

            // now read the package
            var resultBytes = ReadMultipleBytes(tag, numBytes, db, startByteAdr);

            // and decode it
            Class.FromBytes(sourceClass, classType, resultBytes.ToArray());
        }

        private List<byte> ReadMultipleBytes(Tag tag, int numBytes, int db, int startByteAdr = 0)
        {
            var resultBytes = new List<byte>();
            var index = startByteAdr;
            while (numBytes > 0)
            {
                var maxToRead = Math.Min(numBytes, 200);
                var bytes = ReadBytes(tag, DataType.DataBlock, db, index, maxToRead);
                if (bytes == null)
                {
                    return new List<byte>();
                }

                resultBytes.AddRange(bytes);
                numBytes -= maxToRead;
                index += maxToRead;
            }

            return resultBytes;
        }

        private IObservable<Unit> TagReaderObservable() =>
            Observable.Create<Unit>(__ =>
                {
                    var isReading = false;
                    var tim = _restartReadCycle
                        .Throttle(TimeSpan.FromMilliseconds(100))
                        .StartWith(default(Unit))
                        .Subscribe(async _ =>
                    {
                        if (_isConnected && !isReading)
                        {
                            _lock.Wait();
                            isReading = true;
                            foreach (Tag tag in TagList.Values)
                            {
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

                            _lock.Release();
                            await Task.Delay(10);
                            isReading = false;
                            _pLCRequestSubject.OnNext(new PLCRequest(PLCRequestType.Restart, default!));
                        }
                    });
                    _restartReadCycle.OnNext(default);
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
        private bool Write(Tag tag, DataType dataType, int db, int startByteAdr, object value)
        {
            if (value == null)
            {
                return false;
            }

            byte[] package;
            switch (value.GetType().Name)
            {
                case "Byte":
                    package = new byte[] { (byte)value };
                    break;

                case "Int16":
                    package = Int.ToByteArray((short)value);
                    break;

                case "UInt16":
                    var parsed = ushort.Parse(value.ToString()!);
                    var vOut = BitConverter.GetBytes(parsed);
                    package = new byte[] { vOut[1], vOut[0] };
                    break;

                case "ushort":
                    package = Word.ToByteArray((ushort)value);
                    break;

                case "Int32":
                    package = DInt.ToByteArray((int)value);
                    break;

                case "uint":
                    package = DWord.ToByteArray((uint)value);
                    break;

                case "Double":
                    package = Real.ToByteArray((double)value);
                    break;

                case "Byte[]":
                    package = (byte[])value;
                    break;

                case "Int16[]":
                    package = Int.ToByteArray((short[])value);
                    break;

                case "ushort[]":
                    package = Word.ToByteArray((ushort[])value);
                    break;

                case "Int32[]":
                    package = DInt.ToByteArray((int[])value);
                    break;

                case "uint[]":
                    package = DWord.ToByteArray((uint[])value);
                    break;

                case "Double[]":
                    package = Real.ToByteArray((double[])value);
                    break;

                case "String":
                    package = PlcTypes.String.ToByteArray(value as string);
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

        private bool WriteString(Tag tag)
        {
            DataType mDataType;
            int mDB;
            int mByte;
            int mBit;

            string addressLocation;
            byte @byte;
            object objValue;

            var txt = tag.Address!.ToUpper();
            txt = txt.Replace(" ", string.Empty); // Remove spaces

            try
            {
                switch (txt.Substring(0, 2))
                {
                    case "DB":
                        var strings = txt.Split(new char[] { '.' });
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
                                objValue = Convert.ChangeType(tag.NewValue!, typeof(byte));
                                return Write(tag, DataType.DataBlock, mDB, dbIndex, (byte)objValue);

                            case "DBW":

                                if (tag.Value is short @int)
                                {
                                    return Write(tag, DataType.DataBlock, mDB, dbIndex, @int);
                                }

                                objValue = Convert.ChangeType(tag.NewValue!, typeof(ushort));

                                return Write(tag, DataType.DataBlock, mDB, dbIndex, (ushort)objValue);

                            case "DBD":
                                if (tag.NewValue is int int1)
                                {
                                    return Write(tag, DataType.DataBlock, mDB, dbIndex, int1);
                                }
                                else if (tag.NewValue is double dbl1)
                                {
                                    return Write(tag, DataType.DataBlock, mDB, dbIndex, dbl1);
                                }
                                else
                                {
                                    objValue = Convert.ChangeType(tag.NewValue!, typeof(uint));
                                }

                                return Write(tag, DataType.DataBlock, mDB, dbIndex, (uint)objValue);

                            case "DBX":
                                mByte = dbIndex;
                                mBit = int.Parse(strings[2]);
                                if (mBit > 7)
                                {
                                    throw new Exception(string.Format("Addressing Error: You can only reference bitwise locations 0-7. Address {0} is invalid", mBit));
                                }

                                var b = Read<byte>(tag, DataType.DataBlock, mDB, mByte, VarType.Byte, 1);
                                if (Convert.ToInt32(tag.NewValue) == 1)
                                {
                                    b = (byte)(b | (byte)Math.Pow(2, mBit)); // set bit
                                }
                                else
                                {
                                    b = (byte)(b & (b ^ (byte)Math.Pow(2, mBit))); // reset bit
                                }

                                return Write(tag, DataType.DataBlock, mDB, mByte, b);

                            case "DBS":

                                // DB-String
                                return Write(tag, DataType.DataBlock, mDB, dbIndex, (string)tag.NewValue!);

                            default:
                                throw new Exception(string.Format("Addressing Error: Unable to parse address {0}. Supported formats include DBB (BYTE), DBW (WORD), DBD (DWORD), DBX (BITWISE), DBS (STRING).", dbType));
                        }

                    case "EB":

                        // Input Byte
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(byte));
                        return Write(tag, DataType.Input, 0, int.Parse(txt.Substring(2)), (byte)objValue);

                    case "EW":

                        // Input Word
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(ushort));
                        return Write(tag, DataType.Input, 0, int.Parse(txt.Substring(2)), (ushort)objValue);

                    case "ED":

                        // Input Double-Word
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(uint));
                        return Write(tag, DataType.Input, 0, int.Parse(txt.Substring(2)), (uint)objValue);

                    case "AB":

                        // Output Byte
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(byte));
                        return Write(tag, DataType.Output, 0, int.Parse(txt.Substring(2)), (byte)objValue);

                    case "AW":

                        // Output Word
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(ushort));
                        return Write(tag, DataType.Output, 0, int.Parse(txt.Substring(2)), (ushort)objValue);

                    case "AD":

                        // Output Double-Word
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(uint));
                        return Write(tag, DataType.Output, 0, int.Parse(txt.Substring(2)), (uint)objValue);

                    case "MB":

                        // Memory Byte
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(byte));
                        return Write(tag, DataType.Memory, 0, int.Parse(txt.Substring(2)), (byte)objValue);

                    case "MW":

                        // Memory Word
                        objValue = Convert.ChangeType(tag.NewValue!, typeof(ushort));
                        return Write(tag, DataType.Memory, 0, int.Parse(txt.Substring(2)), (ushort)objValue);

                    case "MD":

                        // Memory Double-Word
                        return Write(tag, DataType.Memory, 0, int.Parse(txt.Substring(2)), tag.NewValue!);

                    default:
                        switch (txt.Substring(0, 1))
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
                                return Write(tag, DataType.Timer, 0, int.Parse(txt.Substring(1)), (double)tag.NewValue!);

                            case "Z":
                            case "C":

                                // Counter
                                return Write(tag, DataType.Counter, 0, int.Parse(txt.Substring(1)), (short)tag.NewValue!);

                            default:
                                throw new Exception(string.Format("Unknown variable type {0}.", txt.Substring(0, 1)));
                        }

                        addressLocation = txt.Substring(1);
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

                        @byte = Read<byte>(tag, mDataType, 0, mByte, VarType.Byte, 1);

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
                                @byte = (byte)(@byte | (byte)Math.Pow(2, mBit));      // Set bit
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
}
