// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Dint.
/// </summary>
internal static class DInt
{
    /// <summary>
    /// Cds the word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A int.</returns>
    public static int CDWord(long value)
    {
        if (value > int.MaxValue)
        {
            value -= (long)int.MaxValue + 1;
            value = (long)int.MaxValue + 1 - value;
            value *= -1;
        }

        return (int)value;
    }

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A int.</returns>
    public static int FromByteArray(byte[] bytes) => FromBytes(bytes[3], bytes[2], bytes[1], bytes[0]);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A int.</returns>
    public static int FromByteArray(byte[] bytes, int start) => FromBytes(bytes[start + 3], bytes[start + 2], bytes[start + 1], bytes[start]);

    /// <summary>
    /// Froms the bytes.
    /// </summary>
    /// <param name="v1">The v1.</param>
    /// <param name="v2">The v2.</param>
    /// <param name="v3">The v3.</param>
    /// <param name="v4">The v4.</param>
    /// <returns>A int.</returns>
    public static int FromBytes(byte v1, byte v2, byte v3, byte v4) => (int)(v1 + (v2 * Math.Pow(2, 8)) + (v3 * Math.Pow(2, 16)) + (v4 * Math.Pow(2, 24)));

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>int array.</returns>
    public static int[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(int value) => [
            (byte)((value >> 24) & 255),
            (byte)((value >> 16) & 255),
            (byte)((value >> 8) & 255),
            (byte)(value & 255),
    ];

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(int[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
