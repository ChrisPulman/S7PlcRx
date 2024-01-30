// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

internal static class Real
{
    /// <summary>
    /// Converts a S7 Real (4 bytes) to float.
    /// </summary>
    public static float FromByteArray(byte[] bytes)
    {
        if (bytes.Length != 4)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes array must contain 4 bytes.");
        }

        // sps uses bigendian so we have to reverse if platform needs
        if (BitConverter.IsLittleEndian)
        {
            // reverse array
            Array.Reverse(bytes);
        }

        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Converts a float to S7 Real (4 bytes).
    /// </summary>
    public static byte[] ToByteArray(float value)
    {
        var bytes = BitConverter.GetBytes(value);

        // sps uses bigendian so we have to check if platform is same
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    /// <summary>
    /// Converts an array of float to an array of bytes.
    /// </summary>
    public static byte[] ToByteArray(float[] value) => TypeConverter.ToByteArray(value, ToByteArray);

    /// <summary>
    /// Converts an array of S7 Real to an array of float.
    /// </summary>
    public static float[] ToArray(byte[] bytes) => TypeConverter.ToArray(bytes, FromByteArray);
}
