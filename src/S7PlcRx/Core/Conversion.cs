// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Conversion.
/// </summary>
internal static class Conversion
{
    /// <summary>
    /// Converts a binary string to int value.
    /// </summary>
    /// <param name="txt">The text.</param>
    /// <returns>A int.</returns>
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
    /// Converts a binary string to a byte. Can return null.
    /// </summary>
    /// <param name="txt">The text.</param>
    /// <returns>
    /// A byte.
    /// </returns>
    public static byte? BinStringToByte(this string txt) => txt.Length == 8 ? (byte)BinStringToInt32(txt) : null;

    /// <summary>
    /// Converts from DWord (DBD) to double.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A double.</returns>
    public static double ConvertToDouble(this uint input) => LReal.FromByteArray(DWord.ToByteArray(input));

    /// <summary>
    /// Converts from DWord (DBD) to float.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A float.</returns>
    public static float ConvertToFloat(this uint input) => Real.FromByteArray(DWord.ToByteArray(input));

    /// <summary>
    /// Converts from uint value to int value; it's used to retrieve negative values from DBDs.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A int.</returns>
    public static int ConvertToInt(this uint input) => int.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts from ushort value to short value; it's used to retrieve negative values from words.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A short.</returns>
    public static short ConvertToShort(this ushort input) => short.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts from double to DWord (DBD).
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A uint.</returns>
    public static uint ConvertToUInt(this float input) => DWord.FromByteArray(LReal.ToByteArray(input));

    /// <summary>
    /// Converts from Int32 value to UInt32 value; it's used to pass negative values to DBDs.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A uint.</returns>
    public static uint ConvertToUInt(this int input) => uint.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Converts from short value to ushort value; it's used to pass negative values to DWs.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>A ushort.</returns>
    public static ushort ConvertToUshort(this short input) => ushort.Parse(input.ToString("X"), NumberStyles.HexNumber);

    /// <summary>
    /// Helper to get a bit value given a byte and the bit index.
    /// Example: DB1.DBX0.5 -&gt; var bytes = ReadBytes(DB1.DBW0); bool bit = bytes[0].SelectBit(5).
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="bitPosition">The bit position.</param>
    /// <returns>A bool.</returns>
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
    /// Converts the value to a binary string.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A string.</returns>
    /// <exception cref="Exception">
    /// Conversion error in ValToBinString with the type.
    /// </exception>
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
