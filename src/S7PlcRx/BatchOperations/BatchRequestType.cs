// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.BatchOperations;
#else
namespace S7PlcRx.BatchOperations;
#endif

/// <summary>Specifies the type of operation to perform in a batch request.</summary>
internal enum BatchRequestType
{
    /// <summary>Read operation.</summary>
    Read,

    /// <summary>Write operation.</summary>
    Write
}
