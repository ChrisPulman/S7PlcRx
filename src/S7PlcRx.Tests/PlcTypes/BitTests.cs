// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>
/// Tests Bit PlcType helpers.
/// </summary>
public class BitTests
{
    /// <summary>
    /// Ensures FromByte extracts correct bit values.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bit">The bit.</param>
    /// <param name="expected">if set to <c>true</c> [expected].</param>
    [TestCase(0b0000_0001, 0, true)]
    [TestCase(0b0000_0001, 1, false)]
    [TestCase(0b1000_0000, 7, true)]
    public void FromByte_ShouldReturnExpected(byte value, byte bit, bool expected)
    {
        Assert.That(Bit.FromByte(value, bit), Is.EqualTo(expected));
    }

    /// <summary>
    /// Ensures FromSpan validates byte index.
    /// </summary>
    [Test]
    public void FromSpan_WhenByteIndexOutOfRange_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Bit.FromSpan(stackalloc byte[1], 1, 0));
    }

    /// <summary>
    /// Ensures FromSpan validates bit index.
    /// </summary>
    /// <param name="bitIndex">Index of the bit.</param>
    [TestCase(-1)]
    [TestCase(8)]
    public void FromSpan_WhenBitIndexInvalid_ShouldThrow(int bitIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Bit.FromSpan(stackalloc byte[1], 0, bitIndex));
    }

    /// <summary>
    /// Ensures SetBit sets and clears the selected bit.
    /// </summary>
    [Test]
    public void SetBit_ShouldSetAndClear()
    {
        Span<byte> bytes = stackalloc byte[1];
        Bit.SetBit(bytes, 0, 3, true);
        Assert.That(bytes[0], Is.EqualTo(0b0000_1000));

        Bit.SetBit(bytes, 0, 3, false);
        Assert.That(bytes[0], Is.EqualTo(0));
    }

    /// <summary>
    /// Ensures ToBitArray throws when length is null.
    /// </summary>
    [Test]
    public void ToBitArray_WhenLengthNull_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => Bit.ToBitArray(new byte[] { 0x00 }, length: null));
    }

    /// <summary>
    /// Ensures ToBitArray throws when bytes span is empty.
    /// </summary>
    [Test]
    public void ToBitArray_WhenEmptySpan_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Bit.ToBitArray(ReadOnlySpan<byte>.Empty, 1));
    }

    /// <summary>
    /// Ensures ToBitArray throws when length exceeds available bits.
    /// </summary>
    [Test]
    public void ToBitArray_WhenLengthTooLarge_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Bit.ToBitArray(new byte[] { 0x00 }, length: 9));
    }

    /// <summary>
    /// Ensures ToBitArray returns exactly the requested number of bits.
    /// </summary>
    [Test]
    public void ToBitArray_ShouldRespectLength()
    {
        var bits = Bit.ToBitArray(new byte[] { 0b0000_1111 }, length: 4);
        Assert.That(bits.Length, Is.EqualTo(4));
        Assert.That(bits[0], Is.True);
        Assert.That(bits[3], Is.True);
    }

    /// <summary>
    /// Ensures GetBits reads multiple positions correctly.
    /// </summary>
    [Test]
    public void GetBits_ShouldReturnExpected()
    {
        var bytes = new byte[] { 0b0000_0011 };
        Span<(int byteIndex, int bitIndex)> positions = stackalloc (int, int)[2];
        positions[0] = (0, 0);
        positions[1] = (0, 1);

        var results = Bit.GetBits(bytes, positions);
        Assert.That(results, Is.EqualTo(new[] { true, true }));
    }

    /// <summary>
    /// Ensures SetBits applies multiple updates correctly.
    /// </summary>
    [Test]
    public void SetBits_ShouldApplyMultipleUpdates()
    {
        Span<byte> bytes = stackalloc byte[1];
        Span<(int byteIndex, int bitIndex, bool value)> updates = stackalloc (int, int, bool)[2];
        updates[0] = (0, 0, true);
        updates[1] = (0, 7, true);

        Bit.SetBits(bytes, updates);
        Assert.That(bytes[0], Is.EqualTo(0b1000_0001));
    }
}
