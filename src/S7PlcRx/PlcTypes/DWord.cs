// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides utility methods for converting between S7 DWord (4-byte) representations and unsigned 32-bit integers
/// (uint).
/// </summary>
/// <remarks>All conversions assume S7 DWord format, which uses big-endian byte order. These methods are intended
/// for working with Siemens S7 PLC data or other protocols that represent 32-bit unsigned integers in big-endian
/// format. Methods throw exceptions if provided buffers are too small to contain a DWord value.</remarks>
internal static class DWord
{
    /// <summary>
    /// Creates a 32-bit unsigned integer from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must contain at least four bytes starting at the beginning of
    /// the array.</param>
    /// <returns>A 32-bit unsigned integer represented by the first four bytes of the array.</returns>
    public static uint FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a sequence of bytes from the specified array, starting at the given index, to a 32-bit unsigned
    /// integer.
    /// </summary>
    /// <param name="bytes">The array containing the bytes to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading bytes.</param>
    /// <returns>A 32-bit unsigned integer representing the converted value from the specified byte sequence.</returns>
    public static uint FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Creates a 32-bit unsigned integer from the first four bytes of the specified read-only byte span, interpreting
    /// the bytes as big-endian.
    /// </summary>
    /// <remarks>This method interprets the input bytes using big-endian byte order, regardless of the
    /// system's endianness. If the span contains more than four bytes, only the first four are used.</remarks>
    /// <param name="bytes">A read-only span of bytes containing at least four bytes to convert to a 32-bit unsigned integer. The first four
    /// bytes are used in the conversion.</param>
    /// <returns>A 32-bit unsigned integer represented by the first four bytes of the span, interpreted as big-endian.</returns>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="bytes"/> is less than 4.</exception>
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
    /// Creates a 32-bit unsigned integer from four individual bytes, using little-endian byte order.
    /// </summary>
    /// <remarks>The bytes are combined such that v1 is the lowest-order byte and v4 is the highest-order
    /// byte. This method is useful for reconstructing a UInt32 value from a sequence of bytes, such as when reading
    /// binary data from a stream.</remarks>
    /// <param name="v1">The least significant byte of the resulting 32-bit unsigned integer.</param>
    /// <param name="v2">The second byte, which becomes the second least significant byte of the resulting value.</param>
    /// <param name="v3">The third byte, which becomes the third least significant byte of the resulting value.</param>
    /// <param name="v4">The most significant byte of the resulting 32-bit unsigned integer.</param>
    /// <returns>A 32-bit unsigned integer composed from the specified bytes in little-endian order.</returns>
    public static uint FromBytes(byte v1, byte v2, byte v3, byte v4) =>
        (uint)(v1 | (v2 << 8) | (v3 << 16) | (v4 << 24));

    /// <summary>
    /// Converts the specified byte array to an array of 32-bit unsigned integers.
    /// </summary>
    /// <remarks>The conversion uses the default byte order of the system architecture. If the length of the
    /// input array is not a multiple of 4, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of 4.</param>
    /// <returns>An array of 32-bit unsigned integers representing the converted values from the input byte array.</returns>
    public static uint[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts the specified read-only span of bytes to an array of 32-bit unsigned integers.
    /// </summary>
    /// <remarks>Each group of four consecutive bytes in the input span is interpreted as a single 32-bit
    /// unsigned integer. The conversion uses the byte order expected by the FromSpan method. If the length of the span
    /// is not a multiple of 4, any remaining bytes are ignored.</remarks>
    /// <param name="bytes">The read-only span of bytes to convert. The length must be a multiple of 4.</param>
    /// <returns>An array of 32-bit unsigned integers representing the converted values from the input byte span.</returns>
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
    /// Converts the specified 32-bit unsigned integer to a byte array in little-endian order.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer to convert to a byte array.</param>
    /// <returns>A 4-element byte array containing the bytes of the specified value in little-endian order.</returns>
    public static byte[] ToByteArray(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified 32-bit unsigned integer value to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>This method writes the value in big-endian format, regardless of the system's native
    /// endianness. The first byte in the span will contain the most significant byte of the value.</remarks>
    /// <param name="value">The 32-bit unsigned integer value to write to the span.</param>
    /// <param name="destination">The span of bytes that receives the big-endian representation of the value. Must be at least 4 bytes in length.</param>
    /// <exception cref="ArgumentException">Thrown if the length of destination is less than 4 bytes.</exception>
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
    /// Converts each 32-bit unsigned integer in the specified read-only span to its byte representation and writes the
    /// result to the provided destination span.
    /// </summary>
    /// <param name="values">A read-only span of 32-bit unsigned integers to convert to bytes.</param>
    /// <param name="destination">A span of bytes that receives the byte representations of the input values. Must be at least four times the
    /// length of <paramref name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to contain the byte representations of all
    /// elements in <paramref name="values"/>.</exception>
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
    /// Converts the specified array of 32-bit unsigned integers to a byte array.
    /// </summary>
    /// <param name="value">An array of 32-bit unsigned integers to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input values. The length of the returned array is four
    /// times the length of the input array.</returns>
    public static byte[] ToByteArray(uint[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
