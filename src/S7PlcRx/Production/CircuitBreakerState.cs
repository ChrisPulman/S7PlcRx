// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Specifies the operational state of a circuit breaker used to control the flow of operations in response to failures.
/// </summary>
/// <remarks>Use this enumeration to determine or set the current state of a circuit breaker implementation. The
/// state controls whether operations are allowed, blocked, or tested for recovery. Typical usage involves transitioning
/// between these states based on error rates or recovery attempts.</remarks>
public enum CircuitBreakerState
{
    /// <summary>Circuit is closed (normal operation).</summary>
    Closed,

    /// <summary>Circuit is open (blocking operations).</summary>
    Open,

    /// <summary>Circuit is half-open (testing recovery).</summary>
    HalfOpen
}
