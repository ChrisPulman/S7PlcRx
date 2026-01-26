// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Specifies the type of request to perform on a programmable logic controller (PLC), such as reading or writing data.
/// </summary>
internal enum PLCRequestType
{
    /// <summary>
    /// The read.
    /// </summary>
    Read,

    /// <summary>
    /// The write.
    /// </summary>
    Write,
}
