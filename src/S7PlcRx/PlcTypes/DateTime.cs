// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>Contains the methods to convert between <see cref="T:System.DateTime"/> and S7 representation of datetime values.</summary>
public static class DateTime
{
    /// <summary>The minimum <see cref="T:System.DateTime"/> value supported by the specification.</summary>
    public static readonly System.DateTime SpecMinimumDateTime = new(1990, 1, 1, 0, 0, 0, System.DateTimeKind.Unspecified);

    /// <summary>The maximum <see cref="T:System.DateTime"/> value supported by the specification.</summary>
    public static readonly System.DateTime SpecMaximumDateTime = new(2089, 12, 31, 23, 59, 59, 999, System.DateTimeKind.Unspecified);

    /// <summary>The serialized S7 date-time width in bytes.</summary>
    private const int TypeLengthInBytes = 8;

    /// <summary>The allocation size above which array pooling is used.</summary>
    private const int ArrayPoolThresholdInBytes = 1024;

    /// <summary>The radix used for binary-coded decimal digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The shift separating the two binary-coded decimal nibbles.</summary>
    private const int BcdNibbleShift = 4;

    /// <summary>The century base used for encoded years from 90 through 99.</summary>
    private const int PreviousCenturyBaseYear = 1900;

    /// <summary>The century base used for encoded years from 00 through 89.</summary>
    private const int CurrentCenturyBaseYear = 2000;

    /// <summary>The encoded year at which mapping switches to the previous century.</summary>
    private const int CurrentCenturyThreshold = 90;

    /// <summary>The number of years in one century.</summary>
    private const int YearsPerCentury = 100;

    /// <summary>The byte offset containing the month.</summary>
    private const int MonthOffset = 1;

    /// <summary>The byte offset containing the day.</summary>
    private const int DayOffset = 2;

    /// <summary>The byte offset containing the hour.</summary>
    private const int HourOffset = 3;

    /// <summary>The byte offset containing the minute.</summary>
    private const int MinuteOffset = 4;

    /// <summary>The byte offset containing the second.</summary>
    private const int SecondOffset = 5;

    /// <summary>The byte offset containing the first two millisecond digits.</summary>
    private const int HundredthMillisecondOffset = 6;

    /// <summary>The byte offset containing the final millisecond digit and weekday.</summary>
    private const int MillisecondAndWeekdayOffset = 7;

    /// <summary>The largest valid month value.</summary>
    private const byte MaximumMonth = 12;

    /// <summary>The largest valid day value in the wire field.</summary>
    private const byte MaximumDay = 31;

    /// <summary>The largest valid hour value.</summary>
    private const byte MaximumHour = 23;

    /// <summary>The largest valid minute or second value.</summary>
    private const byte MaximumMinuteOrSecond = 59;

    /// <summary>The largest value represented by two decimal digits.</summary>
    private const byte MaximumHundredths = 99;

    /// <summary>The largest value represented by one decimal digit.</summary>
    private const byte MaximumSingleDigit = 9;

