// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// DWord.
/// </summary>
internal static class DWord
{
    /// <summary>
    /// Converts a DWord (4 bytes) to uint.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A uint.</returns>
    public static uint FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A uint.</returns>
    public static uint FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Converts a S7 DWord from span to uint.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A uint value.</returns>
    public static uint FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes span must contain at least 4 bytes.");
        }

        // S7 uses big-endian byte order
        if (BitConverter.IsLittleEndian)
        {
            Span<byte> temp = stackalloc byte[4];
            bytes.Slice(0, 4).CopyTo(temp);
            temp.Reverse();
            return MemoryMarshal.Read<uint>(temp);
        }

        return MemoryMarshal.Read<uint>(bytes);
    }

    /// <summary>
    /// Froms the bytes.
    /// </summary>
    /// <param name="v1">The v1.</param>
    /// <param name="v2">The v2.</param>
    /// <param name="v3">The v3.</param>
    /// <param name="v4">The v4.</param>
    /// <returns>A uint.</returns>
    public static uint FromBytes(byte v1, byte v2, byte v3, byte v4) =>
        (uint)(v1 | (v2 << 8) | (v3 << 16) | (v4 << 24));

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A uint array.</returns>
    public static uint[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a span of S7 DWord bytes to an array of uint.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>An array of uint values.</returns>
    public static uint[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 4;
        var entries = bytes.Length / typeSize;
        var values = new uint[entries];

        for (var i = 0; i < entries; ++i)
        {
            values[i] = FromSpan(bytes.Slice(i * typeSize, typeSize));
        }

        return values;
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes a uint value to the specified span in S7 DWord format.
    /// </summary>
    /// <param name="value">The uint value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(uint value, Span<byte> destination)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination span must be at least 4 bytes", nameof(destination));
        }

#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
        MemoryMarshal.Write(destination, ref value);
#pragma warning restore CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

        // S7 uses big-endian, so reverse if we're on little-endian platform
        if (BitConverter.IsLittleEndian)
        {
            destination.Slice(0, 4).Reverse();
        }
    }

    /// <summary>
    /// Converts multiple uint values to the specified span.
    /// </summary>
    /// <param name="values">The uint values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<uint> values, Span<byte> destination)
    {
        if (destination.Length < values.Length * 4)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < values.Length; i++)
        {
            ToSpan(values[i], destination.Slice(i * 4, 4));
        }
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(uint[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
