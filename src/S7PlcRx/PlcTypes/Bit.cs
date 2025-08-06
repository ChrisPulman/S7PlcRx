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
    /// Converts a bit from a span at the specified byte and bit position.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <param name="byteIndex">The byte index.</param>
    /// <param name="bitIndex">The bit index within the byte.</param>
    /// <returns>A bool.</returns>
    public static bool FromSpan(ReadOnlySpan<byte> bytes, int byteIndex, int bitIndex)
    {
        if (byteIndex >= bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteIndex), "Byte index is out of range");
        }

        if (bitIndex < 0 || bitIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7");
        }

        return FromByte(bytes[byteIndex], (byte)bitIndex);
    }

    /// <summary>
    /// Converts an array of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <returns>
    /// A BitArray with the same number of bits and equal values as <paramref name="bytes" />.
    /// </returns>
    public static BitArray ToBitArray(byte[] bytes) => ToBitArray(bytes.AsSpan(), bytes?.Length * 8);

    /// <summary>
    /// Converts a span of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes span to convert.</param>
    /// <returns>A BitArray with the same number of bits as the span.</returns>
    public static BitArray ToBitArray(ReadOnlySpan<byte> bytes) => ToBitArray(bytes, bytes.Length * 8);

    /// <summary>
    /// Converts an array of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="length">The number of bits to return.</param>
    /// <returns>A BitArray with <paramref name="length"/> bits.</returns>
    public static BitArray ToBitArray(byte[] bytes, int? length) => ToBitArray(bytes.AsSpan(), length);

    /// <summary>
    /// Converts a span of bytes to a BitArray.
    /// </summary>
    /// <param name="bytes">The bytes span to convert.</param>
    /// <param name="length">The number of bits to return.</param>
    /// <returns>A BitArray with the specified number of bits.</returns>
    public static BitArray ToBitArray(ReadOnlySpan<byte> bytes, int? length)
    {
        if (length == null)
        {
            throw new ArgumentNullException(nameof(length));
        }

        if (bytes.IsEmpty)
        {
            throw new ArgumentException("Bytes span cannot be empty", nameof(bytes));
        }

        if (length > bytes.Length * 8)
        {
            throw new ArgumentException($"Not enough data in bytes to return {length} bits.", nameof(bytes));
        }

        // For .NET Standard 2.0 compatibility, convert span to array for BitArray constructor
        var byteArray = bytes.ToArray();
        var bitArr = new BitArray(byteArray);
        var bools = new bool[length.Value];

        for (var i = 0; i < length; i++)
        {
            bools[i] = bitArr[i];
        }

        return new BitArray(bools);
    }

    /// <summary>
    /// Sets a bit in a byte span at the specified position.
    /// </summary>
    /// <param name="bytes">The bytes span to modify.</param>
    /// <param name="byteIndex">The byte index.</param>
    /// <param name="bitIndex">The bit index within the byte.</param>
    /// <param name="value">The value to set.</param>
    public static void SetBit(Span<byte> bytes, int byteIndex, int bitIndex, bool value)
    {
        if (byteIndex >= bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteIndex), "Byte index is out of range");
        }

        if (bitIndex < 0 || bitIndex > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7");
        }

        if (value)
        {
            bytes[byteIndex] = (byte)(bytes[byteIndex] | (1 << bitIndex));
        }
        else
        {
            bytes[byteIndex] = (byte)(bytes[byteIndex] & ~(1 << bitIndex));
        }
    }

    /// <summary>
    /// Efficiently extracts multiple bits from a byte span.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <param name="bitPositions">Array of (byteIndex, bitIndex) tuples.</param>
    /// <returns>Array of boolean values for the specified bit positions.</returns>
    public static bool[] GetBits(ReadOnlySpan<byte> bytes, ReadOnlySpan<(int byteIndex, int bitIndex)> bitPositions)
    {
        var results = new bool[bitPositions.Length];

        for (var i = 0; i < bitPositions.Length; i++)
        {
            var (byteIndex, bitIndex) = bitPositions[i];
            results[i] = FromSpan(bytes, byteIndex, bitIndex);
        }

        return results;
    }

    /// <summary>
    /// Efficiently sets multiple bits in a byte span.
    /// </summary>
    /// <param name="bytes">The bytes span to modify.</param>
    /// <param name="bitUpdates">Array of (byteIndex, bitIndex, value) tuples.</param>
    public static void SetBits(Span<byte> bytes, ReadOnlySpan<(int byteIndex, int bitIndex, bool value)> bitUpdates)
    {
        for (var i = 0; i < bitUpdates.Length; i++)
        {
            var (byteIndex, bitIndex, value) = bitUpdates[i];
            SetBit(bytes, byteIndex, bitIndex, value);
        }
    }
}
