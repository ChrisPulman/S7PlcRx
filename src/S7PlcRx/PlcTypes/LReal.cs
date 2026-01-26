// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using S7PlcRx.Core;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between S7 LReal (64-bit floating point) representations and .NET double
/// values.
/// </summary>
/// <remarks>The methods in this class handle conversion between S7 LReal format (used in Siemens S7 PLCs) and
/// .NET double values, including proper handling of endianness. All methods are static and intended for internal use
/// when working with S7 protocol data. This class is not thread-safe, but all members are stateless and safe for
/// concurrent use.</remarks>
internal static class LReal
{
    /// <summary>
    /// Converts a byte array to a double-precision floating-point number.
    /// </summary>
    /// <param name="bytes">The byte array containing the binary representation of a double-precision floating-point value. Must be at least
    /// 8 bytes in length.</param>
    /// <returns>A double-precision floating-point number represented by the specified byte array.</returns>
    public static double FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a sequence of bytes from the specified array, starting at the given index, to a double-precision
    /// floating-point number.
    /// </summary>
    /// <param name="bytes">The byte array containing the value to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading the bytes.</param>
    /// <returns>A double-precision floating-point number represented by the specified bytes.</returns>
    public static double FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Converts the first 8 bytes of a read-only byte span to a double-precision floating-point value, interpreting the
    /// bytes as big-endian format.
    /// </summary>
    /// <remarks>This method interprets the input bytes as a big-endian IEEE 754 double-precision value,
    /// regardless of the system's endianness. If the span contains more than 8 bytes, only the first 8 bytes are
    /// used.</remarks>
    /// <param name="bytes">A read-only span of bytes containing at least 8 bytes representing a double-precision floating-point value in
    /// big-endian order.</param>
    /// <returns>A double-precision floating-point value represented by the first 8 bytes of the span.</returns>
    /// <exception cref="ArgumentException">Thrown when the length of <paramref name="bytes"/> is less than 8.</exception>
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
    /// Converts a 32-bit signed integer in DWord format to its equivalent double-precision floating-point value.
    /// </summary>
    /// <param name="value">The 32-bit signed integer value in DWord format to convert.</param>
    /// <returns>A double-precision floating-point value that represents the specified DWord.</returns>
    public static double FromDWord(int value) => FromByteArray(DInt.ToByteArray(value));

    /// <summary>
    /// Converts the specified 32-bit unsigned integer to its double-precision floating-point representation.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer value to convert.</param>
    /// <returns>A double-precision floating-point number that represents the specified 32-bit unsigned integer.</returns>
    public static double FromDWord(uint value) => FromByteArray(DWord.ToByteArray(value));

    /// <summary>
    /// Converts a byte array to an array of double-precision floating-point values.
    /// </summary>
    /// <remarks>The method interprets each consecutive group of 8 bytes in the input array as a
    /// double-precision floating-point value, using the system's endianness. If the length of the input array is not a
    /// multiple of 8, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of the size of a double (8 bytes).</param>
    /// <returns>An array of double values created from the input byte array.</returns>
    public static double[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes to an array of double-precision floating-point values.
    /// </summary>
    /// <remarks>The method interprets each consecutive group of 8 bytes in the span as a double-precision
    /// floating-point value. The conversion uses the system's endianness. Any remaining bytes that do not form a
    /// complete double are ignored.</remarks>
    /// <param name="bytes">A read-only span of bytes representing the binary data to convert. The length must be a multiple of 8, as each
    /// double value is represented by 8 bytes.</param>
    /// <returns>An array of double values parsed from the specified byte span. The length of the array is equal to the number of
    /// complete double values in the input.</returns>
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
    /// Converts the specified double-precision floating-point value to its equivalent 8-byte array representation.
    /// </summary>
    /// <param name="value">The double-precision floating-point number to convert.</param>
    /// <returns>A byte array containing the 8-byte binary representation of the specified value.</returns>
    public static byte[] ToByteArray(double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Writes the specified double-precision floating-point value to the provided span in big-endian byte order.
    /// </summary>
    /// <remarks>This method writes the value in big-endian format, which is commonly used in certain binary
    /// protocols such as Siemens S7. If the current platform is little-endian, the bytes are reversed to ensure correct
    /// ordering.</remarks>
    /// <param name="value">The double-precision floating-point value to write to the span.</param>
    /// <param name="destination">The span of bytes that will receive the 8-byte big-endian representation of the value. Must be at least 8 bytes
    /// in length.</param>
    /// <exception cref="ArgumentException">Thrown if the length of destination is less than 8 bytes.</exception>
    public static void ToSpan(double value, Span<byte> destination)
    {
        if (destination.Length < 8)
        {
            throw new ArgumentException("Destination span must be at least 8 bytes", nameof(destination));
        }

#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.
        MemoryMarshal.Write(destination, ref value);
#pragma warning restore CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

        // S7 uses big-endian, so reverse if we're on little-endian platform
        if (BitConverter.IsLittleEndian)
        {
            destination.Slice(0, 8).Reverse();
        }
    }

    /// <summary>
    /// Writes each double-precision floating-point value from the specified read-only span to the specified destination
    /// span as a sequence of bytes.
    /// </summary>
    /// <param name="values">The read-only span of double values to convert to their byte representations.</param>
    /// <param name="destination">The span of bytes that receives the byte representations of the values. Must be at least values.Length × 8 bytes
    /// in length.</param>
    /// <exception cref="ArgumentException">Thrown when the length of destination is less than values.Length × 8 bytes.</exception>
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
    /// Converts an array of double-precision floating-point numbers to a byte array representation.
    /// </summary>
    /// <param name="value">The array of double values to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input double array. The array will be empty if the
    /// input array is empty.</returns>
    public static byte[] ToByteArray(double[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
