// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>The exception that is thrown when a tag's address is outside the valid range for the operation or context.</summary>
/// <remarks>This exception indicates that an operation attempted to access or use a tag with an address that is
/// not supported or is out of bounds. It is typically thrown by APIs that validate tag addresses before performing read
/// or write operations. Catch this exception to handle cases where tag addressing errors may occur, such as user input
/// or dynamic tag selection.</remarks>
[Serializable]
public class TagAddressOutOfRangeException : ArgumentOutOfRangeException
{
    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    public TagAddressOutOfRangeException(Tag? tag)
        : base(nameof(tag.Address))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
    public TagAddressOutOfRangeException(Tag tag, Exception innerException)
        : base(nameof(tag.Address), innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    public TagAddressOutOfRangeException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public TagAddressOutOfRangeException(string message)
        : base(null, message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public TagAddressOutOfRangeException(string message, Exception innerException)
        : base(null, message)
    {
        _ = innerException ?? throw new ArgumentNullException(nameof(innerException));
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="message">The message that describes the error.</param>
    public TagAddressOutOfRangeException(Tag tag, string message)
        : base(nameof(tag.Address), message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.</summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="actualValue">The value of the argument that causes this exception.</param>
    /// <param name="message">The message that describes the error.</param>
    public TagAddressOutOfRangeException(Tag tag, object actualValue, string message)
        : base(nameof(tag.Address), actualValue, message)
    {
    }
}
