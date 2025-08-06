// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Int.
/// </summary>
internal static class Int
{
    /// <summary>
    /// cs the word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A short.</returns>
    public static short CWord(int value)
    {
        if (value > 32767)
        {
            value -= 32768;
            value = 32768 - value;
            value *= -1;
        }

        return (short)value;
    }

    /// <summary>
    /// From the byte array.bytes[0] -&gt; HighByte bytes[1] -&gt; LowByte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A short.</returns>
    public static short FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A short.</returns>
    public static short FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Converts a S7 Int from span to short.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A short value.</returns>
    public static short FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes span must contain at least 2 bytes.");
        }

        // S7 uses big-endian byte order
        if (BitConverter.IsLittleEndian)
        {
            Span<byte> temp = stackalloc byte[2];
            bytes.Slice(0, 2).CopyTo(temp);
            temp.Reverse();
            return MemoryMarshal.Read<short>(temp);
        }

        return MemoryMarshal.Read<short>(bytes);
    }

    /// <summary>
    /// From the bytes.
    /// </summary>
    /// <param name="loVal">The lo value.</param>
    /// <param name="hiVal">The hi value.</param>
    /// <returns>A short.</returns>
    public static short FromBytes(byte loVal, byte hiVal) => (short)((hiVal << 8) | loVal);

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A short array.</returns>
    public static short[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a span of S7 Int bytes to an array of short.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>An array of short values.</returns>
    public static short[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 2;
        var entries = bytes.Length / typeSize;
        var values = new short[entries];

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
    public static byte[] ToByteArray(short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes a short value to the specified span in S7 Int format.
    /// </summary>
    /// <param name="value">The short value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(short value, Span<byte> destination)
    {
        if (destination.Length < 2)
        {
            throw new ArgumentException("Destination span must be at least 2 bytes", nameof(destination));
        }

#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
        MemoryMarshal.Write(destination, ref value);
#pragma warning restore CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

        // S7 uses big-endian, so reverse if we're on little-endian platform
        if (BitConverter.IsLittleEndian)
        {
            destination.Slice(0, 2).Reverse();
        }
    }

    /// <summary>
    /// Converts multiple short values to the specified span.
    /// </summary>
    /// <param name="values">The short values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<short> values, Span<byte> destination)
    {
        if (destination.Length < values.Length * 2)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < values.Length; i++)
        {
            ToSpan(values[i], destination.Slice(i * 2, 2));
        }
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(short[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
