// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the conversion methods to convert Bit from S7 plc to C#.
/// </summary>
public static class Bit
{
    /// <summary>
    /// Converts a Bit to bool.
    /// </summary>
    /// <param name="v">The v.</param>
    /// <param name="bitAdr">The bit adr.</param>
    /// <returns>
    /// A bool.
    /// </returns>
    public static bool FromByte(byte v, byte bitAdr) => (v & (1 << bitAdr)) != 0;

    /// <summary>
    /// Converts an array of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <returns>
    /// A BitArray with the same number of bits and equal values as <paramref name="bytes" />.
    /// </returns>
    public static BitArray ToBitArray(byte[] bytes) => ToBitArray(bytes, bytes?.Length * 8);

    /// <summary>
    /// Converts an array of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="length">The number of bits to return.</param>
    /// <returns>A BitArray with <paramref name="length"/> bits.</returns>
    public static BitArray ToBitArray(byte[] bytes, int? length)
    {
        if (length == null)
        {
            throw new ArgumentNullException(nameof(length));
        }

        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (length > bytes?.Length * 8)
        {
            throw new ArgumentException($"Not enough data in bytes to return {length} bits.", nameof(bytes));
        }

        var bitArr = new BitArray(bytes!);
        var bools = new bool[length.Value];
        for (var i = 0; i < length; i++)
        {
            bools[i] = bitArr[i];
        }

        return new BitArray(bools);
    }
}
