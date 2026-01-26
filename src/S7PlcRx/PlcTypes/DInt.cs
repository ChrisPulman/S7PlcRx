// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between Siemens S7 DInt (32-bit signed integer) representations and .NET int
/// values.
/// </summary>
/// <remarks>This class supports conversion between S7 DInt values, which use big-endian byte order, and .NET int
/// values. Methods are provided for reading and writing single or multiple DInt values from and to byte arrays and
/// spans. All methods assume S7 DInt format and handle endianness as required. This class is intended for internal use
/// when working with S7 PLC data structures.</remarks>
internal static class DInt
{
    /// <summary>
    /// Converts a 64-bit signed integer to a 32-bit signed integer, applying a custom transformation for values greater
    /// than <see cref="int.MaxValue"/>.
    /// </summary>
    /// <remarks>If <paramref name="value"/> is greater than <see cref="int.MaxValue"/>, the method applies a
    /// specific transformation before casting to <see cref="int"/>. This is not a standard cast and may produce
    /// negative results for large input values.</remarks>
    /// <param name="value">The 64-bit signed integer value to convert.</param>
    /// <returns>A 32-bit signed integer representing the converted value. For values greater than <see cref="int.MaxValue"/>, a
    /// custom transformation is applied before conversion.</returns>
    public static int CDWord(long value)
    {
        if (value > int.MaxValue)
        {
            value -= (long)int.MaxValue + 1;
            value = (long)int.MaxValue + 1 - value;
            value *= -1;
        }

        return (int)value;
    }

    /// <summary>
    /// Creates an integer value from the specified byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert to an integer. The array must contain at least the number of
    /// bytes required to represent an integer.</param>
    /// <returns>An integer value represented by the specified byte array.</returns>
    public static int FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Creates an integer value from a byte array starting at the specified index.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading bytes.</param>
    /// <returns>The integer value represented by the bytes starting at the specified index.</returns>
    public static int FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Creates a 32-bit signed integer from the first four bytes of the specified read-only byte span, interpreting the
    /// bytes as big-endian.
    /// </summary>
    /// <remarks>This method interprets the byte order as big-endian, regardless of the system's endianness.
    /// Additional bytes in the span beyond the first four are ignored.</remarks>
    /// <param name="bytes">A read-only span of bytes containing at least four bytes to convert to a 32-bit signed integer. The first four
    /// bytes are used for the conversion.</param>
    /// <returns>A 32-bit signed integer represented by the first four bytes of the span, interpreted as big-endian.</returns>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="bytes"/> is less than 4.</exception>
    public static int FromSpan(ReadOnlySpan<byte> bytes)
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
            return MemoryMarshal.Read<int>(temp);
        }

        return MemoryMarshal.Read<int>(bytes);
    }

    /// <summary>
    /// Creates a 32-bit signed integer from four bytes, using little-endian byte order.
    /// </summary>
    /// <remarks>The bytes are combined such that v1 is the least significant byte and v4 is the most
    /// significant byte. This method is useful for reconstructing an integer from a byte array, such as when reading
    /// binary data from a stream.</remarks>
    /// <param name="v1">The least significant byte of the resulting integer.</param>
    /// <param name="v2">The second byte of the resulting integer.</param>
    /// <param name="v3">The third byte of the resulting integer.</param>
    /// <param name="v4">The most significant byte of the resulting integer.</param>
    /// <returns>A 32-bit signed integer composed from the specified bytes in little-endian order.</returns>
    public static int FromBytes(byte v1, byte v2, byte v3, byte v4) =>
        v1 | (v2 << 8) | (v3 << 16) | (v4 << 24);

    /// <summary>
    /// Converts a byte array to an array of 32-bit integers.
    /// </summary>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of 4.</param>
    /// <returns>An array of 32-bit integers representing the converted values from the input byte array.</returns>
    public static int[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes to an array of 32-bit integers.
    /// </summary>
    /// <remarks>Each group of four consecutive bytes in the input span is interpreted as a single 32-bit
    /// integer. The conversion uses the byte order expected by the FromSpan method. If the length of the span is not a
    /// multiple of 4, any remaining bytes are ignored.</remarks>
    /// <param name="bytes">The input span containing the bytes to convert. The length must be a multiple of 4.</param>
    /// <returns>An array of 32-bit integers parsed from the input byte span.</returns>
    public static int[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 4;
        var entries = bytes.Length / typeSize;
        var values = new int[entries];

        for (var i = 0; i < entries; ++i)
        {
            values[i] = FromSpan(bytes.Slice(i * typeSize, typeSize));
        }

        return values;
    }

    /// <summary>
    /// Converts the specified 32-bit signed integer to a byte array in little-endian order.
    /// </summary>
    /// <remarks>The returned array represents the integer in little-endian format, with the least significant
    /// byte at index 0. This method is useful for serialization or interoperability with systems that require
    /// byte-level representations of integers.</remarks>
    /// <param name="value">The 32-bit signed integer to convert to a byte array.</param>
    /// <returns>A 4-element byte array containing the little-endian representation of the specified integer.</returns>
    public static byte[] ToByteArray(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified 32-bit integer value to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>This method writes the integer in big-endian format, regardless of the system's native
    /// endianness. The first four bytes of the destination span will be overwritten.</remarks>
    /// <param name="value">The 32-bit integer value to write to the span.</param>
    /// <param name="destination">The span of bytes that will receive the big-endian representation of the value. Must be at least 4 bytes in
    /// length.</param>
    /// <exception cref="ArgumentException">Thrown if destination is less than 4 bytes in length.</exception>
    public static void ToSpan(int value, Span<byte> destination)
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
    /// Writes the binary representation of each 32-bit integer in the specified read-only span to the provided
    /// destination span of bytes.
    /// </summary>
    /// <param name="values">A read-only span of 32-bit integers to convert to their binary representation.</param>
    /// <param name="destination">A span of bytes that receives the binary data. Must be at least four times the length of <paramref
    /// name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="destination"/> is not large enough to contain the binary representations of all
    /// values.</exception>
    public static void ToSpan(ReadOnlySpan<int> values, Span<byte> destination)
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
    /// Converts an array of 32-bit integers to its equivalent byte array representation.
    /// </summary>
    /// <param name="value">An array of 32-bit integers to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input integer array. The length of the returned array
    /// is four times the length of the input array.</returns>
    public static byte[] ToByteArray(int[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
