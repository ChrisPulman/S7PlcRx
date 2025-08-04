// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>Circuit is closed (normal operation).</summary>
    Closed,

    /// <summary>Circuit is open (blocking operations).</summary>
    Open,

    /// <summary>Circuit is half-open (testing recovery).</summary>
    HalfOpen
}
