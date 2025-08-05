// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

internal static class Real
{
    /// <summary>
    /// Converts a S7 Real (4 bytes) to float.
    /// </summary>
    public static float FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a S7 Real from span to float.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A float value.</returns>
    public static float FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 4)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes span must contain 4 bytes.");
        }

        // S7 uses big-endian, so we need to handle endianness
        if (BitConverter.IsLittleEndian)
        {
            // Create a temporary span and reverse for big-endian
            Span<byte> temp = stackalloc byte[4];
            bytes.CopyTo(temp);
            temp.Reverse();
            return MemoryMarshal.Read<float>(temp);
        }

        return MemoryMarshal.Read<float>(bytes);
    }

    /// <summary>
    /// Converts a float to S7 Real (4 bytes).
    /// </summary>
    public static byte[] ToByteArray(float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes a float value to the specified span in S7 Real format.
    /// </summary>
    /// <param name="value">The float value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(float value, Span<byte> destination)
    {
        if (destination.Length < 4)
        {
            throw new ArgumentException("Destination span must be at least 4 bytes", nameof(destination));
        }

        MemoryMarshal.Write(destination, ref value);

        // S7 uses big-endian, so reverse if we're on little-endian platform
        if (BitConverter.IsLittleEndian)
        {
            destination.Slice(0, 4).Reverse();
        }
    }

    /// <summary>
    /// Converts an array of float to an array of bytes.
    /// </summary>
    public static byte[] ToByteArray(float[] value) => TypeConverter.ToByteArray(value, ToByteArray);

    /// <summary>
    /// Converts multiple float values to the specified span.
    /// </summary>
    /// <param name="values">The float values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<float> values, Span<byte> destination)
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
    /// Converts an array of S7 Real to an array of float.
    /// </summary>
    public static float[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a span of S7 Real bytes to an array of float.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>An array of float values.</returns>
    public static float[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 4;
        var entries = bytes.Length / typeSize;
        var values = new float[entries];

        for (var i = 0; i < entries; ++i)
        {
            values[i] = FromSpan(bytes.Slice(i * typeSize, typeSize));
        }

        return values;
    }
}
