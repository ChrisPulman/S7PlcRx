// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using S7PlcRx.Reactive.Core;
#else
using S7PlcRx.Core;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>Provides static methods for converting between S7 Timer byte representations and .NET numeric types.</summary>
/// <remarks>This class is intended for working with Siemens S7 PLC timer values, enabling conversion to and from
/// the S7-specific byte format and standard .NET types such as double and ushort. All members are static and the class
/// cannot be instantiated.</remarks>
public static class Timer
{
    /// <summary>The serialized timer width in bytes.</summary>
    private const int TypeLengthInBytes = sizeof(ushort);

    /// <summary>The first bit of the time-base selector.</summary>
    private const int TimeBaseStartBit = 2;

    /// <summary>The exclusive end bit of the time-base selector.</summary>
    private const int TimeBaseEndBit = 4;

    /// <summary>The first bit of the hundreds digit.</summary>
    private const int HundredsDigitStartBit = 4;

    /// <summary>The exclusive end bit of the hundreds digit.</summary>
    private const int HundredsDigitEndBit = 8;

    /// <summary>The first bit of the tens digit.</summary>
    private const int TensDigitStartBit = 8;

    /// <summary>The exclusive end bit of the tens digit.</summary>
    private const int TensDigitEndBit = 12;

    /// <summary>The first bit of the units digit.</summary>
    private const int UnitsDigitStartBit = 12;

    /// <summary>The exclusive end bit of the units digit.</summary>
    private const int UnitsDigitEndBit = 16;

    /// <summary>The multiplier for the hundredths-of-a-second time base.</summary>
    private const double HundredthsScale = 0.01;

    /// <summary>The multiplier for the tenths-of-a-second time base.</summary>
    private const double TenthsScale = 0.1;

    /// <summary>The multiplier for the ten-second time base and tens digit.</summary>
    private const double TensScale = 10.0;

    /// <summary>The multiplier for the hundreds digit.</summary>
    private const double HundredsScale = 100.0;

    /// <summary>Converts a byte array to a double-precision floating-point number.</summary>
    /// <param name="bytes">The byte array containing the bytes to convert. Must represent a valid double value in the expected byte order.</param>
    /// <returns>A double-precision floating-point number represented by the specified byte array.</returns>
    public static double FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>Converts a read-only span of bytes to a double-precision floating-point number.</summary>
    /// <remarks>The conversion uses the platform's endianness. Ensure that the byte order in the span matches
    /// the expected endianness for correct results.</remarks>
    /// <param name="bytes">A read-only span of bytes containing the binary representation of the double value. The span must contain at
    /// least 8 bytes, starting at the beginning of the span.</param>
    /// <returns>A double-precision floating-point number represented by the first 8 bytes of the span.</returns>
    public static double FromSpan(ReadOnlySpan<byte> bytes) => FromByteArray(bytes, 0);

    /// <summary>
    /// Converts a sequence of bytes from the specified array, starting at the given index, to a double-precision
    /// floating-point number.
    /// </summary>
    /// <param name="bytes">The byte array containing the value to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin reading the bytes.</param>
    /// <returns>A double-precision floating-point number represented by the eight bytes starting at the specified index in the
    /// array.</returns>
    public static double FromByteArray(byte[] bytes, int start) => FromSpan(bytes.AsSpan(start));

    /// <summary>
    /// Converts a sequence of bytes starting at the specified position to a double-precision floating-point value using
    /// a custom binary encoding.
    /// </summary>
    /// <remarks>The method expects a custom binary format for the encoded value. The interpretation of the
    /// bytes and the resulting value may not correspond to standard IEEE 754 encoding. Ensure that the input data
    /// matches the expected format.</remarks>
    /// <param name="bytes">A read-only span of bytes containing the encoded value.</param>
    /// <param name="start">The zero-based index in the span at which to begin reading the 2-byte encoded value.</param>
    /// <returns>A double-precision floating-point value decoded from the specified bytes.</returns>
    /// <exception cref="ArgumentException">Thrown if the span does not contain at least 2 bytes starting from the specified position.</exception>
    public static double FromByteArray(ReadOnlySpan<byte> bytes, int start)
    {
        if (bytes.Length < start + TypeLengthInBytes)
        {
            throw new ArgumentException("Bytes span must contain at least 2 bytes from start position");
        }

        var value = (short)Word.FromBytes(bytes[start + 1], bytes[start]);
        var txt = value.ValToBinString();
        var wert = txt[HundredsDigitStartBit..HundredsDigitEndBit].BinStringToInt32() * HundredsScale;
        wert += txt[TensDigitStartBit..TensDigitEndBit].BinStringToInt32() * TensScale;
        wert += txt[UnitsDigitStartBit..UnitsDigitEndBit].BinStringToInt32();
        switch (txt[TimeBaseStartBit..TimeBaseEndBit])
        {
            case "00":
                {
                    wert *= HundredthsScale;
                    break;
                }

            case "01":
                {
                    wert *= TenthsScale;
                    break;
                }

            case "10":
                {
                    wert *= 1.0;
                    break;
                }

            case "11":
                {
                    wert *= TensScale;
                    break;
                }
        }

        return wert;
    }

