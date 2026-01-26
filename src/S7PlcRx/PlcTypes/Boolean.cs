// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for manipulating individual bits within a byte value.
/// </summary>
/// <remarks>This class includes utility methods for reading, setting, and clearing specific bits in a byte. All
/// bit indices are zero-based, ranging from 0 (least significant bit) to 7 (most significant bit). These methods are
/// useful for low-level operations such as flag management, bitmasking, or protocol handling where direct bit
/// manipulation is required.</remarks>
public static class Boolean
{
    /// <summary>
    /// Determines whether the specified bit is set in the given byte value.
    /// </summary>
    /// <param name="value">The byte value to examine for the specified bit.</param>
    /// <param name="bit">The zero-based position of the bit to check. Must be in the range 0 to 7.</param>
    /// <returns>true if the bit at the specified position is set; otherwise, false.</returns>
    public static bool GetValue(byte value, int bit) => (value & (1 << bit)) != 0;

    /// <summary>
    /// Sets the value of a bit to 1 (true), given the address of the bit. Returns
    /// a copy of the value with the bit set.
    /// </summary>
    /// <param name="value">The input value to modify.</param>
    /// <param name="bit">The index (zero based) of the bit to set.</param>
    /// <returns>The modified value with the bit at index set.</returns>
    public static byte SetBit(byte value, int bit)
    {
        SetBit(ref value, bit);

        return value;
    }

    /// <summary>
    /// Sets the value of a bit to 1 (true), given the address of the bit.
    /// </summary>
    /// <param name="value">The value to modify.</param>
    /// <param name="bit">The index (zero based) of the bit to set.</param>
    public static void SetBit(ref byte value, int bit) => value = (byte)((value | (1 << bit)) & 0xFF);

    /// <summary>
    /// Resets the value of a bit to 0 (false), given the address of the bit. Returns
    /// a copy of the value with the bit cleared.
    /// </summary>
    /// <param name="value">The input value to modify.</param>
    /// <param name="bit">The index (zero based) of the bit to clear.</param>
    /// <returns>The modified value with the bit at index cleared.</returns>
    public static byte ClearBit(byte value, int bit)
    {
        ClearBit(ref value, bit);

        return value;
    }

    /// <summary>
    /// Resets the value of a bit to 0 (false), given the address of the bit.
    /// </summary>
    /// <param name="value">The input value to modify.</param>
    /// <param name="bit">The index (zero based) of the bit to clear.</param>
    public static void ClearBit(ref byte value, int bit) => value = (byte)(value & ~(1 << bit) & 0xFF);
}
