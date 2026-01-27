// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Represents the configuration settings for error handling and retry logic in a production environment.
/// </summary>
/// <remarks>This class provides options to control retry attempts, delay strategies, and circuit breaker behavior
/// for handling transient errors. It is typically used to configure error resilience policies in applications that
/// interact with external systems or services.</remarks>
public sealed class ProductionErrorConfig
{
    /// <summary>Gets or sets the maximum retry attempts.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Gets or sets the base retry delay in milliseconds.</summary>
    public int BaseRetryDelayMs { get; set; } = 1000;

    /// <summary>Gets or sets a value indicating whether gets or sets whether to use exponential backoff.</summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>Gets or sets the circuit breaker failure threshold.</summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>Gets or sets the circuit breaker timeout.</summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
