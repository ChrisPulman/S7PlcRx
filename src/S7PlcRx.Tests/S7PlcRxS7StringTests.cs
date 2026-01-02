// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
