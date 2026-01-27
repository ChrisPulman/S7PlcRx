// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between binary representations, numeric types, and bitwise operations.
/// Intended for internal use in scenarios involving low-level data manipulation or communication with systems that
/// require specific binary formats.
/// </summary>
/// <remarks>The Conversion class includes extension methods for converting between binary strings and numeric
/// types, extracting or setting individual bits in bytes, and handling conversions between signed and unsigned integer
/// representations. These methods are particularly useful when working with protocols or data formats that require
/// explicit control over binary layouts, such as PLC communication or custom serialization. This class is not intended
/// for general-purpose use and should be used with care, as incorrect usage may result in data loss or unexpected
/// behavior.</remarks>
internal static class Conversion
{
    /// <summary>
    /// Converts a string representation of a binary number to its 32-bit signed integer equivalent.
    /// </summary>
    /// <remarks>The method interprets the input string as a binary number, with the leftmost character as the
    /// most significant bit. The input string must not be empty and should only contain '0' and '1' characters;
    /// otherwise, the result may not be meaningful.</remarks>
    /// <param name="txt">The string containing the binary number to convert. Each character must be '0' or '1'.</param>
    /// <returns>A 32-bit signed integer equivalent to the binary number represented by <paramref name="txt"/>.</returns>
    public static int BinStringToInt32(this string txt)
    {
        var ret = 0;

        for (var i = 0; i < txt.Length; i++)
        {
            ret = (ret << 1) | ((txt[i] == '1') ? 1 : 0);
        }

        return ret;
    }

    /// <summary>
    /// Converts an 8-character binary string to its equivalent byte value.
    /// </summary>
    /// <remarks>If the input string contains any characters other than '0' or '1', or if its length is not
    /// exactly 8, the method returns null. This method is intended for use with valid 8-bit binary
    /// representations.</remarks>
    /// <param name="txt">The string containing exactly 8 characters, each of which must be '0' or '1', representing a binary number.</param>
    /// <returns>A byte value equivalent to the binary string if the input has exactly 8 characters; otherwise, null.</returns>
    public static byte? BinStringToByte(this string txt) => txt.Length == 8 ? (byte)BinStringToInt32(txt) : null;

    /// <summary>
    /// Converts the specified 32-bit unsigned integer to a double-precision floating-point number by interpreting its
    /// binary representation.
    /// </summary>
    /// <remarks>This method performs a bitwise reinterpretation of the input value. The resulting double may
    /// not represent a numerically meaningful value unless the input was originally produced from a double using the
    /// corresponding conversion.</remarks>
    /// <param name="input">The 32-bit unsigned integer whose bit pattern is to be reinterpreted as a double-precision floating-point value.</param>
    /// <returns>A double-precision floating-point number whose binary representation matches that of the input value.</returns>
    public static double ConvertToDouble(this uint input) => LReal.FromByteArray(DWord.ToByteArray(input));

    /// <summary>
    /// Converts the specified 32-bit unsigned integer to its IEEE 754 single-precision floating-point representation by
    /// interpreting the bit pattern as a float.
    /// </summary>
    /// <remarks>This method does not perform numeric conversion; it reinterprets the raw bits of the input as
    /// a float. Use this method when you need to treat the binary representation of an unsigned integer as a
    /// floating-point value, such as when working with low-level data formats or serialization.</remarks>
    /// <param name="input">The 32-bit unsigned integer whose bit pattern is to be reinterpreted as a single-precision floating-point value.</param>
    /// <returns>A single-precision floating-point value whose bit pattern is identical to that of the input integer.</returns>
    public static float ConvertToFloat(this uint input) => Real.FromByteArray(DWord.ToByteArray(input));

    /// <summary>
    /// Converts the specified unsigned integer to a 32-bit signed integer by interpreting its hexadecimal
    /// representation.
    /// </summary>
    /// <remarks>If the hexadecimal value of the input exceeds the range of a 32-bit signed integer, an
    /// exception is thrown.</remarks>
    /// <param name="input">The unsigned integer value to convert.</param>
    /// <returns>A 32-bit signed integer that represents the hexadecimal value of the input.</returns>
    public static int ConvertToInt(this uint input) => int.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts the specified 16-bit unsigned integer to a 16-bit signed integer by interpreting its hexadecimal
    /// representation.
    /// </summary>
    /// <remarks>This method interprets the input value as a hexadecimal number and parses it as a signed
    /// 16-bit integer. Values greater than 0x7FFF will be converted to their corresponding negative signed values due
    /// to two's complement representation.</remarks>
    /// <param name="input">The 16-bit unsigned integer to convert.</param>
    /// <returns>A 16-bit signed integer that represents the hexadecimal value of the input.</returns>
    public static short ConvertToShort(this ushort input) => short.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts the specified single-precision floating-point value to a 32-bit unsigned integer by interpreting its
    /// binary representation.
    /// </summary>
    /// <remarks>This method does not perform numeric conversion or rounding. Instead, it reinterprets the raw
    /// bits of the floating-point value as an unsigned integer. Use this method when you need to access the underlying
    /// bit pattern of a float.</remarks>
    /// <param name="input">The single-precision floating-point value whose bit pattern is to be reinterpreted as a 32-bit unsigned integer.</param>
    /// <returns>A 32-bit unsigned integer that has the same binary representation as the input floating-point value.</returns>
    public static uint ConvertToUInt(this float input) => DWord.FromByteArray(LReal.ToByteArray(input));

