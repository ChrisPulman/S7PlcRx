// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1201, SA1204

using System.Collections.Concurrent;
using S7PlcRx.PlcTypes;
using Timer = System.Threading.Timer;

namespace S7PlcRx.Binding;

/// <summary>
/// Runtime engine used by generated tag bindings to poll and write PLC DB values in byte-array batches.
/// </summary>
public sealed class S7TagRuntimeBinding : IDisposable
{
    private const int MaxReadGapBytes = 16;
    private const int WriteFlushMs = 20;
    private readonly IRxS7 _plc;
    private readonly IReadOnlyList<S7TagDefinition> _definitions;
    private readonly Dictionary<string, S7TagRuntimeAddress> _addresses;
    private readonly Dictionary<string, S7TagDefinition> _definitionsByName;
    private readonly Action<string, object?> _applyRead;
    private readonly List<Timer> _timers = [];
    private readonly ConcurrentDictionary<string, object?> _pendingWrites = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly Timer _writeTimer;
    private bool _disposed;

    private S7TagRuntimeBinding(IRxS7 plc, IReadOnlyList<S7TagDefinition> definitions, Action<string, object?> applyRead)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _applyRead = applyRead ?? throw new ArgumentNullException(nameof(applyRead));
        _definitionsByName = definitions.ToDictionary(static d => d.Name, StringComparer.InvariantCultureIgnoreCase);
        _addresses = definitions.ToDictionary(static d => d.Name, ParseAddress, StringComparer.InvariantCultureIgnoreCase);

