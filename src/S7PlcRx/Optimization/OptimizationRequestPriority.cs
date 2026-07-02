// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>Specifies the priority level for an optimization request.</summary>
/// <remarks>Use this enumeration to indicate the relative importance of an optimization request. Higher priority
/// values may be processed before lower ones, depending on the scheduling or queuing logic of the system.</remarks>
public enum OptimizationRequestPriority
{
    /// <summary>Low priority.</summary>
    Low = 0,

    /// <summary>Normal priority.</summary>
    Normal = 1,

    /// <summary>High priority.</summary>
    High = 2,

    /// <summary>Critical priority.</summary>
    Critical = 3
}
