// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>
/// Tests the S7 TimeSpan (TIME) PlcType.
/// </summary>
public class TimeSpanTests
{
    /// <summary>
    /// Ensures TimeSpan byte conversion roundtrips.
    /// </summary>
    [Test]
    public void ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        var value = System.TimeSpan.FromMilliseconds(123456);
        var bytes = S7PlcRx.PlcTypes.TimeSpan.ToByteArray(value);
        var parsed = S7PlcRx.PlcTypes.TimeSpan.FromByteArray(bytes);
        Assert.That(parsed, Is.EqualTo(value));
    }

    /// <summary>
    /// Ensures FromSpan validates required length.
    /// </summary>
    [Test]
    public void FromSpan_WhenTooShort_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.FromSpan(stackalloc byte[3]));
    }

    /// <summary>
    /// Ensures ToSpan validates destination capacity.
    /// </summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        var dest = new byte[3];
        Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(System.TimeSpan.Zero, dest));
    }

    /// <summary>
    /// Ensures ToSpan enforces spec minimum.
    /// </summary>
    [Test]
    public void ToSpan_WhenBeforeSpecMinimum_ShouldThrow()
    {
        var ts = S7PlcRx.PlcTypes.TimeSpan.SpecMinimumTimeSpan - System.TimeSpan.FromMilliseconds(1);
        var dest = new byte[4];
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(ts, dest));
    }

    /// <summary>
    /// Ensures ToSpan enforces spec maximum.
    /// </summary>
    [Test]
    public void ToSpan_WhenAfterSpecMaximum_ShouldThrow()
    {
        var ts = S7PlcRx.PlcTypes.TimeSpan.SpecMaximumTimeSpan + System.TimeSpan.FromMilliseconds(1);
        var dest = new byte[4];
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.ToSpan(ts, dest));
    }

    /// <summary>
    /// Ensures multiple TimeSpans can roundtrip.
    /// </summary>
    [Test]
    public void ToArray_ThenToByteArray_ShouldRoundtripMultiple()
    {
        var values = new[]
        {
            System.TimeSpan.FromMilliseconds(-1),
            System.TimeSpan.FromMilliseconds(0),
            System.TimeSpan.FromMilliseconds(3456),
        };

        var bytes = S7PlcRx.PlcTypes.TimeSpan.ToByteArray(values);
        var parsed = S7PlcRx.PlcTypes.TimeSpan.ToArray(bytes);
        Assert.That(parsed, Is.EqualTo(values));
    }

    /// <summary>
    /// Ensures ToArray validates buffer length alignment.
    /// </summary>
    [Test]
    public void ToArray_WhenNotMultipleOf4_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.TimeSpan.ToArray(new byte[5]));
    }

    /// <summary>
    /// Ensures ToByteArray validates null input.
    /// </summary>
    [Test]
    public void ToByteArray_WhenNullArray_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => S7PlcRx.PlcTypes.TimeSpan.ToByteArray((System.TimeSpan[])null!));
    }
}
