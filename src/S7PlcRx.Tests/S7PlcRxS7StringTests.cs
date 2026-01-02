// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests S7 string helpers.
/// </summary>
public class S7PlcRxS7StringTests
{
    /// <summary>
    /// Ensures S7String roundtrip works.
    /// </summary>
    [Test]
    public void S7String_Roundtrip_ShouldPreserveValueWithinReservedLength()
    {
        var bytes = S7String.ToByteArray("HELLO", reservedLength: 10);
        var value = S7String.FromByteArray(bytes);
        Assert.That(value, Is.EqualTo("HELLO"));
    }

    /// <summary>
    /// Ensures S7WString roundtrip works.
    /// </summary>
    [Test]
    public void S7WString_Roundtrip_ShouldPreserveUnicodeValue()
    {
        var bytes = S7WString.ToByteArray("Hé??ø", reservedLength: 10);
        var value = S7WString.FromByteArray(bytes);
        Assert.That(value, Is.EqualTo("Hé??ø"));
    }

    /// <summary>
    /// Ensures too-short S7String payload throws.
    /// </summary>
    [Test]
    public void S7String_FromByteArray_WhenTooShort_ShouldThrowPlcException()
    {
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x01]));
        Assert.That(ex!.Message, Does.Contain("too short"));
    }

    /// <summary>
    /// Ensures invalid S7String header with length larger than capacity throws.
    /// </summary>
    [Test]
    public void S7String_FromByteArray_WhenLengthExceedsCapacity_ShouldThrowPlcException()
    {
        // size=1, length=2 => invalid
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x01, 0x02, (byte)'A', (byte)'B']));
        Assert.That(ex!.Message, Does.Contain("length larger than capacity"));
    }

    /// <summary>
    /// Ensures S7String parsing with insufficient payload bytes throws.
    /// </summary>
    [Test]
    public void S7String_FromByteArray_WhenInsufficientPayload_ShouldThrowPlcException()
    {
        // size=10, length=5 but only 1 byte payload present
        var ex = Assert.Throws<PlcException>(() => S7String.FromByteArray([0x0A, 0x05, (byte)'A']));
        Assert.That(ex!.Message, Does.Contain("Insufficient data"));
    }

    /// <summary>
    /// Ensures S7String reserved length constraint is enforced.
    /// </summary>
    [Test]
    public void S7String_ToSpan_WhenReservedLengthTooLarge_ShouldThrow()
    {
        var dest = new byte[2 + 255];
        Assert.Throws<ArgumentException>(() => S7String.ToSpan("A", reservedLength: 255, dest));
    }

    /// <summary>
    /// Ensures S7String value length cannot exceed reserved length.
    /// </summary>
    [Test]
    public void S7String_ToSpan_WhenValueTooLongForReserved_ShouldThrow()
    {
        var dest = new byte[2 + 3];
        Assert.Throws<ArgumentException>(() => S7String.ToSpan("ABCD", reservedLength: 3, dest));
    }

    /// <summary>
    /// Ensures TryToSpan returns false when destination is too small.
    /// </summary>
    [Test]
    public void S7String_TryToSpan_WhenDestinationTooSmall_ShouldReturnFalse()
    {
        Span<byte> dest = stackalloc byte[2 + 3 - 1];
        var ok = S7String.TryToSpan("A", reservedLength: 3, dest, out var written);
        Assert.That(ok, Is.False);
        Assert.That(written, Is.EqualTo(0));
    }

    /// <summary>
    /// Ensures TryToSpan returns false when value length exceeds reserved length.
    /// </summary>
    [Test]
    public void S7String_TryToSpan_WhenValueTooLong_ShouldReturnFalse()
    {
        Span<byte> dest = stackalloc byte[2 + 3];
        var ok = S7String.TryToSpan("ABCD", reservedLength: 3, dest, out var written);
        Assert.That(ok, Is.False);
        Assert.That(written, Is.EqualTo(0));
    }

    /// <summary>
    /// Ensures ToSpan writes correct header and clears trailing bytes.
    /// </summary>
    [Test]
    public void S7String_ToSpan_ShouldWriteHeaderAndClearRemaining()
    {
        Span<byte> dest = stackalloc byte[2 + 5];
        dest.Fill(0xFF);

        var written = S7String.ToSpan("HI", reservedLength: 5, dest);
        Assert.That(written, Is.EqualTo(7));
        Assert.That(dest[0], Is.EqualTo(5));
        Assert.That(dest[1], Is.EqualTo(2));
        Assert.That(dest[2], Is.EqualTo((byte)'H'));
        Assert.That(dest[3], Is.EqualTo((byte)'I'));
        Assert.That(dest[4], Is.EqualTo(0));
        Assert.That(dest[5], Is.EqualTo(0));
        Assert.That(dest[6], Is.EqualTo(0));
    }

    /// <summary>
    /// Ensures too-short S7WString payload throws.
    /// </summary>
    [Test]
    public void S7WString_FromByteArray_WhenTooShort_ShouldThrowPlcException()
    {
        var ex = Assert.Throws<PlcException>(() => S7WString.FromByteArray([0x00, 0x01, 0x00]));
        Assert.That(ex!.Message, Does.Contain("too short"));
    }

    /// <summary>
    /// Ensures invalid S7WString header with length larger than capacity throws.
    /// </summary>
    [Test]
    public void S7WString_FromByteArray_WhenLengthExceedsCapacity_ShouldThrowPlcException()
    {
        // size=1, length=2 => invalid
        var ex = Assert.Throws<PlcException>(() => S7WString.FromByteArray([0x00, 0x01, 0x00, 0x02, 0x00, 0x41, 0x00, 0x42]));
        Assert.That(ex!.Message, Does.Contain("length larger than capacity"));
    }

    /// <summary>
    /// Ensures S7WString ToByteArray rejects null.
    /// </summary>
    [Test]
    public void S7WString_ToByteArray_WhenNull_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => S7WString.ToByteArray(null, reservedLength: 1));
    }

    /// <summary>
    /// Ensures S7WString reserved length constraint is enforced.
    /// </summary>
    [Test]
    public void S7WString_ToByteArray_WhenReservedLengthTooLarge_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => S7WString.ToByteArray("A", reservedLength: 16383));
    }
}
