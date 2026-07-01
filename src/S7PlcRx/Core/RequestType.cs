// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>Types of optimization requests.</summary>
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
