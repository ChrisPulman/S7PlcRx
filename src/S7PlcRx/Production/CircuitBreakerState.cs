// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Production;
#else
namespace S7PlcRx.Production;
#endif

/// <summary>Specifies the operational state of a circuit breaker used to control the flow of operations in response to failures.</summary>
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
