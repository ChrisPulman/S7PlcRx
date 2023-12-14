// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// DWord.
/// </summary>
internal static class DWord
{
    /// <summary>
    /// Converts a DWord (4 bytes) to uint.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A uint.</returns>
    public static uint FromByteArray(byte[] bytes) => (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A uint.</returns>
    public static uint FromByteArray(byte[] bytes, int start) =>
        (uint)(bytes[start] << 24 | bytes[start + 1] << 16 | bytes[start + 2] << 8 | bytes[start + 3]);

    /// <summary>
    /// Froms the bytes.
    /// </summary>
    /// <param name="v1">The v1.</param>
    /// <param name="v2">The v2.</param>
    /// <param name="v3">The v3.</param>
    /// <param name="v4">The v4.</param>
    /// <returns>A uint.</returns>
    public static uint FromBytes(byte v1, byte v2, byte v3, byte v4) =>
        (uint)((v4 << 24) | (v3 << 16) | (v2 << 8) | v1);

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
            values[cnt] = FromByteArray([bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++]]);
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
        bytes[0] = (byte)((value >> 24) & 0xFF);
        bytes[1] = (byte)((value >> 16) & 0xFF);
        bytes[2] = (byte)((value >> 8) & 0xFF);
        bytes[3] = (byte)(value & 0xFF);

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