    /// <summary>Converts a byte array to an array of double-precision floating-point values.</summary>
    /// <remarks>The method interprets each consecutive group of 8 bytes in the input array as a
    /// double-precision floating-point value, using the system's endianness. If the length of the input array is not a
    /// multiple of 8, an exception may be thrown.</remarks>
    /// <param name="bytes">The byte array to convert. The length must be a multiple of the size of a double (8 bytes).</param>
    /// <returns>An array of double values created from the input byte array.</returns>
    public static double[] ToArray(byte[] bytes) => ToArray(bytes.AsSpan());

    /// <summary>Converts a read-only span of bytes to an array of double-precision floating-point values.</summary>
    /// <remarks>The method interprets each consecutive pair of bytes in the input span as a double value. The
    /// length of the input span must be evenly divisible by 2; otherwise, any remaining bytes are ignored.</remarks>
    /// <param name="bytes">The read-only span of bytes to convert. The length must be a multiple of 2, with each pair of bytes representing
    /// a double value.</param>
    /// <returns>An array of double values parsed from the specified byte span.</returns>
    public static double[] ToArray(ReadOnlySpan<byte> bytes)
    {
        const int typeSize = 2;
        var entries = bytes.Length / typeSize;
        var values = new double[entries];

        for (var i = 0; i < entries; ++i)
        {
            values[i] = FromByteArray(bytes, i * typeSize);
        }

        return values;
    }

    /// <summary>Converts the specified 16-bit unsigned integer to a byte array.</summary>
    /// <param name="value">The 16-bit unsigned integer to convert to a byte array.</param>
    /// <returns>A byte array containing the two bytes of the specified value in platform endianness.</returns>
    public static byte[] ToByteArray(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        ToSpan(value, bytes);
        return bytes.ToArray();
    }

    /// <summary>Writes the specified 16-bit unsigned integer value to the provided span as two bytes in big-endian order.</summary>
    /// <remarks>The value is written in big-endian byte order, with the most significant byte first. The
    /// method does not allocate memory and writes directly to the provided span.</remarks>
    /// <param name="value">The 16-bit unsigned integer value to write to the span.</param>
    /// <param name="destination">The span of bytes that will receive the two-byte representation of the value. Must be at least 2 bytes in
    /// length.</param>
    /// <exception cref="ArgumentException">Thrown if the length of destination is less than 2.</exception>
    public static void ToSpan(ushort value, Span<byte> destination)
    {
        if (destination.Length < TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span must be at least 2 bytes", nameof(destination));
        }

        Word.ToSpan(value, destination);
    }

    /// <summary>
    /// Converts a sequence of 16-bit unsigned integers to their byte representations and writes the result to the
    /// specified destination span.
    /// </summary>
    /// <param name="values">The read-only span of 16-bit unsigned integers to convert.</param>
    /// <param name="destination">The span of bytes that receives the converted values. Must be at least twice the length of <paramref
    /// name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is not large enough to contain the converted bytes.</exception>
    public static void ToSpan(ReadOnlySpan<ushort> values, Span<byte> destination)
    {
        if (destination.Length < values.Length * TypeLengthInBytes)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        for (var i = 0; i < values.Length; i++)
        {
            ToSpan(values[i], destination.Slice(i * TypeLengthInBytes, TypeLengthInBytes));
        }
    }

    /// <summary>Converts an array of 16-bit unsigned integers to a byte array.</summary>
    /// <param name="value">The array of 16-bit unsigned integers to convert. Cannot be null.</param>
    /// <returns>A byte array containing the binary representation of the input values.</returns>
    public static byte[] ToByteArray(ushort[] value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TypeConverter.ToByteArray(value, ToByteArray);
    }
}
