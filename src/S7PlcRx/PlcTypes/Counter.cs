// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between S7 Counter byte representations and <see cref="ushort"/> values.
/// </summary>
/// <remarks>The <see cref="Counter"/> class supports parsing and serializing S7 Counter values, which are
/// commonly used in Siemens S7 PLC communication protocols. All methods use big-endian byte order, with the high byte
/// first, to match the S7 Counter format. This class is thread-safe as it contains only static methods and does not
/// maintain any internal state.</remarks>
public static class Counter
{
    /// <summary>
    /// Converts a byte array to a 16-bit unsigned integer.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must contain at least two bytes representing the value in the
    /// expected byte order.</param>
    /// <returns>A 16-bit unsigned integer represented by the first two bytes of the array.</returns>
    public static ushort FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts the first two bytes of the specified read-only span to a 16-bit unsigned integer, interpreting the
    /// bytes in little-endian order.
    /// </summary>
    /// <param name="bytes">A read-only span of bytes containing at least two elements. The first two bytes are used to construct the 16-bit
    /// unsigned integer.</param>
    /// <returns>A 16-bit unsigned integer formed from the first two bytes of the span, with the first byte as the least
    /// significant and the second as the most significant.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> contains fewer than two bytes.</exception>
    public static ushort FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new ArgumentException("Bytes span must contain at least 2 bytes");
        }

        return FromBytes(bytes[1], bytes[0]);
    }

    /// <summary>
    /// Converts a sequence of bytes from the specified array, starting at the given index, to a 16-bit unsigned
    /// integer.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to convert. Cannot be null.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading bytes. Must be within the bounds of the array.</param>
    /// <returns>A 16-bit unsigned integer represented by the bytes at the specified position in the array.</returns>
    public static ushort FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Creates a 16-bit unsigned integer from two bytes, using the specified low and high byte values.
    /// </summary>
    /// <remarks>The returned value is constructed by placing <paramref name="hiVal"/> in the high-order
    /// position and <paramref name="loVal"/> in the low-order position. This is commonly used when converting from
    /// little-endian byte representations.</remarks>
    /// <param name="loVal">The low-order byte of the resulting 16-bit unsigned integer.</param>
    /// <param name="hiVal">The high-order byte of the resulting 16-bit unsigned integer.</param>
    /// <returns>A 16-bit unsigned integer composed from the specified low and high bytes.</returns>
    public static ushort FromBytes(byte loVal, byte hiVal) => (ushort)((hiVal << 8) | loVal);

    /// <summary>
    /// Converts a byte array to an array of 16-bit unsigned integers.
    /// </summary>
    /// <remarks>Each pair of bytes in the input array is interpreted as a single 16-bit unsigned integer. The
    /// conversion uses the default byte order of the platform.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of <see cref="ushort"/> values representing the converted data from the input byte array.</returns>
    public static ushort[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes to an array of 16-bit unsigned integers.
    /// </summary>
    /// <remarks>Each pair of bytes in <paramref name="bytes"/> is interpreted as a single 16-bit unsigned
    /// integer. If the length of <paramref name="bytes"/> is not a multiple of 2, any remaining bytes are
    /// ignored.</remarks>
    /// <param name="bytes">A read-only span of bytes containing the data to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of <see cref="ushort"/> values parsed from the specified byte span. The array length is half the length
    /// of <paramref name="bytes"/>.</returns>
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
    /// Converts the specified 16-bit unsigned integer to a byte array in little-endian order.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer to convert to a byte array.</param>
    /// <returns>A two-element byte array containing the little-endian representation of the specified value.</returns>
    public static byte[] ToByteArray(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified 16-bit unsigned integer value to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>This method encodes the value in big-endian format, with the most significant byte first.
    /// This is consistent with the representation used by S7 types.</remarks>
    /// <param name="value">The 16-bit unsigned integer to write to the destination span.</param>
    /// <param name="destination">The span of bytes that receives the big-endian representation of the value. Must have a length of at least 2
    /// bytes.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is less than 2 bytes in length.</exception>
    public static void ToSpan(ushort value, Span<byte> destination)
    {
        if (destination.Length < 2)
        {
            throw new ArgumentException("Destination span must be at least 2 bytes", nameof(destination));
        }

        // Counter uses big-endian format like other S7 types
        destination[0] = (byte)(value >> 8);
        destination[1] = (byte)(value & 0xFF);
    }

    /// <summary>
    /// Copies the contents of a span of 16-bit unsigned integers into a span of bytes, encoding each value as two bytes
    /// in little-endian order.
    /// </summary>
    /// <remarks>Each <see cref="ushort"/> value in <paramref name="values"/> is encoded as two bytes in
    /// little-endian format and written sequentially to <paramref name="destination"/>. The method does not allocate
    /// additional memory.</remarks>
    /// <param name="values">The read-only span of 16-bit unsigned integers to copy from.</param>
    /// <param name="destination">The span of bytes to copy the encoded values into. Must be at least twice the length of <paramref
    /// name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to hold the encoded bytes.</exception>
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
    /// Converts an array of 16-bit unsigned integers to a byte array.
    /// </summary>
    /// <param name="value">An array of <see cref="ushort"/> values to convert. Cannot be <see langword="null"/>.</param>
    /// <returns>A byte array representing the binary data of the input <see cref="ushort"/> array.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is <see langword="null"/>.</exception>
    public static byte[] ToByteArray(ushort[] value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TypeConverter.ToByteArray(value, ToByteArray);
    }
}
