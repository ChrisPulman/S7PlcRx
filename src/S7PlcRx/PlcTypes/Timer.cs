// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Timer.
/// </summary>
internal static class Timer
{
    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a S7 Timer from span to double.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A double value representing timer value.</returns>
    public static double FromSpan(ReadOnlySpan<byte> bytes) => FromByteArray(bytes, 0);

    /// <summary>
    /// From the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// From the span.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <param name="start">The start.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(ReadOnlySpan<byte> bytes, int start)
    {
        if (bytes.Length < start + 2)
        {
            throw new ArgumentException("Bytes span must contain at least 2 bytes from start position");
        }

        var value = (short)Word.FromBytes(bytes[start + 1], bytes[start]);
        var txt = value.ValToBinString();
        var wert = txt.Substring(4, 4).BinStringToInt32() * 100.0;
        wert += txt.Substring(8, 4).BinStringToInt32() * 10.0;
        wert += txt.Substring(12, 4).BinStringToInt32();
        switch (txt.Substring(2, 2))
        {
            case "00":
                wert *= 0.01;
                break;

            case "01":
                wert *= 0.1;
                break;

            case "10":
                wert *= 1.0;
                break;

            case "11":
                wert *= 10.0;
                break;
        }

        return wert;
    }

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double array.</returns>
    public static double[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a span of S7 Timer bytes to an array of double.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>An array of double values.</returns>
    public static double[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 2;
        var entries = bytes.Length / typeSize;
        var values = new double[entries];

        for (var i = 0; i < entries; ++i)
        {
            values[i] = FromByteArray(bytes, i * typeSize);
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
    /// Writes a ushort value to the specified span in S7 Timer format.
    /// </summary>
    /// <param name="value">The ushort value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ushort value, Span<byte> destination)
    {
        if (destination.Length < 2)
        {
            throw new ArgumentException("Destination span must be at least 2 bytes", nameof(destination));
        }

        // Convert using the same logic as original but more efficiently
        const int x = 2;
        long valLong = value;
        for (var cnt = 0; cnt < x; cnt++)
        {
            var x1 = 1L << (cnt * 8); // More efficient than Math.Pow(256, cnt)
            var x3 = valLong / x1;
            destination[x - cnt - 1] = (byte)(x3 & 255);
            valLong -= destination[x - cnt - 1] * x1;
        }
    }

    /// <summary>
    /// Converts multiple ushort values to the specified span.
    /// </summary>
    /// <param name="values">The ushort values.</param>
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

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
