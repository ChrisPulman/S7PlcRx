// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Core;

internal static class S7MultiVar
{
    internal static byte[] BuildReadVarRequest(IReadOnlyList<ReadItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (items.Count == 0)
        {
            return [];
        }

        if (items.Count > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(items), items.Count, "S7 ReadVar supports up to 255 items per PDU.");
        }

        // Header is 19 bytes, each item 12 bytes.
        var size = 19 + (12 * items.Count);
        var package = new ByteArray(size);

        // TPKT
        package.Add([3, 0, 0]);
        package.Add((byte)size);

        // COTP + S7 header start
        package.Add([2, 240, 128, 50, 1, 0, 0, 0, 0]);

        // Parameter length = 2 + 12*n
        package.Add(Word.ToByteArray((ushort)(2 + (items.Count * 12))));

        // Data length = 0 for read request
        package.Add([0, 0]);

        // Function = Read Var (0x04)
        package.Add(4);

        // Item count
        package.Add((byte)items.Count);

        foreach (var item in items)
        {
            package.Add(BuildVarSpec(item.DataType, item.Db, item.StartByteAdr, item.Count));
        }

        return package.Array;
    }

    internal static IReadOnlyList<ReadResult> ParseReadVarResponse(
        ReadOnlySpan<byte> response,
        IReadOnlyList<ReadItem> items,
        ArrayPool<byte> pool)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (pool == null)
        {
            throw new ArgumentNullException(nameof(pool));
        }

        if (items.Count == 0 || response.Length < 17)
        {
            return [];
        }

        // Parameter length is a big-endian ushort at offset 13/14 in the full frame.
        var paramLength = (ushort)((response[13] << 8) | response[14]);
        var dataStart = 17 + paramLength;
        if ((uint)dataStart >= (uint)response.Length)
        {
            return Array.Empty<ReadResult>();
        }

        var results = new List<ReadResult>(items.Count);
        var offset = dataStart;

        for (var i = 0; i < items.Count; i++)
        {
            if (offset + 4 > response.Length)
            {
                break;
            }

            var returnCode = response[offset];
            var transportSize = response[offset + 1];
            var bitLength = (response[offset + 2] << 8) | response[offset + 3];
            offset += 4;

            var byteLen = (bitLength + 7) / 8;
            if (byteLen < 0 || offset + byteLen > response.Length)
            {
                break;
            }

            if (byteLen == 0)
            {
                results.Add(new ReadResult(items[i].TagName, returnCode, transportSize, null, 0));
            }
            else
            {
                var rented = pool.Rent(byteLen);
                response.Slice(offset, byteLen).CopyTo(rented.AsSpan(0, byteLen));
                results.Add(new ReadResult(items[i].TagName, returnCode, transportSize, rented, byteLen));
            }

            offset += byteLen;

            // Data padded to even length.
            if ((byteLen & 1) == 1)
            {
                offset++;
            }
        }

        return results;
    }

    internal static byte[] BuildWriteVarRequest(IReadOnlyList<WriteItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (items.Count == 0)
        {
            return [];
        }

        if (items.Count > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(items), items.Count, "S7 WriteVar supports up to 255 items per PDU.");
        }

        var paramLength = 2 + (items.Count * 12);
        var dataLength = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var len = items[i].Data.Length;
            dataLength += 4 + len + ((len & 1) == 1 ? 1 : 0);
        }

        var size = 17 + paramLength + dataLength;
        var package = new ByteArray(size);

        // TPKT
        package.Add([3, 0, 0]);
        package.Add((byte)size);

        // COTP + S7 header start
        package.Add([2, 240, 128, 50, 1, 0, 0, 0, 0]);

        package.Add(Word.ToByteArray((ushort)paramLength));
        package.Add(Word.ToByteArray((ushort)dataLength));

        // Function = Write Var (0x05)
        package.Add(5);
        package.Add((byte)items.Count);

        foreach (var item in items)
        {
            package.Add(BuildVarSpec(item.DataType, item.Db, item.StartByteAdr, item.Count));
        }

        foreach (var item in items)
        {
            // Return code (0 for request)
            package.Add(0);

            // Transport size
            package.Add(item.TransportSize);

            // Length in bits
            package.Add(Word.ToByteArray((ushort)(item.Data.Length * 8)));

            // Data
            package.Add(item.Data);

            if ((item.Data.Length & 1) == 1)
            {
                package.Add(0);
            }
        }

        return package.Array;
    }

    internal static IReadOnlyList<WriteResult> ParseWriteVarResponse(ReadOnlySpan<byte> response, int expectedItemCount)
    {
        if (expectedItemCount <= 0 || response.Length < 17)
        {
            return [];
        }

        var paramLength = (ushort)((response[13] << 8) | response[14]);
        var dataStart = 17 + paramLength;
        if ((uint)dataStart >= (uint)response.Length)
        {
            return [];
        }

        var results = new List<WriteResult>(expectedItemCount);
        var offset = dataStart;

        for (var i = 0; i < expectedItemCount; i++)
        {
            if (offset >= response.Length)
            {
                break;
            }

            results.Add(new WriteResult(i, response[offset]));
            offset++;
        }

        return results;
    }

    private static byte[] BuildVarSpec(DataType dataType, int db, int startByteAdr, int count)
    {
        var package = new ByteArray(12);
        package.Add([18, 10, 16]);

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

        var overflow = (int)(startByteAdr * 8 / 65535U);
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

        return package.Array;
    }

    internal readonly struct ReadItem(DataType dataType, int db, int startByteAdr, int count, string tagName)
    {
        public DataType DataType { get; } = dataType;

        public int Db { get; } = db;

        public int StartByteAdr { get; } = startByteAdr;

        public int Count { get; } = count;

        public string TagName { get; } = tagName;
    }

    internal readonly struct ReadResult(string tagName, byte returnCode, byte transportSize, byte[]? rentedBuffer, int length)
    {
        public string TagName { get; } = tagName;

        public byte ReturnCode { get; } = returnCode;

        public byte TransportSize { get; } = transportSize;

        public byte[]? RentedBuffer { get; } = rentedBuffer;

        public int Length { get; } = length;

        public ReadOnlyMemory<byte> Data => RentedBuffer == null || Length <= 0 ? ReadOnlyMemory<byte>.Empty : RentedBuffer.AsMemory(0, Length);
    }

    internal readonly struct WriteItem(DataType dataType, int db, int startByteAdr, int count, byte transportSize, byte[] data, string tagName)
    {
        public DataType DataType { get; } = dataType;

        public int Db { get; } = db;

        public int StartByteAdr { get; } = startByteAdr;

        public int Count { get; } = count;

        public byte TransportSize { get; } = transportSize;

        public byte[] Data { get; } = data;

        public string TagName { get; } = tagName;
    }

    internal readonly struct WriteResult(int index, byte returnCode)
    {
        public int Index { get; } = index;

        public byte ReturnCode { get; } = returnCode;
    }
}
