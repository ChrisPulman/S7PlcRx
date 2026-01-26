// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Specifies error codes that indicate the result of an operation or the type of error encountered.
/// </summary>
/// <remarks>Use this enumeration to identify specific error conditions when handling operation results. The
/// values represent distinct error types, such as connection failures, invalid data formats, or communication issues.
/// The meaning of each code is defined by the context in which it is used.</remarks>
public enum ErrorCode
{
    /// <summary>
    /// The no error.
    /// </summary>
    NoError = 0,

    /// <summary>
    /// The wrong CPU type.
    /// </summary>
    WrongCPUType = 1,

    /// <summary>
    /// The connection error.
    /// </summary>
    ConnectionError = 2,

    /// <summary>
    /// The IP address not available.
    /// </summary>
    IPAddressNotAvailable,

    /// <summary>
    /// The wrong variable format.
    /// </summary>
    WrongVarFormat = 10,

    /// <summary>
    /// The wrong number received bytes.
    /// </summary>
    WrongNumberReceivedBytes = 11,

    /// <summary>
    /// The send data.
    /// </summary>
    SendData = 20,

    /// <summary>
    /// The read data.
    /// </summary>
    ReadData = 30,

    /// <summary>
    /// The write data.
    /// </summary>
    WriteData = 50,
}
