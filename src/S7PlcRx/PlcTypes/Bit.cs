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
    /// Determines whether the specified bit in a byte value is set.
    /// </summary>
    /// <remarks>If bitAdr is outside the range 0 to 7, the result may not be meaningful. This method does not
    /// validate the bit position.</remarks>
    /// <param name="v">The byte value to examine.</param>
    /// <param name="bitAdr">The zero-based position of the bit to check. Must be in the range 0 to 7.</param>
    /// <returns>true if the bit at the specified position is set; otherwise, false.</returns>
    public static bool FromByte(byte v, byte bitAdr) => (v & (1 << bitAdr)) != 0;

    /// <summary>
    /// Determines whether the specified bit is set in a byte within a read-only span of bytes.
    /// </summary>
    /// <param name="bytes">A read-only span of bytes from which the target byte is selected.</param>
    /// <param name="byteIndex">The zero-based index of the byte within <paramref name="bytes"/> to examine. Must be less than the length of
    /// <paramref name="bytes"/>.</param>
    /// <param name="bitIndex">The zero-based index of the bit within the selected byte to check. Must be in the range 0 to 7.</param>
    /// <returns>true if the specified bit is set; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="byteIndex"/> is greater than or equal to the length of <paramref name="bytes"/>, or if
    /// <paramref name="bitIndex"/> is less than 0 or greater than 7.</exception>
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
    /// Converts the specified byte array to a BitArray, where each bit in the array represents a bit in the input
    /// bytes.
    /// </summary>
    /// <param name="bytes">The byte array to convert. Each byte is interpreted in order, with the least significant bit first in each byte.</param>
    /// <returns>A BitArray containing the bits from the input byte array. If the input array is null or empty, returns an empty
    /// BitArray.</returns>
    public static BitArray ToBitArray(byte[] bytes) => ToBitArray(bytes.AsSpan(), bytes?.Length * 8);

    /// <summary>
    /// Creates a new BitArray representing the bits contained in the specified read-only span of bytes.
    /// </summary>
    /// <param name="bytes">A read-only span of bytes whose bits will be copied into the resulting BitArray. Each byte is interpreted in
    /// little-endian order, with the least significant bit first.</param>
    /// <returns>A BitArray containing the bits from the input span. The length of the BitArray will be equal to the total number
    /// of bits in the input.</returns>
    public static BitArray ToBitArray(ReadOnlySpan<byte> bytes) => ToBitArray(bytes, bytes.Length * 8);

    /// <summary>
    /// Converts the specified byte array to a BitArray, optionally limiting the number of bits included.
    /// </summary>
    /// <param name="bytes">The array of bytes to convert to a BitArray. Cannot be null.</param>
    /// <param name="length">The optional number of bits to include in the BitArray. If specified, only the first length bits are included;
    /// otherwise, all bits from the byte array are used. Must be non-negative and not greater than the total number of
    /// bits in the array.</param>
    /// <returns>A BitArray containing the bits from the specified byte array, limited to the specified length if provided.</returns>
    public static BitArray ToBitArray(byte[] bytes, int? length) => ToBitArray(bytes.AsSpan(), length);

    /// <summary>
    /// Converts a span of bytes to a BitArray containing the specified number of bits.
    /// </summary>
    /// <remarks>The returned BitArray contains bits in the same order as they appear in the input bytes,
    /// starting from the least significant bit of the first byte. This method is compatible with .NET Standard 2.0 by
    /// converting the span to an array before constructing the BitArray.</remarks>
    /// <param name="bytes">The span of bytes to convert to a BitArray. The span must not be empty and must contain enough data to represent
    /// the requested number of bits.</param>
    /// <param name="length">The number of bits to include in the resulting BitArray. Must not be null and must not exceed the total number
    /// of bits available in the input bytes.</param>
    /// <returns>A BitArray containing the first length bits from the input bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown if length is null.</exception>
    /// <exception cref="ArgumentException">Thrown if bytes is empty or if length is greater than the total number of bits available in bytes.</exception>
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
    /// Sets the value of a specific bit within a byte in the provided span.
    /// </summary>
    /// <param name="bytes">A span of bytes in which the bit will be set or cleared.</param>
    /// <param name="byteIndex">The zero-based index of the byte within <paramref name="bytes"/> whose bit will be modified. Must be less than
    /// the length of <paramref name="bytes"/>.</param>
    /// <param name="bitIndex">The zero-based index of the bit to modify within the specified byte. Must be in the range 0 to 7.</param>
    /// <param name="value">The value to assign to the specified bit. If <see langword="true"/>, the bit is set; if <see langword="false"/>,
    /// the bit is cleared.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="byteIndex"/> is greater than or equal to the length of <paramref name="bytes"/>, or
    /// when <paramref name="bitIndex"/> is less than 0 or greater than 7.</exception>
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
    /// Extracts the values of specified bits from a sequence of bytes.
    /// </summary>
    /// <remarks>If a specified bit position refers to an index outside the bounds of the input span, an
    /// exception may be thrown.</remarks>
    /// <param name="bytes">The span of bytes from which bits will be read.</param>
    /// <param name="bitPositions">A span of tuples specifying the positions of bits to extract. Each tuple contains the zero-based index of the
    /// byte and the zero-based index of the bit within that byte.</param>
    /// <returns>An array of Boolean values indicating the state of each requested bit. Each element is <see langword="true"/> if
    /// the corresponding bit is set; otherwise, <see langword="false"/>.</returns>
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
    /// Sets the specified bits in the provided byte span according to the given updates.
    /// </summary>
    /// <remarks>Each tuple in <paramref name="bitUpdates"/> must reference a valid byte and bit index within
    /// <paramref name="bytes"/>. Modifying bits outside the bounds of <paramref name="bytes"/> may result in undefined
    /// behavior.</remarks>
    /// <param name="bytes">The span of bytes in which bits will be set or cleared. Each update modifies a bit within this span.</param>
    /// <param name="bitUpdates">A read-only span of tuples specifying the byte index, bit index, and value to set for each bit. Each tuple
    /// indicates which bit to update and whether to set it to <see langword="true"/> or <see langword="false"/>.</param>
    public static void SetBits(Span<byte> bytes, ReadOnlySpan<(int byteIndex, int bitIndex, bool value)> bitUpdates)
    {
        for (var i = 0; i < bitUpdates.Length; i++)
        {
            var (byteIndex, bitIndex, value) = bitUpdates[i];
            SetBit(bytes, byteIndex, bitIndex, value);
        }
    }
}
