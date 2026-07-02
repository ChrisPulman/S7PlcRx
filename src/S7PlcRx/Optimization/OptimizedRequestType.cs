// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>Specifies the type of operation to be performed in an optimized request.</summary>
/// <remarks>Use this enumeration to indicate whether a request is intended for reading, writing, or performing
/// diagnostic actions. The value selected may affect how the system processes or prioritizes the request.</remarks>
internal enum OptimizedRequestType
{
    /// <summary>Read operation.</summary>
    Read,

    /// <summary>Write operation.</summary>
    Write,

    /// <summary>Diagnostic operation.</summary>
    Diagnostic
}
