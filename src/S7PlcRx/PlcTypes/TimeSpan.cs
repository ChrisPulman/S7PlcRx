// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides methods and constants for converting between S7 PLC time representations and .NET <see
/// cref="T:System.TimeSpan"/> values.
/// </summary>
/// <remarks>This class supports parsing and serializing <see cref="T:System.TimeSpan"/> values to and from the S7
/// PLC binary format, where time spans are represented as 4-byte signed integers in milliseconds. All methods assume
/// the S7 time format and enforce the valid range defined by <see cref="F:SpecMinimumTimeSpan"/> and <see
/// cref="F:SpecMaximumTimeSpan"/>. The class is static and cannot be instantiated.</remarks>
public static class TimeSpan
{
    /// <summary>
    /// Represents the size, in bytes, of the type.
    /// </summary>
    public const int TypeLengthInBytes = 4;

    /// <summary>
    /// Represents the minimum allowable value for a specification time span, defined as the number of milliseconds
    /// equal to <see cref="int.MinValue"/>.
    /// </summary>
    public static readonly System.TimeSpan SpecMinimumTimeSpan = System.TimeSpan.FromMilliseconds(int.MinValue);

    /// <summary>
    /// Represents the maximum allowable time span for specification purposes, set to the largest value expressible in
    /// milliseconds as an integer.
    /// </summary>
    /// <remarks>This value is useful when an upper bound for a time interval is required, such as in timeout
    /// or delay scenarios where the maximum supported duration is needed. The value is equivalent to
    /// TimeSpan.FromMilliseconds(int.MaxValue).</remarks>
    public static readonly System.TimeSpan SpecMaximumTimeSpan = System.TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Creates a TimeSpan structure from its binary representation in a byte array.
    /// </summary>
    /// <remarks>The byte array must contain a valid binary representation of a TimeSpan as produced by the
    /// corresponding serialization method. Supplying an array that is too short or incorrectly formatted may result in
    /// an exception.</remarks>
    /// <param name="bytes">A byte array containing the binary representation of a TimeSpan. The array must be at least 8 bytes in length
    /// and encoded in the expected format.</param>
    /// <returns>A TimeSpan value represented by the specified byte array.</returns>
    public static System.TimeSpan FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Creates a TimeSpan from a read-only span of bytes representing a 32-bit integer value in milliseconds.
    /// </summary>
    /// <param name="bytes">A read-only span of bytes containing the 32-bit integer value, in little-endian format, representing the number
    /// of milliseconds for the TimeSpan. Must be at least 4 bytes in length.</param>
    /// <returns>A TimeSpan that represents the time interval specified by the 32-bit integer value, in milliseconds, contained
    /// in the input span.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the length of bytes is less than 4.</exception>
    public static System.TimeSpan FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a TimeSpan requires exactly 4 bytes of input data, input data is '{bytes.Length}' long.");
        }

        var milliseconds = DInt.FromSpan(bytes);
        return System.TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// Converts a byte array to an array of <see cref="System.TimeSpan"/> values.
    /// </summary>
    /// <remarks>The method interprets the input byte array as a sequence of <see cref="System.TimeSpan"/>
    /// values in their binary format. The caller is responsible for ensuring that the byte array was created using a
    /// compatible serialization method and that its length is valid.</remarks>
    /// <param name="bytes">The byte array containing the binary representation of one or more <see cref="System.TimeSpan"/> values. The
    /// array length must be a multiple of the size of a <see cref="System.TimeSpan"/> structure.</param>
    /// <returns>An array of <see cref="System.TimeSpan"/> values deserialized from the specified byte array.</returns>
    public static System.TimeSpan[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Converts a read-only span of bytes into an array of TimeSpan values, interpreting each group of bytes as a
    /// duration in milliseconds.
    /// </summary>
    /// <remarks>Each consecutive group of 8 bytes in the input is interpreted as a 64-bit signed integer in
    /// the platform's endianness, representing a duration in milliseconds. The method does not perform validation on
    /// the range of the resulting TimeSpan values.</remarks>
    /// <param name="bytes">A read-only span of bytes representing one or more 64-bit signed integer values, each corresponding to a
    /// duration in milliseconds.</param>
    /// <returns>An array of TimeSpan values created from the input bytes. Each element represents a duration corresponding to
    /// one 64-bit integer value in the input.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the length of bytes is not a multiple of the size of a 64-bit signed integer.</exception>
    public static System.TimeSpan[] ToArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % TypeLengthInBytes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing an array of TimeSpan requires a multiple of {TypeLengthInBytes} bytes of input data, input data is '{bytes.Length}' long.");
        }

        var result = new System.TimeSpan[bytes.Length / TypeLengthInBytes];
        var milliseconds = DInt.ToArray(bytes);

        for (var i = 0; i < milliseconds.Length; i++)
        {
            result[i] = System.TimeSpan.FromMilliseconds(milliseconds[i]);
        }

        return result;
    }

    /// <summary>
    /// Converts the specified <see cref="System.TimeSpan"/> value to its binary representation as a byte array.
    /// </summary>
    /// <param name="timeSpan">The <see cref="System.TimeSpan"/> value to convert to a byte array.</param>
    /// <returns>A byte array containing the binary representation of the specified <see cref="System.TimeSpan"/> value.</returns>
    public static byte[] ToByteArray(System.TimeSpan timeSpan)
    {
        Span<byte> bytes = stackalloc byte[TypeLengthInBytes];
        ToSpan(timeSpan, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Encodes the specified <see cref="System.TimeSpan"/> value into its S7 time representation and writes the result
    /// to the provided byte span.
    /// </summary>
    /// <remarks>The S7 time representation encodes a time interval as a 4-byte value in milliseconds. Only
    /// time spans within the supported S7 range can be encoded.</remarks>
    /// <param name="timeSpan">The time interval to encode. Must be within the supported S7 time range.</param>
    /// <param name="destination">The span of bytes to which the encoded S7 time value will be written. Must be at least 4 bytes in length.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="destination"/> is less than 4 bytes in length.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="timeSpan"/> is less than the minimum or greater than the maximum value supported by
    /// the S7 time representation.</exception>
    public static void ToSpan(System.TimeSpan timeSpan, Span<byte> destination)
    {
        if (destination.Length < TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span must be at least 4 bytes", nameof(destination));
        }

        if (timeSpan < SpecMinimumTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSpan), timeSpan, $"Time span '{timeSpan}' is before the minimum '{SpecMinimumTimeSpan}' supported in S7 time representation.");
        }

        if (timeSpan > SpecMaximumTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(timeSpan), timeSpan, $"Time span '{timeSpan}' is after the maximum '{SpecMaximumTimeSpan}' supported in S7 time representation.");
        }

        var milliseconds = (int)timeSpan.TotalMilliseconds;
        DInt.ToSpan(milliseconds, destination);
    }

    /// <summary>
    /// Converts an array of <see cref="System.TimeSpan"/> values to a byte array representation.
    /// </summary>
    /// <param name="timeSpans">An array of <see cref="System.TimeSpan"/> values to convert. Cannot be null.</param>
    /// <returns>A byte array containing the serialized representation of the input <see cref="System.TimeSpan"/> values. The
    /// length of the array is proportional to the number of elements in <paramref name="timeSpans"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="timeSpans"/> is null.</exception>
    public static byte[] ToByteArray(System.TimeSpan[] timeSpans)
    {
        if (timeSpans == null)
        {
            throw new ArgumentNullException(nameof(timeSpans));
        }

        // Use ArrayPool for large allocations
        var totalBytes = timeSpans.Length * TypeLengthInBytes;
        byte[]? pooledArray = null;
        var buffer = totalBytes > 1024
            ? pooledArray = ArrayPool<byte>.Shared.Rent(totalBytes)
            : new byte[totalBytes];

        try
        {
            var span = buffer.AsSpan(0, totalBytes);
            for (var i = 0; i < timeSpans.Length; i++)
            {
                ToSpan(timeSpans[i], span.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
            }

            return span.ToArray();
        }
        finally
        {
            if (pooledArray != null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    /// <summary>
    /// Converts a sequence of <see cref="System.TimeSpan"/> values to their binary representation and writes the result
    /// to the specified destination span.
    /// </summary>
    /// <param name="timeSpans">The read-only span containing the <see cref="System.TimeSpan"/> values to convert.</param>
    /// <param name="destination">The span of bytes that receives the binary representation of the <paramref name="timeSpans"/> values. Must be
    /// large enough to hold all converted values.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to contain the binary representation of all
    /// <paramref name="timeSpans"/> values.</exception>
    public static void ToSpan(ReadOnlySpan<System.TimeSpan> timeSpans, Span<byte> destination)
    {
        if (destination.Length < timeSpans.Length * TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < timeSpans.Length; i++)
        {
            ToSpan(timeSpans[i], destination.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }
    }
}
