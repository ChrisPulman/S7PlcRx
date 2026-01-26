// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides utility methods for converting between 16-bit unsigned integers (words) and their byte array or span
/// representations, using big-endian (high byte first) byte order.
/// </summary>
/// <remarks>All methods in this class assume that words are represented in big-endian format, where the first
/// byte is the high-order byte and the second byte is the low-order byte. These methods are intended for scenarios
/// where explicit control over byte order is required, such as binary serialization, communication protocols, or file
/// I/O. The class is static and cannot be instantiated.</remarks>
internal static class Word
{
    /// <summary>
    /// Creates a 16-bit unsigned integer from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must contain at least two elements.</param>
    /// <returns>A 16-bit unsigned integer represented by the first two bytes of the array.</returns>
    public static ushort FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Creates a 16-bit unsigned integer from a span containing two bytes in little-endian order.
    /// </summary>
    /// <remarks>This method interprets the first two bytes of the span as a little-endian encoded unsigned
    /// 16-bit integer. The caller must ensure that the span contains at least two bytes to avoid an
    /// exception.</remarks>
    /// <param name="bytes">A read-only span of bytes that provides the two bytes to convert. The span must have a length of at least 2,
    /// with the least significant byte at index 0 and the most significant byte at index 1.</param>
    /// <returns>A 16-bit unsigned integer represented by the two bytes in the specified span.</returns>
    public static ushort FromSpan(ReadOnlySpan<byte> bytes) => FromBytes(bytes[1], bytes[0]);

    /// <summary>
    /// Creates a 16-bit unsigned integer from a byte array starting at the specified index.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading the value.</param>
    /// <returns>A 16-bit unsigned integer formed from the specified bytes.</returns>
    public static ushort FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Creates a 16-bit unsigned integer from two bytes, using the specified low and high byte values.
    /// </summary>
    /// <remarks>The resulting value is calculated as (hiVal * 256) + loVal, with loVal as the least
    /// significant byte and hiVal as the most significant byte. This method assumes a little-endian byte
    /// order.</remarks>
    /// <param name="loVal">The low-order byte of the resulting 16-bit unsigned integer.</param>
    /// <param name="hiVal">The high-order byte of the resulting 16-bit unsigned integer.</param>
    /// <returns>A 16-bit unsigned integer composed from the specified low and high bytes.</returns>
    public static ushort FromBytes(byte loVal, byte hiVal) => (ushort)((hiVal * 256) + loVal);

    /// <summary>
    /// Converts a byte array to an array of 16-bit unsigned integers.
    /// </summary>
    /// <remarks>The conversion interprets each pair of bytes in the input array as a single 16-bit unsigned
    /// integer. If the length of the input array is not a multiple of 2, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of 16-bit unsigned integers representing the converted values from the input byte array.</returns>
    public static ushort[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// Converts a read-only span of bytes to an array of 16-bit unsigned integers.
    /// </summary>
    /// <remarks>Each pair of bytes in the input span is interpreted as a single 16-bit unsigned integer. The
    /// conversion uses the byte order expected by the FromSpan method. If the length of the input span is not a
    /// multiple of 2, any remaining bytes are ignored.</remarks>
    /// <param name="bytes">The input span containing the bytes to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of 16-bit unsigned integers parsed from the input bytes. The length of the array is half the length of
    /// the input span.</returns>
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
    /// Converts the specified 16-bit unsigned integer to a byte array.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer to convert.</param>
    /// <returns>A byte array containing the bytes of the specified value in little-endian order.</returns>
    public static byte[] ToByteArray(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified 16-bit unsigned integer to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>The method writes the most significant byte of value to destination[0] and the least
    /// significant byte to destination[1].</remarks>
    /// <param name="value">The 16-bit unsigned integer value to write to the span.</param>
    /// <param name="destination">The span of bytes that receives the big-endian representation of the value. Must be at least 2 bytes in length.</param>
    /// <exception cref="ArgumentException">Thrown if destination is less than 2 bytes in length.</exception>
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
    /// Copies the byte representation of the specified 16-bit unsigned integer into the given array starting at the
    /// specified index.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer to convert to bytes.</param>
    /// <param name="destination">The array that will receive the bytes representing the value. Must have sufficient space to accommodate two
    /// bytes starting at the specified index.</param>
    /// <param name="start">The zero-based index in the destination array at which to begin copying the bytes.</param>
    public static void ToByteArray(ushort value, Array destination, int start)
    {
        var bytes = ToByteArray(value);
        Array.Copy(bytes, 0, destination, start, 2);
    }

    /// <summary>
    /// Converts an array of 16-bit unsigned integers to a byte array.
    /// </summary>
    /// <param name="value">The array of 16-bit unsigned integers to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input values.</returns>
    public static byte[] ToByteArray(ushort[] value) => TypeConverter.ToByteArray(value, ToByteArray);

    /// <summary>
    /// Converts a sequence of 16-bit unsigned integers to their byte representation and writes the result to the
    /// specified destination span.
    /// </summary>
    /// <param name="values">The sequence of 16-bit unsigned integers to convert.</param>
    /// <param name="destination">The span of bytes that receives the converted values. Must be at least twice the length of <paramref
    /// name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to contain the converted bytes.</exception>
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
