// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Batch request types for PLC operations.
/// </summary>
internal enum BatchRequestType
{
    /// <summary>
    /// Read operation.
    /// </summary>
    Read,

    /// <summary>
    /// Write operation.
    /// </summary>
    Write
}
