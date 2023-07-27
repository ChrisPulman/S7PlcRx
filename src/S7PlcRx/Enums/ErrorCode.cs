// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Error Codes.
/// </summary>
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
    WriteData = 50
}
