// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx;

/// <summary>
/// PlcException.
/// </summary>
/// <seealso cref="System.Exception" />
[Serializable]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1194:Implement exception constructors", Justification = "Not desired in this instance.")]
public class PlcException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlcException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    public PlcException(ErrorCode errorCode)
        : this(errorCode, $"PLC communication failed with error '{errorCode}'.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlcException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public PlcException(ErrorCode errorCode, Exception? innerException)
        : this(errorCode, innerException?.Message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlcException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The message.</param>
    public PlcException(ErrorCode errorCode, string? message)
        : base(message) => ErrorCode = errorCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlcException"/> class.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The message.</param>
    /// <param name="inner">The inner.</param>
    public PlcException(ErrorCode errorCode, string? message, Exception? inner)
        : base(message, inner) => ErrorCode = errorCode;

    /// <summary>
    /// Gets the error code.
    /// </summary>
    /// <value>
    /// The error code.
    /// </value>
    public ErrorCode ErrorCode { get; }
}
