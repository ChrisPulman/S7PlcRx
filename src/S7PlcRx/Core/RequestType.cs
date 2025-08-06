// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Core;

/// <summary>
/// Types of optimization requests.
/// </summary>
internal enum RequestType
{
    /// <summary>Read operation.</summary>
    Read,

    /// <summary>Write operation.</summary>
    Write,

    /// <summary>Batch read operation.</summary>
    BatchRead,

    /// <summary>Batch write operation.</summary>
    BatchWrite
}
