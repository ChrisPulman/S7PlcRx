// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using SystemDateTime = System.DateTime;

namespace S7PlcRx.Tests.PlcTypes;

/// <summary>
/// Tests the S7 DateTime PlcType.
/// </summary>
public class DateTimeTests
{
    /// <summary>
    /// Ensures DateTime byte conversion roundtrips.
    /// </summary>
    [Test]
    public void ToByteArray_ThenFromByteArray_ShouldRoundtrip()
    {
        var value = new SystemDateTime(2024, 12, 31, 23, 59, 58, 123);
        var bytes = S7PlcRx.PlcTypes.DateTime.ToByteArray(value);
        var parsed = S7PlcRx.PlcTypes.DateTime.FromByteArray(bytes);
        Assert.That(parsed, Is.EqualTo(value));
    }

    /// <summary>
    /// Ensures FromSpan validates required length.
    /// </summary>
    [Test]
    public void FromSpan_WhenTooShort_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.FromSpan(stackalloc byte[7]));
    }

    /// <summary>
    /// Ensures ToSpan validates destination capacity.
    /// </summary>
    [Test]
    public void ToSpan_WhenDestinationTooSmall_ShouldThrow()
    {
        var dest = new byte[7];
        Assert.Throws<ArgumentException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(new SystemDateTime(2024, 1, 1), dest));
    }

    /// <summary>
    /// Ensures ToSpan enforces spec minimum.
    /// </summary>
    [Test]
    public void ToSpan_WhenBeforeSpecMinimum_ShouldThrow()
    {
        var dt = S7PlcRx.PlcTypes.DateTime.SpecMinimumDateTime.AddMilliseconds(-1);
        var dest = new byte[8];
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(dt, dest));
    }

    /// <summary>
    /// Ensures ToSpan enforces spec maximum.
    /// </summary>
    [Test]
    public void ToSpan_WhenAfterSpecMaximum_ShouldThrow()
    {
        var dt = S7PlcRx.PlcTypes.DateTime.SpecMaximumDateTime.AddMilliseconds(1);
        var dest = new byte[8];
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToSpan(dt, dest));
    }

    /// <summary>
    /// Ensures multiple DateTimes can roundtrip.
    /// </summary>
    [Test]
    public void ToArray_ThenToByteArray_ShouldRoundtripMultiple()
    {
        var values = new[]
        {
            new SystemDateTime(2020, 1, 1, 0, 0, 0, 0),
            new SystemDateTime(2025, 6, 30, 12, 34, 56, 789),
        };

        var bytes = S7PlcRx.PlcTypes.DateTime.ToByteArray(values);
        var parsed = S7PlcRx.PlcTypes.DateTime.ToArray(bytes);
        Assert.That(parsed, Is.EqualTo(values));
    }

    /// <summary>
    /// Ensures ToArray validates buffer length alignment.
    /// </summary>
    [Test]
    public void ToArray_WhenNotMultipleOf8_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => S7PlcRx.PlcTypes.DateTime.ToArray(new byte[9]));
    }
}
