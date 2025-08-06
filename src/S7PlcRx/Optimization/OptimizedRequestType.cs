// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Types of optimized requests.
/// </summary>
internal enum OptimizedRequestType
{
    /// <summary>
    /// Read operation.
    /// </summary>
    Read,

    /// <summary>
    /// Write operation.
    /// </summary>
    Write,

    /// <summary>
    /// Diagnostic operation.
    /// </summary>
    Diagnostic
}
