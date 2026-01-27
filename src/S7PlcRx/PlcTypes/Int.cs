// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between S7 Int (16-bit signed integer) representations and .NET types,
/// including byte arrays and spans.
/// </summary>
/// <remarks>This class is intended for working with Siemens S7 PLC data formats, which use big-endian byte order
/// for 16-bit signed integers. All methods assume S7 Int format unless otherwise specified. The class is internal and
/// not intended for direct use outside of the containing assembly.</remarks>
internal static class Int
{
    /// <summary>
    /// Converts a 32-bit signed integer to a 16-bit signed integer, applying a custom transformation for values greater
    /// than 32,767.
    /// </summary>
    /// <remarks>If the input value is greater than 32,767, a specific transformation is applied before
    /// conversion. This method does not throw an exception for values outside the range of a 16-bit signed integer;
    /// instead, it applies the custom logic to produce a result within the range.</remarks>
    /// <param name="value">The 32-bit signed integer to convert.</param>
    /// <returns>A 16-bit signed integer representing the converted value.</returns>
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
    /// Converts a byte array to a 16-bit signed integer.
    /// </summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must contain at least two bytes starting at index zero.</param>
    /// <returns>A 16-bit signed integer represented by the first two bytes of the array.</returns>
    public static short FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a sequence of bytes from the specified array, starting at the given index, to a 16-bit signed integer.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading the bytes.</param>
    /// <returns>A 16-bit signed integer represented by the two bytes starting at the specified index in the array.</returns>
    public static short FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Creates a 16-bit signed integer from the first two bytes of the specified read-only byte span, interpreting the
    /// bytes as big-endian.
    /// </summary>
    /// <remarks>This method interprets the input bytes using big-endian byte order, regardless of the
    /// system's endianness. This is commonly used for protocols or file formats that specify big-endian
    /// encoding.</remarks>
    /// <param name="bytes">A read-only span of bytes containing at least two bytes. The first two bytes are used to construct the 16-bit
    /// signed integer.</param>
    /// <returns>A 16-bit signed integer represented by the first two bytes of the span, interpreted as big-endian.</returns>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="bytes"/> is less than 2.</exception>
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
    /// Creates a 16-bit signed integer from two bytes, using the specified low and high byte values.
    /// </summary>
    /// <remarks>The bytes are combined in little-endian order, with loVal as the least significant byte and
    /// hiVal as the most significant byte.</remarks>
    /// <param name="loVal">The low-order byte of the 16-bit value.</param>
    /// <param name="hiVal">The high-order byte of the 16-bit value.</param>
    /// <returns>A 16-bit signed integer formed by combining the specified low and high bytes.</returns>
    public static short FromBytes(byte loVal, byte hiVal) => (short)((hiVal << 8) | loVal);

    /// <summary>
    /// Converts the specified byte array to an array of 16-bit signed integers.
    /// </summary>
    /// <remarks>The conversion interprets each consecutive pair of bytes as a single 16-bit signed integer.
    /// The byte order used for conversion is platform-dependent. If the length of the input array is not a multiple of
    /// 2, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of 16-bit signed integers representing the converted values from the input byte array.</returns>
    public static short[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes to an array of 16-bit signed integers.
    /// </summary>
    /// <remarks>Each pair of bytes in the input span is interpreted as a single 16-bit signed integer. The
    /// conversion uses the byte order expected by the FromSpan method. If the length of the span is not a multiple of
    /// 2, any remaining bytes are ignored.</remarks>
    /// <param name="bytes">The read-only span of bytes to convert. The length must be a multiple of 2.</param>
    /// <returns>An array of 16-bit signed integers representing the converted values from the input span.</returns>
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
    /// Converts the specified 16-bit signed integer to a byte array.
    /// </summary>
    /// <param name="value">The 16-bit signed integer to convert.</param>
    /// <returns>A byte array containing the two bytes that represent the specified value.</returns>
    public static byte[] ToByteArray(short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified 16-bit signed integer value to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>This method writes the value in big-endian format, regardless of the system's native
    /// endianness. The caller is responsible for ensuring that the destination span has sufficient space.</remarks>
    /// <param name="value">The 16-bit signed integer value to write to the span.</param>
    /// <param name="destination">The span of bytes that receives the big-endian representation of the value. Must be at least 2 bytes in length.</param>
    /// <exception cref="ArgumentException">Thrown if the length of destination is less than 2 bytes.</exception>
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
    /// Writes the contents of a span of 16-bit signed integers to a span of bytes in little-endian order.
    /// </summary>
    /// <param name="values">The source span containing the 16-bit signed integer values to write.</param>
    /// <param name="destination">The destination span where the bytes will be written. Must be at least twice the length of <paramref
    /// name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="destination"/> is not large enough to contain the converted bytes.</exception>
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
    /// Converts an array of 16-bit signed integers to a byte array.
    /// </summary>
    /// <param name="value">An array of 16-bit signed integers to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input values.</returns>
    public static byte[] ToByteArray(short[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
