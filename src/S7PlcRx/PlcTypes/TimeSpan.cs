// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert between <see cref="T:System.TimeSpan"/> and S7 representation of TIME values.
/// </summary>
public static class TimeSpan
{
    /// <summary>
    /// The type length in bytes.
    /// </summary>
    public const int TypeLengthInBytes = 4;

    /// <summary>
    /// The minimum <see cref="T:System.TimeSpan"/> value supported by the specification.
    /// </summary>
    public static readonly System.TimeSpan SpecMinimumTimeSpan = System.TimeSpan.FromMilliseconds(int.MinValue);

    /// <summary>
    /// The maximum <see cref="T:System.TimeSpan"/> value supported by the specification.
    /// </summary>
    public static readonly System.TimeSpan SpecMaximumTimeSpan = System.TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>
    /// Parses a <see cref="T:System.TimeSpan"/> value from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A <see cref="T:System.TimeSpan"/> object representing the value read from PLC.</returns>
    public static System.TimeSpan FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Parses a <see cref="T:System.TimeSpan"/> value from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>A <see cref="T:System.TimeSpan"/> object representing the value read from PLC.</returns>
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
    /// Parses an array of <see cref="T:System.TimeSpan"/> values from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="T:System.TimeSpan"/> objects representing the values read from PLC.</returns>
    public static System.TimeSpan[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Parses an array of <see cref="T:System.TimeSpan"/> values from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>An array of <see cref="T:System.TimeSpan"/> objects representing the values read from PLC.</returns>
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
    /// Converts a <see cref="T:System.TimeSpan"/> value to a byte array.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan value to convert.</param>
    /// <returns>A byte array containing the S7 time representation of <paramref name="timeSpan"/>.</returns>
    public static byte[] ToByteArray(System.TimeSpan timeSpan)
    {
        Span<byte> bytes = stackalloc byte[TypeLengthInBytes];
        ToSpan(timeSpan, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Converts a <see cref="T:System.TimeSpan"/> value to a span.
    /// </summary>
    /// <param name="timeSpan">The TimeSpan value to convert.</param>
    /// <param name="destination">The destination span.</param>
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
    /// Converts an array of <see cref="T:System.TimeSpan"/> values to a byte array.
    /// </summary>
    /// <param name="timeSpans">The TimeSpan values to convert.</param>
    /// <returns>A byte array containing the S7 time representations of <paramref name="timeSpans"/>.</returns>
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
    /// Converts multiple TimeSpan values to the specified span.
    /// </summary>
    /// <param name="timeSpans">The TimeSpan values.</param>
    /// <param name="destination">The destination span.</param>
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
