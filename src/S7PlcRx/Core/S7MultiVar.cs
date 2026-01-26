// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Core;

/// <summary>
/// Provides static helper methods for constructing and parsing S7 protocol multi-variable read and write requests and
/// responses.
/// </summary>
/// <remarks>This class is intended for internal use when communicating with Siemens S7 PLCs using the S7
/// protocol. It encapsulates the low-level details of building and interpreting S7 ReadVar and WriteVar PDUs for batch
/// operations. All members are static and thread-safe.</remarks>
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

    /// <summary>
    /// Parses a PLC 'Read Var' response frame and returns the results for each requested item.
    /// </summary>
    /// <remarks>The returned list contains one entry per item in <paramref name="items"/>, up to the number
    /// of results present in the response. If the response is incomplete or malformed, fewer results may be returned.
    /// Rented buffers in the results must be returned to the provided pool by the caller when no longer
    /// needed.</remarks>
    /// <param name="response">The response data received from the PLC, as a span of bytes representing the full protocol frame.</param>
    /// <param name="items">The collection of items that were requested in the original read operation. Each item corresponds to a result in
    /// the response. Cannot be null.</param>
    /// <param name="pool">The array pool used to rent buffers for the result data. Cannot be null.</param>
    /// <returns>A read-only list of <see cref="ReadResult"/> objects containing the results for each requested item. The list
    /// may be empty if the response is invalid or contains no results.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> or <paramref name="pool"/> is null.</exception>
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

    /// <summary>
    /// Builds a byte array representing an S7 WriteVar request for the specified collection of write items.
    /// </summary>
    /// <remarks>The resulting byte array can be sent directly to an S7-compatible device to perform a
    /// multi-variable write operation. The S7 protocol limits each WriteVar request to a maximum of 255 items per
    /// protocol data unit (PDU).</remarks>
    /// <param name="items">A read-only list of write items to include in the WriteVar request. Each item specifies the data and address
    /// information to be written.</param>
    /// <returns>A byte array containing the encoded S7 WriteVar request. Returns an empty array if no items are provided.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="items"/> contains more than 255 elements.</exception>
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

    /// <summary>
    /// Parses a response buffer containing the results of a write variable operation and returns a list of write
    /// results.
    /// </summary>
    /// <remarks>If the response buffer is too short or does not contain the expected number of items, the
    /// returned list may contain fewer results than requested. This method does not throw exceptions for malformed or
    /// incomplete responses.</remarks>
    /// <param name="response">The response buffer containing the raw bytes returned from the write variable operation.</param>
    /// <param name="expectedItemCount">The expected number of write result items to parse from the response. Must be greater than zero.</param>
    /// <returns>A read-only list of <see cref="WriteResult"/> objects representing the outcome of each write operation. The list
    /// may be empty if the response is invalid or contains no results.</returns>
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

    /// <summary>
    /// Builds a variable specification (VarSpec) byte array for use in S7 protocol communication, based on the
    /// specified data type, data block, starting byte address, and element count.
    /// </summary>
    /// <remarks>The format of the returned byte array depends on the specified data type. For Timer and
    /// Counter types, the data block parameter is not used and the starting address is handled differently than for
    /// other data types. This method is typically used when building low-level S7 protocol messages for PLC
    /// communication.</remarks>
    /// <param name="dataType">The type of data area to access. Determines how the variable specification is constructed. Typical values
    /// include Timer, Counter, or other supported S7 data types.</param>
    /// <param name="db">The number of the data block to access. Ignored for data types that do not use data blocks (such as Timer or
    /// Counter).</param>
    /// <param name="startByteAdr">The starting byte address within the specified data area. For Timer and Counter types, this is interpreted as a
    /// direct address; for other types, it is multiplied by 8 to represent a bit address.</param>
    /// <param name="count">The number of elements to include in the variable specification. Must be a positive value.</param>
    /// <returns>A byte array containing the constructed variable specification suitable for use in S7 protocol requests.</returns>
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

    /// <summary>
    /// Represents a request to read a specific range of data from a PLC data block or memory area.
    /// </summary>
    /// <param name="dataType">The type of data to read, specifying the memory area or data type (such as input, output, or data block).</param>
    /// <param name="db">The number of the data block to read from. Ignored for data types that do not use data blocks.</param>
    /// <param name="startByteAdr">The starting byte address within the specified data area or data block from which to begin reading.</param>
    /// <param name="count">The number of elements or bytes to read, depending on the data type.</param>
    /// <param name="tagName">The symbolic tag name associated with the read operation. Can be used for identification or logging purposes.</param>
    internal readonly struct ReadItem(DataType dataType, int db, int startByteAdr, int count, string tagName)
    {
        public DataType DataType { get; } = dataType;

        public int Db { get; } = db;

        public int StartByteAdr { get; } = startByteAdr;

        public int Count { get; } = count;

        public string TagName { get; } = tagName;
    }

    /// <summary>
    /// Represents the result of a read operation, including the tag name, return code, transport size, data buffer, and
    /// the number of bytes read.
    /// </summary>
    /// <param name="tagName">The name of the tag that was read.</param>
    /// <param name="returnCode">The return code indicating the status of the read operation.</param>
    /// <param name="transportSize">The transport size code associated with the data read.</param>
    /// <param name="rentedBuffer">The buffer containing the raw data read from the source, or null if no data was returned.</param>
    /// <param name="length">The number of bytes in the buffer that contain valid data.</param>
    internal readonly struct ReadResult(string tagName, byte returnCode, byte transportSize, byte[]? rentedBuffer, int length)
    {
        public string TagName { get; } = tagName;

        public byte ReturnCode { get; } = returnCode;

        public byte TransportSize { get; } = transportSize;

        public byte[]? RentedBuffer { get; } = rentedBuffer;

        public int Length { get; } = length;

        public ReadOnlyMemory<byte> Data => RentedBuffer == null || Length <= 0 ? ReadOnlyMemory<byte>.Empty : RentedBuffer.AsMemory(0, Length);
    }

    /// <summary>
    /// Represents a data item to be written to a PLC, including its type, address, size, and associated data buffer.
    /// </summary>
    /// <param name="dataType">The data type of the item to write. Specifies how the data should be interpreted by the PLC.</param>
    /// <param name="db">The number of the data block (DB) in the PLC where the data will be written.</param>
    /// <param name="startByteAdr">The starting byte address within the data block where the write operation begins.</param>
    /// <param name="count">The number of elements or items to write. Must be greater than zero.</param>
    /// <param name="transportSize">The transport size code indicating how the data is transferred to the PLC.</param>
    /// <param name="data">The buffer containing the data to be written. Cannot be null.</param>
    /// <param name="tagName">The symbolic tag name associated with the data item, used for identification or diagnostics.</param>
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

    /// <summary>
    /// Represents the result of a write operation, including the operation index and return code.
    /// </summary>
    /// <param name="index">The zero-based index associated with the write operation result.</param>
    /// <param name="returnCode">The return code indicating the outcome of the write operation.</param>
    internal readonly struct WriteResult(int index, byte returnCode)
    {
        public int Index { get; } = index;

        public byte ReturnCode { get; } = returnCode;
    }
}