    /// <summary>Parses a <see cref="T:System.DateTime"/> value from bytes.</summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime"/> object representing the value read from PLC.</returns>
    public static System.DateTime FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>Parses a <see cref="T:System.DateTime"/> value from a span.</summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime"/> object representing the value read from PLC.</returns>
    public static System.DateTime FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTime requires exactly 8 bytes of input data, input data is '{bytes.Length}' long.");
        }

        return FromSpanImpl(bytes);
    }

    /// <summary>Parses an array of <see cref="T:System.DateTime"/> values from bytes.</summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime"/> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>Parses an array of <see cref="T:System.DateTime"/> values from a span.</summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime"/> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % TypeLengthInBytes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing an array of DateTime requires a multiple of 8 bytes of input data, input data is '{bytes.Length}' long.");
        }

        var cnt = bytes.Length / TypeLengthInBytes;
        var result = new System.DateTime[cnt];

        for (var i = 0; i < cnt; i++)
        {
            result[i] = FromSpanImpl(bytes.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }

        return result;
    }

    /// <summary>Converts a <see cref="T:System.DateTime"/> value to a byte array.</summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>A byte array containing the S7 date time representation of <paramref name="dateTime"/>.</returns>
    public static byte[] ToByteArray(System.DateTime dateTime)
    {
        Span<byte> bytes = stackalloc byte[TypeLengthInBytes];
        ToSpan(dateTime, bytes);
        return bytes.ToArray();
    }

    /// <summary>Converts a <see cref="T:System.DateTime"/> value to a span.</summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(System.DateTime dateTime, Span<byte> destination)
    {
        if (destination.Length < TypeLengthInBytes)
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

        static byte EncodeBcd(int value) => (byte)(((value / DecimalRadix) << BcdNibbleShift) | (value % DecimalRadix));
        static byte MapYear(int year) => (byte)(year < CurrentCenturyBaseYear ? year - PreviousCenturyBaseYear : year - CurrentCenturyBaseYear);
        static int DayOfWeekToInt(DayOfWeek dayOfWeek) => (int)dayOfWeek + 1;

        destination[0] = EncodeBcd(MapYear(dateTime.Year));
        destination[1] = EncodeBcd(dateTime.Month);
        destination[DayOffset] = EncodeBcd(dateTime.Day);
        destination[HourOffset] = EncodeBcd(dateTime.Hour);
        destination[MinuteOffset] = EncodeBcd(dateTime.Minute);
        destination[SecondOffset] = EncodeBcd(dateTime.Second);
        destination[HundredthMillisecondOffset] = EncodeBcd(dateTime.Millisecond / DecimalRadix);
        destination[MillisecondAndWeekdayOffset] = (byte)(((dateTime.Millisecond % DecimalRadix) << BcdNibbleShift) | DayOfWeekToInt(dateTime.DayOfWeek));
    }

    /// <summary>Converts an array of <see cref="T:System.DateTime"/> values to a byte array.</summary>
    /// <param name="dateTimes">The DateTime values to convert.</param>
    /// <returns>A byte array containing the S7 date time representations of <paramref name="dateTimes"/>.</returns>
    public static byte[] ToByteArray(System.DateTime[] dateTimes)
    {
        if (dateTimes?.Any(dateTime => dateTime < SpecMinimumDateTime || dateTime > SpecMaximumDateTime) != false)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTimes), dateTimes, $"At least one date time value is before the minimum '{SpecMinimumDateTime}' or after the maximum '{SpecMaximumDateTime}' supported in S7 date time representation.");
        }

        // Use ArrayPool for large allocations
        var totalBytes = dateTimes.Length * TypeLengthInBytes;
        byte[]? pooledArray = null;
        var buffer = totalBytes > ArrayPoolThresholdInBytes
            ? pooledArray = ArrayPool<byte>.Shared.Rent(totalBytes)
            : new byte[totalBytes];

        try
        {
            var span = buffer.AsSpan(0, totalBytes);
            for (var i = 0; i < dateTimes.Length; i++)
            {
                ToSpan(dateTimes[i], span.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
            }

            return span.ToArray();
        }
        finally
        {
            if (pooledArray is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    /// <summary>Converts multiple DateTime values to the specified span.</summary>
    /// <param name="dateTimes">The DateTime values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<System.DateTime> dateTimes, Span<byte> destination)
    {
        if (destination.Length < dateTimes.Length * TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < dateTimes.Length; i++)
        {
            ToSpan(dateTimes[i], destination.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }
    }

    /// <summary>Stores the f ro ms pa ni m p l value.</summary>
    /// <param name="bytes">The b yt e s value.</param>
    /// <returns>The resulting value.</returns>
    private static System.DateTime FromSpanImpl(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTime requires exactly 8 bytes of input data, input data is {bytes.Length} bytes long.");
        }

        static int DecodeBcd(byte input) => (DecimalRadix * (input >> BcdNibbleShift)) + (input & 0b00001111);

        static int ByteToYear(byte bcdYear)
        {
            var input = DecodeBcd(bcdYear);
            if (input < CurrentCenturyThreshold)
            {
                return input + CurrentCenturyBaseYear;
            }

            if (input < YearsPerCentury)
            {
                return input + PreviousCenturyBaseYear;
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
        var month = AssertRangeInclusive(DecodeBcd(bytes[MonthOffset]), 1, MaximumMonth, "month");
        var day = AssertRangeInclusive(DecodeBcd(bytes[DayOffset]), 1, MaximumDay, "day of month");
        var hour = AssertRangeInclusive(DecodeBcd(bytes[HourOffset]), 0, MaximumHour, "hour");
        var minute = AssertRangeInclusive(DecodeBcd(bytes[MinuteOffset]), 0, MaximumMinuteOrSecond, "minute");
        var second = AssertRangeInclusive(DecodeBcd(bytes[SecondOffset]), 0, MaximumMinuteOrSecond, "second");
        var hsec = AssertRangeInclusive(DecodeBcd(bytes[HundredthMillisecondOffset]), 0, MaximumHundredths, "first two millisecond digits");
        var msec = AssertRangeInclusive(bytes[MillisecondAndWeekdayOffset] >> BcdNibbleShift, 0, MaximumSingleDigit, "third millisecond digit");
        return new System.DateTime(year, month, day, hour, minute, second, (hsec * DecimalRadix) + msec, System.DateTimeKind.Unspecified);
    }
}
