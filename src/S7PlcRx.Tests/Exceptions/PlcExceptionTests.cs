// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests for `PlcException`.
/// </summary>
public class PlcExceptionTests
{
    /// <summary>
    /// Ensures error code and default message are set.
    /// </summary>
    [Test]
    public void Ctor_WithErrorCode_ShouldSetErrorCodeAndMessage()
    {
        var ex = new PlcException(ErrorCode.ReadData);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        Assert.That(ex.Message, Does.Contain("PLC communication failed"));
    }

    /// <summary>
    /// Ensures the inner exception is propagated and its message becomes the exception message.
    /// </summary>
    [Test]
    public void Ctor_WithErrorCodeAndInnerException_ShouldPropagateInnerMessage()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new PlcException(ErrorCode.ReadData, inner);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.ReadData));
        Assert.That(ex.InnerException, Is.SameAs(inner));
        Assert.That(ex.Message, Is.EqualTo("boom"));
    }

    /// <summary>
    /// Ensures custom message and inner exception are set.
    /// </summary>
    [Test]
    public void Ctor_WithErrorCodeMessageAndInner_ShouldSetProperties()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new PlcException(ErrorCode.WriteData, "custom", inner);
        Assert.That(ex.ErrorCode, Is.EqualTo(ErrorCode.WriteData));
        Assert.That(ex.Message, Is.EqualTo("custom"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }
}
