// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Real PLC Type.
/// </summary>
internal static class LReal
{
    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double.</returns>
    public static double FromByteArray(byte[] bytes) => FromByteArray(bytes, 0);

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>
    /// A double.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">bytes.</exception>
    public static double FromByteArray(byte[] bytes, int start)
    {
        if (bytes.Length != 8)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes array must contain 8 bytes.");
        }

        var buffer = bytes;

        // sps uses bigending so we have to reverse if platform needs
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(buffer);
        }

        return BitConverter.ToDouble(buffer, start);
    }

    /// <summary>
    /// Froms the d word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A double.</returns>
    public static double FromDWord(int value) => FromByteArray(DInt.ToByteArray(value));

    /// <summary>
    /// Froms the d word.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A double.</returns>
    public static double FromDWord(uint value) => FromByteArray(DWord.ToByteArray(value));

    /// <summary>
    /// To the array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A double.</returns>
    public static double[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte.</returns>
    public static byte[] ToByteArray(double value)
    {
        var bytes = BitConverter.GetBytes(value);

        // sps uses big endian so we have to check if platform is same
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte.</returns>
    public static byte[] ToByteArray(double[] value) => TypeConverter.ToByteArray(value, ToByteArray);
}
