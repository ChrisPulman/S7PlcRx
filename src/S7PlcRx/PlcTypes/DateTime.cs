// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert between <see cref="T:System.DateTime"/> and S7 representation of datetime values.
/// </summary>
public static class DateTime
{
    /// <summary>
    /// The minimum <see cref="T:System.DateTime"/> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMinimumDateTime = new(1990, 1, 1);

    /// <summary>
    /// The maximum <see cref="T:System.DateTime"/> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMaximumDateTime = new(2089, 12, 31, 23, 59, 59, 999);

    /// <summary>
    /// Parses a <see cref="T:System.DateTime"/> value from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime"/> object representing the value read from PLC.</returns>
    public static System.DateTime FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Parses a <see cref="T:System.DateTime"/> value from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime"/> object representing the value read from PLC.</returns>
    public static System.DateTime FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTime requires exactly 8 bytes of input data, input data is '{bytes.Length}' long.");
        }

        return FromSpanImpl(bytes);
    }

    /// <summary>
    /// Parses an array of <see cref="T:System.DateTime"/> values from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime"/> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Parses an array of <see cref="T:System.DateTime"/> values from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime"/> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 8 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing an array of DateTime requires a multiple of 8 bytes of input data, input data is '{bytes.Length}' long.");
        }

        var cnt = bytes.Length / 8;
        var result = new System.DateTime[cnt];

        for (var i = 0; i < cnt; i++)
        {
            result[i] = FromSpanImpl(bytes.Slice(i * 8, 8));
        }

        return result;
    }

    /// <summary>
    /// Converts a <see cref="T:System.DateTime"/> value to a byte array.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>A byte array containing the S7 date time representation of <paramref name="dateTime"/>.</returns>
    public static byte[] ToByteArray(System.DateTime dateTime)
    {
        Span<byte> bytes = stackalloc byte[8];
        ToSpan(dateTime, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Converts a <see cref="T:System.DateTime"/> value to a span.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(System.DateTime dateTime, Span<byte> destination)
    {
        if (destination.Length < 8)
        {
            throw new ArgumentException("Destination span must be at least 8 bytes", nameof(destination));
        }

        if (dateTime < SpecMinimumDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is before the minimum '{SpecMinimumDateTime}' supported in S7 date time representation.");
        }

        if (dateTime > SpecMaximumDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is after the maximum '{SpecMaximumDateTime}' supported in S7 date time representation.");
        }

        static byte EncodeBcd(int value) => (byte)(((value / 10) << 4) | (value % 10));
        static byte MapYear(int year) => (byte)(year < 2000 ? year - 1900 : year - 2000);
        static int DayOfWeekToInt(DayOfWeek dayOfWeek) => (int)dayOfWeek + 1;

        destination[0] = EncodeBcd(MapYear(dateTime.Year));
        destination[1] = EncodeBcd(dateTime.Month);
        destination[2] = EncodeBcd(dateTime.Day);
        destination[3] = EncodeBcd(dateTime.Hour);
        destination[4] = EncodeBcd(dateTime.Minute);
        destination[5] = EncodeBcd(dateTime.Second);
        destination[6] = EncodeBcd(dateTime.Millisecond / 10);
        destination[7] = (byte)(((dateTime.Millisecond % 10) << 4) | DayOfWeekToInt(dateTime.DayOfWeek));
    }

    /// <summary>
    /// Converts an array of <see cref="T:System.DateTime"/> values to a byte array.
    /// </summary>
    /// <param name="dateTimes">The DateTime values to convert.</param>
    /// <returns>A byte array containing the S7 date time representations of <paramref name="dateTimes"/>.</returns>
    public static byte[] ToByteArray(System.DateTime[] dateTimes)
    {
        if (dateTimes?.Any(dateTime => dateTime < SpecMinimumDateTime || dateTime > SpecMaximumDateTime) != false)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTimes), dateTimes, $"At least one date time value is before the minimum '{SpecMinimumDateTime}' or after the maximum '{SpecMaximumDateTime}' supported in S7 date time representation.");
        }

        // Use ArrayPool for large allocations
        var totalBytes = dateTimes.Length * 8;
        byte[]? pooledArray = null;
        var buffer = totalBytes > 1024
            ? pooledArray = ArrayPool<byte>.Shared.Rent(totalBytes)
            : new byte[totalBytes];

        try
        {
            var span = buffer.AsSpan(0, totalBytes);
            for (var i = 0; i < dateTimes.Length; i++)
            {
                ToSpan(dateTimes[i], span.Slice(i * 8, 8));
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
    /// Converts multiple DateTime values to the specified span.
    /// </summary>
    /// <param name="dateTimes">The DateTime values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<System.DateTime> dateTimes, Span<byte> destination)
    {
        if (destination.Length < dateTimes.Length * 8)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < dateTimes.Length; i++)
        {
            ToSpan(dateTimes[i], destination.Slice(i * 8, 8));
        }
    }

    private static System.DateTime FromSpanImpl(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTime requires exactly 8 bytes of input data, input data is {bytes.Length} bytes long.");
        }

        static int DecodeBcd(byte input) => (10 * (input >> 4)) + (input & 0b00001111);

        static int ByteToYear(byte bcdYear)
        {
            var input = DecodeBcd(bcdYear);
            if (input < 90)
            {
                return input + 2000;
            }

            if (input < 100)
            {
                return input + 1900;
            }

            throw new ArgumentOutOfRangeException(nameof(bcdYear), bcdYear, $"Value '{input}' is higher than the maximum '99' of S7 date and time representation.");
        }

        static int AssertRangeInclusive(int input, byte min, byte max, string field)
        {
            if (input < min)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
            }

            if (input > max)
            {
                throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");
            }

            return input;
        }

        var year = ByteToYear(bytes[0]);
        var month = AssertRangeInclusive(DecodeBcd(bytes[1]), 1, 12, "month");
        var day = AssertRangeInclusive(DecodeBcd(bytes[2]), 1, 31, "day of month");
        var hour = AssertRangeInclusive(DecodeBcd(bytes[3]), 0, 23, "hour");
        var minute = AssertRangeInclusive(DecodeBcd(bytes[4]), 0, 59, "minute");
        var second = AssertRangeInclusive(DecodeBcd(bytes[5]), 0, 59, "second");
        var hsec = AssertRangeInclusive(DecodeBcd(bytes[6]), 0, 99, "first two millisecond digits");
        var msec = AssertRangeInclusive(bytes[7] >> 4, 0, 9, "third millisecond digit");
        ////var dayOfWeek = AssertRangeInclusive(bytes[7] & 0b00001111, 1, 7, "day of week");

        return new System.DateTime(year, month, day, hour, minute, second, (hsec * 10) + msec);
    }
}
