// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert between <see cref="T:System.DateTime" /> and S7 representation of DateTimeLong (DTL) values.
/// </summary>
public static class DateTimeLong
{
    /// <summary>
    /// The type length in bytes.
    /// </summary>
    public const int TypeLengthInBytes = 12;

    /// <summary>
    /// The minimum <see cref="T:System.DateTime" /> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMinimumDateTime = new(1970, 1, 1);

    /// <summary>
    /// The maximum <see cref="T:System.DateTime" /> value supported by the specification.
    /// </summary>
    public static readonly System.DateTime SpecMaximumDateTime = new(2262, 4, 11, 23, 47, 16, 854);

    /// <summary>
    /// Parses a <see cref="T:System.DateTime" /> value from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime" /> object representing the value read from PLC.</returns>
    public static System.DateTime FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Parses a <see cref="T:System.DateTime" /> value from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>A <see cref="T:System.DateTime" /> object representing the value read from PLC.</returns>
    public static System.DateTime FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTimeLong requires exactly 12 bytes of input data, input data is {bytes.Length} bytes long.");
        }

        return FromSpanImpl(bytes);
    }

    /// <summary>
    /// Parses an array of <see cref="T:System.DateTime" /> values from bytes.
    /// </summary>
    /// <param name="bytes">Input bytes read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime" /> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>
    /// Parses an array of <see cref="T:System.DateTime" /> values from a span.
    /// </summary>
    /// <param name="bytes">Input bytes span read from PLC.</param>
    /// <returns>An array of <see cref="T:System.DateTime" /> objects representing the values read from PLC.</returns>
    public static System.DateTime[] ToArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % TypeLengthInBytes != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing an array of DateTimeLong requires a multiple of 12 bytes of input data, input data is '{bytes.Length}' long.");
        }

        var cnt = bytes.Length / TypeLengthInBytes;
        var result = new System.DateTime[cnt];

        for (var i = 0; i < cnt; i++)
        {
            result[i] = FromSpanImpl(bytes.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }

        return result;
    }

    /// <summary>
    /// Converts a <see cref="T:System.DateTime" /> value to a byte array.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <returns>A byte array containing the S7 DateTimeLong representation of <paramref name="dateTime" />.</returns>
    public static byte[] ToByteArray(System.DateTime dateTime)
    {
        Span<byte> bytes = stackalloc byte[TypeLengthInBytes];
        ToSpan(dateTime, bytes);
        return bytes.ToArray();
    }

    /// <summary>
    /// Converts a <see cref="T:System.DateTime" /> value to a span.
    /// </summary>
    /// <param name="dateTime">The DateTime value to convert.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(System.DateTime dateTime, Span<byte> destination)
    {
        if (destination.Length < TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span must be at least 12 bytes", nameof(destination));
        }

        if (dateTime < SpecMinimumDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is before the minimum '{SpecMinimumDateTime}' supported in S7 DateTimeLong representation.");
        }

        if (dateTime > SpecMaximumDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(dateTime), dateTime, $"Date time '{dateTime}' is after the maximum '{SpecMaximumDateTime}' supported in S7 DateTimeLong representation.");
        }

        // Convert Year
        Word.ToSpan((ushort)dateTime.Year, destination.Slice(0, 2));

        // Convert Month
        destination[2] = (byte)dateTime.Month;

        // Convert Day
        destination[3] = (byte)dateTime.Day;

        // Convert WeekDay. NET DateTime starts with Sunday = 0, while S7DT has Sunday = 1.
        destination[4] = (byte)(dateTime.DayOfWeek + 1);

        // Convert Hour
        destination[5] = (byte)dateTime.Hour;

        // Convert Minutes
        destination[6] = (byte)dateTime.Minute;

        // Convert Seconds
        destination[7] = (byte)dateTime.Second;

        // Convert Nanoseconds. Net DateTime has a representation of 1 Tick = 100ns.
        // Thus First take the ticks Mod 1 Second (1s = 10'000'000 ticks), and then Convert to nanoseconds.
        var nanoseconds = (uint)((dateTime.Ticks % 10000000) * 100);
        DWord.ToSpan(nanoseconds, destination.Slice(8, 4));
    }

    /// <summary>
    /// Converts an array of <see cref="T:System.DateTime" /> values to a byte array.
    /// </summary>
    /// <param name="dateTimes">The DateTime values to convert.</param>
    /// <returns>A byte array containing the S7 DateTimeLong representations of <paramref name="dateTimes" />.</returns>
    public static byte[] ToByteArray(System.DateTime[] dateTimes)
    {
        if (dateTimes == null)
        {
            throw new ArgumentNullException(nameof(dateTimes));
        }

        // Use ArrayPool for large allocations
        var totalBytes = dateTimes.Length * TypeLengthInBytes;
        byte[]? pooledArray = null;
        var buffer = totalBytes > 1024
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
        if (destination.Length < dateTimes.Length * TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < dateTimes.Length; i++)
        {
            ToSpan(dateTimes[i], destination.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }
    }

    private static System.DateTime FromSpanImpl(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < TypeLengthInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes.Length, $"Parsing a DateTimeLong requires exactly 12 bytes of input data, input data is {bytes.Length} bytes long.");
        }

        var year = AssertRangeInclusive(Word.FromSpan(bytes.Slice(0, 2)), (ushort)1970, (ushort)2262, "year");
        var month = AssertRangeInclusive(bytes[2], (byte)1, (byte)12, "month");
        var day = AssertRangeInclusive(bytes[3], (byte)1, (byte)31, "day of month");
        ////var dayOfWeek = AssertRangeInclusive(bytes[4], (byte)1, (byte)7, "day of week");
        var hour = AssertRangeInclusive(bytes[5], (byte)0, (byte)23, "hour");
        var minute = AssertRangeInclusive(bytes[6], (byte)0, (byte)59, "minute");
        var second = AssertRangeInclusive(bytes[7], (byte)0, (byte)59, "second");

        var nanoseconds = AssertRangeInclusive(DWord.FromSpan(bytes.Slice(8, 4)), 0u, 999999999u, "nanoseconds");

        var time = new System.DateTime(year, month, day, hour, minute, second);
        return time.AddTicks(nanoseconds / 100);
    }

    private static T AssertRangeInclusive<T>(T input, T min, T max, string field)
        where T : IComparable<T>
    {
        if (input.CompareTo(min) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is lower than the minimum '{min}' allowed for {field}.");
        }

        if (input.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), input, $"Value '{input}' is higher than the maximum '{max}' allowed for {field}.");
        }

        return input;
    }
}
