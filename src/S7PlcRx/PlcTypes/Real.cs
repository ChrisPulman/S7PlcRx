﻿// Copyright (c) Chris Pulman. All rights reserved.
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

        // sps uses bigending so we have to reverse if platform needs
        if (BitConverter.IsLittleEndian)
        {
            // create deep copy of the array and reverse
            bytes = [bytes[3], bytes[2], bytes[1], bytes[0]];
        }

        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Converts a float to S7 Real (4 bytes).
    /// </summary>
    public static byte[] ToByteArray(float value)
    {
        var bytes = BitConverter.GetBytes(value);

        // sps uses bigending so we have to check if platform is same
        if (!BitConverter.IsLittleEndian)
        {
            return bytes;
        }

        // create deep copy of the array and reverse
        return [bytes[3], bytes[2], bytes[1], bytes[0]];
    }

    /// <summary>
    /// Converts an array of float to an array of bytes.
    /// </summary>
    public static byte[] ToByteArray(float[] value)
    {
        var buffer = new byte[4 * value.Length];
        var stream = new MemoryStream(buffer);
        foreach (var val in value)
        {
            stream.Write(ToByteArray(val), 0, 4);
        }

        return buffer;
    }

    /// <summary>
    /// Converts an array of S7 Real to an array of float.
    /// </summary>
    public static float[] ToArray(byte[] bytes)
    {
        var values = new float[bytes.Length / 4];

        var counter = 0;
        for (var cnt = 0; cnt < bytes.Length / 4; cnt++)
        {
            values[cnt] = FromByteArray([bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++]]);
        }

        return values;
    }
}
