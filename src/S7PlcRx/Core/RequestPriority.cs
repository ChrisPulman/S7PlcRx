// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Core;

/// <summary>
/// Request priority levels for batch processing.
/// </summary>
public enum RequestPriority
{
    /// <summary>
    /// Low priority request.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority request.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority request.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority request.
    /// </summary>
    Critical = 3
}