    /// <summary>
    /// Converts the specified 32-bit signed integer to its equivalent 32-bit unsigned integer by interpreting its
    /// hexadecimal representation.
    /// </summary>
    /// <remarks>This method interprets the hexadecimal string representation of the input value as an
    /// unsigned integer. Negative input values will be converted based on their hexadecimal form, which may result in
    /// large unsigned values.</remarks>
    /// <param name="input">The 32-bit signed integer to convert.</param>
    /// <returns>A 32-bit unsigned integer that represents the hexadecimal value of the input integer.</returns>
    public static uint ConvertToUInt(this int input) => uint.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts a 16-bit signed integer to its equivalent 16-bit unsigned integer by interpreting the value as a
    /// hexadecimal number.
    /// </summary>
    /// <remarks>This method interprets the binary representation of the input as a hexadecimal value and
    /// converts it to an unsigned 16-bit integer. Negative input values will be converted to their two's complement
    /// unsigned representation.</remarks>
    /// <param name="input">The 16-bit signed integer to convert.</param>
    /// <returns>A 16-bit unsigned integer that represents the hexadecimal value of the input.</returns>
    public static ushort ConvertToUshort(this short input) => ushort.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Determines whether the specified bit is set in the given byte value.
    /// </summary>
    /// <param name="data">The byte value to examine.</param>
    /// <param name="bitPosition">The zero-based position of the bit to check, where 0 represents the least significant bit. Must be in the range
    /// 0 to 7.</param>
    /// <returns>true if the bit at the specified position is set; otherwise, false.</returns>
    public static bool SelectBit(this byte data, int bitPosition)
    {
        var mask = 1 << bitPosition;
        var result = data & mask;

        return result != 0;
    }

    /// <summary>
    /// Helper to set a bit value to the given byte at the bit index.
    /// <br/>
    /// <example>
    ///   Set the bit at index 4:
    ///   <code>
    ///     byte data = 0;
    ///     data.SetBit(4, true);
    ///   </code>
    /// </example>
    /// </summary>
    /// <param name="data">The data to be modified.</param>
    /// <param name="index">The zero-based index of the bit to set.</param>
    /// <param name="value">The Boolean value to assign to the bit.</param>
    public static void SetBit(this ref byte data, int index, bool value)
    {
        if ((uint)index > 7)
        {
            return;
        }

        if (value)
        {
            var mask = (byte)(1 << index);
            data |= mask;
        }
        else
        {
            var mask = (byte)~(1 << index);
            data &= mask;
        }
    }

    /// <summary>
    /// Converts the specified numeric value or array to its binary string representation.
    /// </summary>
    /// <remarks>Each supported numeric type is converted to its full-width binary representation (e.g., 8
    /// bits for Byte, 16 bits for Int16, 32 bits for Int32, and 64 bits for Int64). For arrays, the binary
    /// representations of all elements are concatenated in order. Types other than the supported numeric types will
    /// result in an empty string.</remarks>
    /// <param name="value">An object representing a single numeric value or an array of numeric values. Supported types are Byte, Int16,
    /// Int32, Int64, and their corresponding array types.</param>
    /// <returns>A string containing the binary representation of the input value or array. Returns an empty string if the input
    /// type is not supported or if an error occurs during conversion.</returns>
    public static string ValToBinString(this object value)
    {
        var txt = string.Empty;
        try
        {
            int cnt;
            int x;
            if (value.GetType().Name.IndexOf("[]") < 0)
            {
                long longValue;

                // is only one value
                switch (value.GetType().Name)
                {
                    case "Byte":
                        x = 7;
                        longValue = (byte)value;
                        break;

                    case "Int16":
                        x = 15;
                        longValue = (short)value;
                        break;

                    case "Int32":
                        x = 31;
                        longValue = (int)value;
                        break;

                    case "Int64":
                        x = 63;
                        longValue = (long)value;
                        break;

                    default:
                        throw new Exception();
                }

                for (cnt = x; cnt >= 0; cnt += -1)
                {
                    txt += (longValue & (long)Math.Pow(2, cnt)) > 0 ? "1" : "0";
                }
            }
            else
            {
                int cnt2;

                // is an Array
                switch (value.GetType().Name)
                {
                    case "Byte[]":
                        x = 7;
                        var byteArr = (byte[])value;
                        for (cnt2 = 0; cnt2 <= byteArr.Length - 1; cnt2++)
                        {
                            for (cnt = x; cnt >= 0; cnt += -1)
                            {
                                txt += (byteArr[cnt2] & (byte)Math.Pow(2, cnt)) > 0 ? "1" : "0";
                            }
                        }

                        break;

                    case "Int16[]":
                        x = 15;
                        var int16Arr = (short[])value;
                        for (cnt2 = 0; cnt2 <= int16Arr.Length - 1; cnt2++)
                        {
                            for (cnt = x; cnt >= 0; cnt += -1)
                            {
                                txt += (int16Arr[cnt2] & (byte)Math.Pow(2, cnt)) > 0 ? "1" : "0";
                            }
                        }

                        break;

                    case "Int32[]":
                        x = 31;
                        var int32Arr = (int[])value;
                        for (cnt2 = 0; cnt2 <= int32Arr.Length - 1; cnt2++)
                        {
                            for (cnt = x; cnt >= 0; cnt += -1)
                            {
                                txt += (int32Arr[cnt2] & (byte)Math.Pow(2, cnt)) > 0 ? "1" : "0";
                            }
                        }

                        break;

                    case "Int64[]":
                        x = 63;
                        var int64Arr = (byte[])value;
                        for (cnt2 = 0; cnt2 <= int64Arr.Length - 1; cnt2++)
                        {
                            for (cnt = x; cnt >= 0; cnt += -1)
                            {
                                txt += (int64Arr[cnt2] & (byte)Math.Pow(2, cnt)) > 0 ? "1" : "0";
                            }
                        }

                        break;

                    default:
                        throw new Exception("Conversion error in ValToBinString with the type");
                }
            }

            return txt;
        }
        catch
        {
            return string.Empty;
        }
    }
}
