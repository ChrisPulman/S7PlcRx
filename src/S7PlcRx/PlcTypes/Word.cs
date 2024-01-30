// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Word.
/// </summary>
internal static class Word
{
    /// <summary>
    /// From the byte array. bytes[0] -&gt; HighByte, bytes[1] -&gt; LowByte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes) => FromBytes(bytes[1], bytes[0]);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes, int start) => FromBytes(bytes[start + 1], bytes[start]);

    /// <summary>
    /// From the bytes.
    /// </summary>
    /// <param name="loVal">The lo value.</param>
    /// <param name="hiVal">The hi value.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromBytes(byte loVal, byte hiVal) => (ushort)((hiVal * 256) + loVal);

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A ushort array.</returns>
    public static ushort[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort value)
    {
        var bytes = new byte[2];
        const int x = 2;
        long valLong = value;
        for (var cnt = 0; cnt < x; cnt++)
        {
            var x1 = (long)Math.Pow(256, cnt);

            var x3 = valLong / x1;
            bytes[x - cnt - 1] = (byte)(x3 & 255);
            valLong -= bytes[x - cnt - 1] * x1;
        }

        return bytes;
    }

    /// <summary>
    /// Converts to bytearray.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="start">The start.</param>
    public static void ToByteArray(ushort value, Array destination, int start) =>
        Array.Copy(ToByteArray(value), 0, destination, start, 2);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
