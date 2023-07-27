// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// DWord.
/// </summary>
internal static class DWord
{
    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A uint.</returns>
    public static uint FromByteArray(byte[] bytes) => FromBytes(bytes[3], bytes[2], bytes[1], bytes[0]);

    /// <summary>
    /// Froms the bytes.
    /// </summary>
    /// <param name="v1">The v1.</param>
    /// <param name="v2">The v2.</param>
    /// <param name="v3">The v3.</param>
    /// <param name="v4">The v4.</param>
    /// <returns>A uint.</returns>
    public static uint FromBytes(byte v1, byte v2, byte v3, byte v4) => (uint)(v1 + (v2 * Math.Pow(2, 8)) + (v3 * Math.Pow(2, 16)) + (v4 * Math.Pow(2, 24)));

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A uint array.</returns>
    public static uint[] ToArray(byte[] bytes)
    {
        var values = new uint[bytes.Length / 4];

        var counter = 0;
        for (var cnt = 0; cnt < bytes.Length / 4; cnt++)
        {
            values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++] });
        }

        return values;
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(uint value)
    {
        var bytes = new byte[4];
        var x = 4;
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
    public static byte[] ToByteArray(uint[] value)
    {
        var arr = new ByteArray();
        foreach (var val in value)
        {
            arr.Add(ToByteArray(val));
        }

        return arr.Array;
    }
}
