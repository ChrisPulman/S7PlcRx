// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Specifies the priority level for an optimization request.
/// </summary>
/// <remarks>Use this enumeration to indicate the relative importance of an optimization request. Higher priority
/// values may be processed before lower ones, depending on the scheduling or queuing logic of the system.</remarks>
public enum OptimizationRequestPriority
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority.
    /// </summary>
    Critical = 3
}
