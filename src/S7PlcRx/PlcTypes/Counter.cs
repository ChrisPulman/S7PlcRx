// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Counter.
/// </summary>
public static class Counter
{
    /// <summary>
    /// From the byte array. bytes[0] -&gt; HighByte, bytes[1] -&gt; LowByte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes)
    {
        if (bytes?.Length != 2)
        {
            throw new ArgumentException("Byte array must be 2 bytes long");
        }

        return FromBytes(bytes[1], bytes[0]);
    }

    /// <summary>
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <returns>A ushort.</returns>
    public static ushort FromByteArray(byte[] bytes, int start)
    {
        if (bytes?.Length < start + 2)
        {
            throw new ArgumentException("Byte array must be at least 2 bytes long");
        }

        return FromBytes(bytes![start + 1], bytes[start]);
    }

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
    /// <returns>A ushort.</returns>
    public static ushort[] ToArray(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        return TypeConverter.ToArray(bytes, FromByteArray);
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort value) => [(byte)((value << 8) & 255), (byte)(value & 255)];

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(ushort[] value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return TypeConverter.ToByteArray(value, ToByteArray);
    }
}
