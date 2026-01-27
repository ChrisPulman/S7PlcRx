// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// The exception that is thrown when a tag's address is outside the valid range for the operation or context.
/// </summary>
/// <remarks>This exception indicates that an operation attempted to access or use a tag with an address that is
/// not supported or is out of bounds. It is typically thrown by APIs that validate tag addresses before performing read
/// or write operations. Catch this exception to handle cases where tag addressing errors may occur, such as user input
/// or dynamic tag selection.</remarks>
[Serializable]
#pragma warning disable RCS1194 // Implement exception constructors.
public class TagAddressOutOfRangeException : ArgumentOutOfRangeException
#pragma warning restore RCS1194 // Implement exception constructors.
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.
    /// </summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    public TagAddressOutOfRangeException(Tag? tag)
        : base(nameof(tag.Address))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.
    /// </summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
    public TagAddressOutOfRangeException(Tag tag, Exception innerException)
        : base(nameof(tag.Address), innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.
    /// </summary>
    public TagAddressOutOfRangeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.
    /// </summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="message">The message that describes the error.</param>
    public TagAddressOutOfRangeException(Tag tag, string message)
        : base(nameof(tag.Address), message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TagAddressOutOfRangeException"/> class.
    /// </summary>
    /// <param name="tag">The Tag that caused the exception.</param>
    /// <param name="actualValue">The value of the argument that causes this exception.</param>
    /// <param name="message">The message that describes the error.</param>
    public TagAddressOutOfRangeException(Tag tag, object actualValue, string message)
        : base(nameof(tag.Address), actualValue, message)
    {
    }
}
