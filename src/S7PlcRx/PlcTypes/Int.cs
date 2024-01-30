// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Int.
/// </summary>
internal static class Int
{
    /// <summary>
    /// cs the word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A short.</returns>
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
    /// From the byte array.bytes[0] -&gt; HighByte bytes[1] -&gt; LowByte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A short.</returns>
    public static short FromByteArray(byte[] bytes) => FromBytes(bytes[1], bytes[0]);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A short.</returns>
    public static short FromByteArray(byte[] bytes, int start) => FromBytes(bytes[start + 1], bytes[start]);

    /// <summary>
    /// From the bytes.
    /// </summary>
    /// <param name="loVal">The lo value.</param>
    /// <param name="hiVal">The hi value.</param>
    /// <returns>A short.</returns>
    public static short FromBytes(byte loVal, byte hiVal) => (short)((hiVal * 256) + loVal);

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A short.</returns>
    public static short[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(short value) => [(byte)((value >> 8) & 255), (byte)(value & 255)];

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(short[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
