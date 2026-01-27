// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between Siemens S7 Real (4-byte IEEE 754 floating-point) representations and
/// .NET float values.
/// </summary>
/// <remarks>The methods in this class handle endianness according to the S7 protocol, which uses big-endian byte
/// order. Use these methods to serialize and deserialize float values when communicating with Siemens S7 PLCs or
/// working with S7 Real data formats. All methods are static and intended for internal use.</remarks>
internal static class Real
{
    /// <summary>
    /// Converts a byte array to a single-precision floating-point value.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must contain at least four bytes representing a 32-bit
    /// floating-point value in the expected format.</param>
    /// <returns>A single-precision floating-point value represented by the specified byte array.</returns>
    public static float FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a 4-byte big-endian span to a single-precision floating-point value.
    /// </summary>
    /// <remarks>This method interprets the input bytes as a big-endian IEEE 754 single-precision
    /// floating-point value, regardless of the system's endianness. Use this method when reading floating-point values
    /// from protocols or file formats that use big-endian byte order, such as Siemens S7 PLCs.</remarks>
    /// <param name="bytes">A read-only span of 4 bytes representing a single-precision floating-point value in big-endian byte order.</param>
    /// <returns>A single-precision floating-point value represented by the specified big-endian byte span.</returns>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="bytes"/> is not 4.</exception>
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
    /// Converts the specified single-precision floating-point value to its equivalent byte array representation.
    /// </summary>
    /// <remarks>The byte order of the returned array is platform-dependent. To ensure consistent results
    /// across different systems, consider specifying endianness explicitly if required.</remarks>
    /// <param name="value">The single-precision floating-point value to convert.</param>
    /// <returns>A 4-byte array containing the binary representation of <paramref name="value"/>.</returns>
    public static byte[] ToByteArray(float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the 4-byte big-endian representation of the specified single-precision floating-point value into the
    /// provided span.
    /// </summary>
    /// <remarks>The value is written in big-endian byte order, regardless of the system's endianness. This is
    /// commonly required for protocols or file formats that specify big-endian encoding.</remarks>
    /// <param name="value">The single-precision floating-point value to write to the span.</param>
    /// <param name="destination">The span of bytes that receives the 4-byte big-endian representation of the value. Must be at least 4 bytes in
    /// length.</param>
    /// <exception cref="ArgumentException">Thrown if the length of destination is less than 4 bytes.</exception>
    public static void ToSpan(float value, Span<byte> destination)
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
    /// Converts an array of single-precision floating-point values to a byte array.
    /// </summary>
    /// <param name="value">The array of <see cref="float"/> values to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input values.</returns>
    public static byte[] ToByteArray(float[] value) => TypeConverter.ToByteArray(value, ToByteArray);

    /// <summary>
    /// Converts a span of single-precision floating-point values to their byte representations and writes them to the
    /// specified destination span.
    /// </summary>
    /// <param name="values">The span of single-precision floating-point values to convert.</param>
    /// <param name="destination">The destination span to which the byte representations of the values are written. Must be at least four times
    /// the length of <paramref name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to contain the byte representations of all
    /// values.</exception>
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
    /// Converts a byte array to an array of single-precision floating-point values.
    /// </summary>
    /// <remarks>The method interprets each group of four bytes in the input array as a single-precision
    /// floating-point value, using the default endianness of the system. If the length of <paramref name="bytes"/> is
    /// not a multiple of 4, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array containing the binary representation of the floating-point values. The length must be a multiple
    /// of 4.</param>
    /// <returns>An array of <see cref="float"/> values converted from the specified byte array.</returns>
    public static float[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes to an array of 32-bit floating-point values.
    /// </summary>
    /// <remarks>The method interprets each consecutive group of 4 bytes in the input span as a
    /// single-precision floating-point value. The byte order and format must match the expected representation for
    /// floats on the current platform.</remarks>
    /// <param name="bytes">The read-only span of bytes to convert. The length must be a multiple of 4, as each float consists of 4 bytes.</param>
    /// <returns>An array of 32-bit floating-point values parsed from the input byte span.</returns>
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
