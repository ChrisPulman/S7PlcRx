// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Real PLC Type.
/// </summary>
internal static class LReal
{
    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Converts a S7 LReal from span to double.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A double value.</returns>
    public static double FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes span must contain at least 8 bytes.");
        }

        // S7 uses big-endian, so we need to handle endianness
        if (BitConverter.IsLittleEndian)
        {
            // Create a temporary span and reverse for big-endian
            Span<byte> temp = stackalloc byte[8];
            bytes.Slice(0, 8).CopyTo(temp);
            temp.Reverse();
            return MemoryMarshal.Read<double>(temp);
        }

        return MemoryMarshal.Read<double>(bytes);
    }

    /// <summary>
    /// Froms the d word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A double.</returns>
    public static double FromDWord(int value) => FromByteArray(DInt.ToByteArray(value));

    /// <summary>
    /// Froms the d word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A double.</returns>
    public static double FromDWord(uint value) => FromByteArray(DWord.ToByteArray(value));

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double array.</returns>
    public static double[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a span of S7 LReal bytes to an array of double.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>An array of double values.</returns>
    public static double[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 8;
        var entries = bytes.Length / typeSize;
        var values = new double[entries];

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
    public static byte[] ToByteArray(double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes a double value to the specified span in S7 LReal format.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(double value, Span<byte> destination)
    {
        if (destination.Length < 8)
        {
            throw new ArgumentException("Destination span must be at least 8 bytes", nameof(destination));
        }

        MemoryMarshal.Write(destination, ref value);

        // S7 uses big-endian, so reverse if we're on little-endian platform
        if (BitConverter.IsLittleEndian)
        {
            destination.Slice(0, 8).Reverse();
        }
    }

    /// <summary>
    /// Converts multiple double values to the specified span.
    /// </summary>
    /// <param name="values">The double values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<double> values, Span<byte> destination)
    {
        if (destination.Length < values.Length * 8)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < values.Length; i++)
        {
            ToSpan(values[i], destination.Slice(i * 8, 8));
        }
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(double[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
