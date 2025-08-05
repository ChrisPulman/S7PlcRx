// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Word.
/// </summary>
internal static class Word
{
    /// <summary>
    /// From the byte array. bytes[0] -&gt; HighByte, bytes[1] -&gt; LowByte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// From the span.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromSpan(ReadOnlySpan<byte> bytes) => FromBytes(bytes[1], bytes[0]);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// From the bytes.
    /// </summary>
    /// <param name="loVal">The lo value.</param>
    /// <param name="hiVal">The hi value.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromBytes(byte loVal, byte hiVal) => (ushort)((hiVal * 256) + loVal);

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A ushort array.</returns>
    public static ushort[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// To the array from span.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A ushort array.</returns>
    public static ushort[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 2;
        var entries = bytes.Length / typeSize;
        var values = new ushort[entries];

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
    public static byte[] ToByteArray(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the value to the specified span.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ushort value, Span<byte> destination)
    {
        if (destination.Length < 2)
        {
            throw new ArgumentException("Destination span must be at least 2 bytes", nameof(destination));
        }

        destination[0] = (byte)(value >> 8);
        destination[1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Converts to bytearray.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="start">The start.</param>
    public static void ToByteArray(ushort value, Array destination, int start)
    {
        var bytes = ToByteArray(value);
        Array.Copy(bytes, 0, destination, start, 2);
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort[] value) => TypeConverter.ToByteArray(value, ToByteArray);

    /// <summary>
    /// Writes multiple values to the specified span.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<ushort> values, Span<byte> destination)
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
}
