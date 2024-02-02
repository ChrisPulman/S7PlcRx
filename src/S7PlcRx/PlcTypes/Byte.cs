// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert from bytes to byte arrays.
/// </summary>
public static class Byte
{
    /// <summary>
    /// Converts a byte to byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(byte value) => [value];

    /// <summary>
    /// Converts a byte array to byte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A byte.</returns>
    /// <exception cref="ArgumentException">Wrong number of bytes. Bytes array must contain 1 bytes.</exception>
    public static byte FromByteArray(byte[] bytes)
    {
        if (bytes?.Length != 1)
        {
            throw new ArgumentException("Wrong number of bytes. Bytes array must contain 1 bytes.");
        }

        return bytes[0];
    }
}
