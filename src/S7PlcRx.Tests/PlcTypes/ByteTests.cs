// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Byte = S7PlcRx.PlcTypes.Byte;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>
/// Tests the byte PlcType helpers.
/// </summary>
public class ByteTests
{
    /// <summary>
    /// Ensures byte conversion roundtrips.
    /// </summary>
    [Test]
    public void ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        var bytes = Byte.ToByteArray(0xAB);
        var parsed = Byte.FromByteArray(bytes);
        Assert.That(parsed, Is.EqualTo(0xAB));
    }

    /// <summary>
    /// Ensures span write guard is enforced.
    /// </summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        var dest = Array.Empty<byte>();
        Assert.Throws<ArgumentException>(() => Byte.ToSpan(0x01, dest));
    }

    /// <summary>
    /// Ensures span read guard is enforced.
    /// </summary>
    [Test]
    public void FromSpan_WhenEmpty_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Byte.FromSpan(ReadOnlySpan<byte>.Empty));
    }

    /// <summary>
    /// Ensures multi-byte copy works.
    /// </summary>
    [Test]
    public void ToSpan_WhenCopyingMultipleBytes_ShouldCopy()
    {
        byte[] src = [1, 2, 3];
        Span<byte> dest = stackalloc byte[3];
        Byte.ToSpan(src, dest);
        Assert.That(dest.ToArray(), Is.EqualTo(src));
    }

    /// <summary>
    /// Ensures multi-byte copy guard is enforced.
    /// </summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmallForMultiple_ShouldThrow()
    {
        byte[] src = [1, 2, 3];
        var dest = new byte[2];
        Assert.Throws<ArgumentException>(() => Byte.ToSpan(src, dest));
    }
}