        RegisterTags();
        StartPollers();
        _writeTimer = new Timer(_ => FlushWrites(), null, WriteFlushMs, WriteFlushMs);
    }

    /// <summary>
    /// Creates and starts a runtime binding for generated PLC tag definitions.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="definitions">The tag definitions emitted by the source generator.</param>
    /// <param name="applyRead">A generated callback that assigns PLC values to backing fields without re-writing them.</param>
    /// <returns>A disposable runtime binding.</returns>
    public static S7TagRuntimeBinding Bind(IRxS7 plc, IReadOnlyList<S7TagDefinition> definitions, Action<string, object?> applyRead) =>
        new(plc, definitions, applyRead);

    /// <summary>
    /// Queues a generated property change for a grouped byte-array write.
    /// </summary>
    /// <param name="name">The generated tag/property name.</param>
    /// <param name="value">The new property value.</param>
    public void Write(string name, object? value)
    {
        if (_disposed || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (_definitionsByName.TryGetValue(name, out var definition) && definition.CanWrite)
        {
            _pendingWrites[name] = value;
        }
    }

    /// <summary>
    /// Releases timers and pending write state.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeTimer.Dispose();
        foreach (var timer in _timers)
        {
            timer.Dispose();
        }

        _ioLock.Dispose();
        _pendingWrites.Clear();
    }

    private static S7TagRuntimeAddress ParseAddress(S7TagDefinition definition)
    {
        var address = definition.Address.ToUpperInvariant().Replace(" ", string.Empty);
        if (!address.StartsWith("DB", StringComparison.Ordinal))
        {
            throw new ArgumentException("Only DB addresses are supported by generated S7 tag byte-array bindings.", nameof(definition));
        }

        var parts = address.Split(['.']);
        if (parts.Length < 2 || !int.TryParse(parts[0].Substring(2), out var db))
        {
            throw new ArgumentException($"Invalid S7 DB address '{definition.Address}'.", nameof(definition));
        }

        var dbPart = parts[1];
        if (dbPart.StartsWith("DBX", StringComparison.Ordinal))
        {
            if (parts.Length < 3 || !int.TryParse(dbPart.Substring(3), out var byteOffset) || !int.TryParse(parts[2], out var bitOffset))
            {
                throw new ArgumentException($"Invalid S7 DB bit address '{definition.Address}'.", nameof(definition));
            }

            if ((uint)bitOffset > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(definition), "DBX bit offset must be between 0 and 7.");
            }

            return new S7TagRuntimeAddress(db, byteOffset, bitOffset, 1);
        }

        if (dbPart.Length < 4 || !int.TryParse(dbPart.Substring(3), out var startByte))
        {
            throw new ArgumentException($"Invalid S7 DB address '{definition.Address}'.", nameof(definition));
        }

        var byteLength = GetByteLength(definition, dbPart.Substring(0, 3));
        return new S7TagRuntimeAddress(db, startByte, null, byteLength);
    }

    private static int GetByteLength(S7TagDefinition definition, string dbType)
    {
        if (definition.ValueType == typeof(byte[]))
        {
            return definition.ArrayLength;
        }

        if (definition.ValueType == typeof(string))
        {
            return definition.ArrayLength;
        }

        var multiplier = definition.ValueType.IsArray ? definition.ArrayLength : 1;
        var elementType = definition.ValueType.IsArray ? definition.ValueType.GetElementType()! : definition.ValueType;
        return dbType switch
        {
            "DBB" => Math.Max(1, multiplier),
            "DBW" => 2 * multiplier,
            "DBD" when elementType == typeof(double) => 8 * multiplier,
            "DBD" => 4 * multiplier,
            _ => throw new ArgumentException($"Unsupported S7 DB address type '{dbType}'.", nameof(definition)),
        };
    }

    private static List<S7TagRange> BuildRanges(IEnumerable<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> tags)
    {
        var ranges = new List<S7TagRange>();
        foreach (var item in tags.OrderBy(static i => i.Value.Db).ThenBy(static i => i.Value.StartByte))
        {
            var endByte = item.Value.StartByte + item.Value.ByteLength;
            var current = ranges.Count > 0 ? ranges[^1] : null;
            if (current != null && current.Db == item.Value.Db && item.Value.StartByte <= current.EndByte + MaxReadGapBytes)
            {
                current.EndByte = Math.Max(current.EndByte, endByte);
                current.Items.Add(item);
            }
            else
            {
                ranges.Add(new S7TagRange(item.Value.Db, item.Value.StartByte, endByte, [item]));
            }
        }

        return ranges;
    }

    private static object? Decode(S7TagDefinition definition, S7TagRuntimeAddress address, byte[] buffer, int rangeStart)
    {
        var offset = address.StartByte - rangeStart;
        var span = buffer.AsSpan(offset, address.ByteLength);
        if (address.BitOffset.HasValue)
        {
            return Bit.FromByte(span[0], (byte)address.BitOffset.Value);
        }

        if (definition.ValueType == typeof(byte[]))
        {
            return span.ToArray();
        }

        if (definition.ValueType == typeof(byte))
        {
            return span[0];
        }

        if (definition.ValueType == typeof(bool))
        {
            return span[0] != 0;
        }

        if (definition.ValueType == typeof(short))
        {
            return Int.FromSpan(span);
        }

        if (definition.ValueType == typeof(ushort))
        {
            return Word.FromSpan(span);
        }

        if (definition.ValueType == typeof(int))
        {
            return DInt.FromSpan(span);
        }

        if (definition.ValueType == typeof(uint))
        {
            return DWord.FromSpan(span);
        }

        if (definition.ValueType == typeof(float))
        {
            return Real.FromSpan(span);
        }

        if (definition.ValueType == typeof(double))
        {
            return LReal.FromSpan(span);
        }

        if (definition.ValueType == typeof(string))
        {
            return PlcTypes.String.FromByteArray(span.ToArray()).Replace("\0", string.Empty);
        }

        return DecodeArray(definition.ValueType, span);
    }

    private static object? DecodeArray(Type valueType, ReadOnlySpan<byte> span)
    {
        if (valueType == typeof(short[]))
        {
            return Int.ToArray(span);
        }

        if (valueType == typeof(ushort[]))
        {
            return Word.ToArray(span);
        }

        if (valueType == typeof(int[]))
        {
            return DInt.ToArray(span);
        }

        if (valueType == typeof(uint[]))
        {
            return DWord.ToArray(span);
        }

        if (valueType == typeof(float[]))
        {
            return Real.ToArray(span);
        }

        if (valueType == typeof(double[]))
        {
            return LReal.ToArray(span);
        }

        return null;
    }

    private static void Encode(S7TagDefinition definition, S7TagRuntimeAddress address, object? value, byte[] buffer, int rangeStart)
    {
        if (value == null)
        {
            return;
        }

        var offset = address.StartByte - rangeStart;
        var span = buffer.AsSpan(offset, address.ByteLength);
        if (address.BitOffset.HasValue)
        {
            var mask = (byte)(1 << address.BitOffset.Value);
            if ((bool)Convert.ChangeType(value, typeof(bool))!)
            {
                span[0] |= mask;
            }
            else
            {
                span[0] &= (byte)~mask;
            }

            return;
        }

        var data = ToBytes(definition.ValueType, value);
        data.AsSpan(0, Math.Min(data.Length, span.Length)).CopyTo(span);
    }

    private static byte[] ToBytes(Type valueType, object value)
    {
        if (valueType == typeof(byte[]))
        {
            return (byte[])value;
        }

        if (valueType == typeof(byte))
        {
            return [(byte)Convert.ChangeType(value, typeof(byte))!];
        }

        if (valueType == typeof(bool))
        {
            return [(bool)Convert.ChangeType(value, typeof(bool))! ? (byte)1 : (byte)0];
        }

        if (valueType == typeof(short))
        {
            return Int.ToByteArray((short)Convert.ChangeType(value, typeof(short))!);
        }

        if (valueType == typeof(ushort))
        {
            return Word.ToByteArray((ushort)Convert.ChangeType(value, typeof(ushort))!);
        }

        if (valueType == typeof(int))
        {
            return DInt.ToByteArray((int)Convert.ChangeType(value, typeof(int))!);
        }

        if (valueType == typeof(uint))
        {
            return DWord.ToByteArray((uint)Convert.ChangeType(value, typeof(uint))!);
        }

        if (valueType == typeof(float))
        {
            return Real.ToByteArray((float)Convert.ChangeType(value, typeof(float))!);
        }

        if (valueType == typeof(double))
        {
            return LReal.ToByteArray((double)Convert.ChangeType(value, typeof(double))!);
        }

        if (valueType == typeof(string))
        {
            return PlcTypes.String.ToByteArray(value as string);
        }

        if (valueType == typeof(short[]))
        {
            return Int.ToByteArray((short[])value);
        }

        if (valueType == typeof(ushort[]))
        {
            return Word.ToByteArray((ushort[])value);
        }

        if (valueType == typeof(int[]))
        {
            return DInt.ToByteArray((int[])value);
        }

        if (valueType == typeof(uint[]))
        {
            return DWord.ToByteArray((uint[])value);
        }

        if (valueType == typeof(float[]))
        {
            return Real.ToByteArray((float[])value);
        }

        if (valueType == typeof(double[]))
        {
            return LReal.ToByteArray((double[])value);
        }

        return [];
    }

    private void RegisterTags()
    {
        foreach (var definition in _definitions)
        {
            _plc.AddUpdateTagItem(definition.ValueType, definition.Name, definition.Address, definition.ArrayLength).SetTagPollIng(false);
        }
    }

    private void StartPollers()
    {
        var intervals = _definitions.Where(static d => d.CanRead).GroupBy(static d => d.PollIntervalMs);
        foreach (var interval in intervals)
        {
            var state = interval.ToArray();
            _timers.Add(new Timer(PollInterval, state, interval.Key, interval.Key));
        }
    }

    private void PollInterval(object? state)
    {
        if (_disposed || state is not S7TagDefinition[] definitions || definitions.Length == 0)
        {
            return;
        }

        _ = PollIntervalAsync(definitions);
    }

    private async Task PollIntervalAsync(IReadOnlyList<S7TagDefinition> definitions)
    {
        if (!await _ioLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var tags = definitions.Select(d => new KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>(d, _addresses[d.Name]));
            foreach (var range in BuildRanges(tags))
            {
                var bytes = await ReadRangeAsync(range).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0)
                {
                    continue;
                }

                foreach (var item in range.Items)
                {
                    var value = Decode(item.Key, item.Value, bytes, range.StartByte);
                    _applyRead(item.Key.Name, value);
                }
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private void FlushWrites()
    {
        if (_disposed || _pendingWrites.IsEmpty)
        {
            return;
        }

        _ = FlushWritesAsync();
    }

    private async Task FlushWritesAsync()
    {
        if (!await _ioLock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var pending = new List<KeyValuePair<S7TagDefinition, object?>>();
            foreach (var entry in _pendingWrites.ToArray())
            {
                if (_pendingWrites.TryRemove(entry.Key, out var value) && _definitionsByName.TryGetValue(entry.Key, out var definition))
                {
                    pending.Add(new KeyValuePair<S7TagDefinition, object?>(definition, value));
                }
            }

            var tags = pending.Select(p => new KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>(p.Key, _addresses[p.Key.Name]));
            var values = pending.ToDictionary(static p => p.Key.Name, static p => p.Value, StringComparer.InvariantCultureIgnoreCase);
            foreach (var range in BuildRanges(tags))
            {
                var bytes = await ReadRangeAsync(range).ConfigureAwait(false) ?? new byte[range.Length];
                if (bytes.Length < range.Length)
                {
                    Array.Resize(ref bytes, range.Length);
                }

                foreach (var item in range.Items)
                {
                    Encode(item.Key, item.Value, values[item.Key.Name], bytes, range.StartByte);
                }

                _plc.Value(RangeTagName(range), bytes);
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<byte[]?> ReadRangeAsync(S7TagRange range)
    {
        var tagName = RangeTagName(range);
        _plc.AddUpdateTagItem<byte[]>(tagName, $"DB{range.Db}.DBB{range.StartByte}", range.Length).SetTagPollIng(false);
        return await _plc.Value<byte[]>(tagName).ConfigureAwait(false);
    }

    private static string RangeTagName(S7TagRange range) => $"__s7_binding_db{range.Db}_{range.StartByte}_{range.Length}";

    private sealed class S7TagRange
    {
        public S7TagRange(int db, int startByte, int endByte, List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> items)
        {
            Db = db;
            StartByte = startByte;
            EndByte = endByte;
            Items = items;
        }

        public int Db { get; }

        public int StartByte { get; }

        public int EndByte { get; set; }

        public int Length => EndByte - StartByte;

        public List<KeyValuePair<S7TagDefinition, S7TagRuntimeAddress>> Items { get; }
    }

    private readonly struct S7TagRuntimeAddress
    {
        public S7TagRuntimeAddress(int db, int startByte, int? bitOffset, int byteLength)
        {
            Db = db;
            StartByte = startByte;
            BitOffset = bitOffset;
            ByteLength = byteLength;
        }

        public int Db { get; }

        public int StartByte { get; }

        public int? BitOffset { get; }

        public int ByteLength { get; }
    }
}
