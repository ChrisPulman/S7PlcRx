// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Boolean = S7PlcRx.PlcTypes.Boolean;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>
/// Tests Boolean PlcType helpers.
/// </summary>
public class BooleanTests
{
    /// <summary>
    /// Ensures GetValue reads the selected bit.
    /// </summary>
    [Test]
    public void GetValue_ShouldReturnExpected()
    {
        Assert.That(Boolean.GetValue(0b0000_0010, 1), Is.True);
        Assert.That(Boolean.GetValue(0b0000_0010, 0), Is.False);
    }

    /// <summary>
    /// Ensures SetBit sets the specified bit.
    /// </summary>
    [Test]
    public void SetBit_ShouldSetBit()
    {
        var value = Boolean.SetBit(0, 2);
        Assert.That(value, Is.EqualTo(0b0000_0100));
    }

    /// <summary>
    /// Ensures SetBit by ref mutates the input value.
    /// </summary>
    [Test]
    public void SetBit_ByRef_ShouldMutate()
    {
        byte value = 0;
        Boolean.SetBit(ref value, 4);
        Assert.That(value, Is.EqualTo(0b0001_0000));
    }

    /// <summary>
    /// Ensures ClearBit clears the specified bit.
    /// </summary>
    [Test]
    public void ClearBit_ShouldClearBit()
    {
        var value = Boolean.ClearBit(0b1111_1111, 7);
        Assert.That(value, Is.EqualTo(0b0111_1111));
    }

    /// <summary>
    /// Ensures ClearBit by ref mutates the input value.
    /// </summary>
    [Test]
    public void ClearBit_ByRef_ShouldMutate()
    {
        byte value = 0b0000_1000;
        Boolean.ClearBit(ref value, 3);
        Assert.That(value, Is.EqualTo(0));
    }
}
