// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
    public static short[] ToArray(byte[] bytes)
    {
        var values = new short[bytes.Length / 2];

        var counter = 0;
        for (var cnt = 0; cnt < bytes.Length / 2; cnt++)
        {
            values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++] });
        }

        return values;
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(short value)
    {
        var bytes = new byte[2];
        var x = 2;
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
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(short[] value)
    {
        var arr = new ByteArray();
        foreach (var val in value)
        {
            arr.Add(ToByteArray(val));
        }

        return arr.Array;
    }
}
