// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Production;
#else
namespace S7PlcRx.Production;
#endif

/// <summary>
/// Provides error handling for production environments by executing operations with circuit breaker protection and
/// configurable error handling policies.
/// </summary>
/// <remarks>This class is intended for use in production scenarios where robust error handling and resilience are
/// required. It wraps operations in a circuit breaker to prevent repeated failures and applies the error handling
/// strategies specified in the provided configuration. Instances of this class are thread-safe and can be reused across
/// multiple operations.</remarks>
/// <param name="config">The configuration settings that define error handling behavior, including circuit breaker thresholds and retry
/// policies. Cannot be null.</param>
public sealed class ProductionErrorHandler(ProductionErrorConfig config)
{
    /// <summary>Stores the c ir cu it br ea k e r used by this instance.</summary>
    private readonly CircuitBreaker _circuitBreaker = new(config);

    /// <summary>Executes an operation with comprehensive error handling.</summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    public Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => _circuitBreaker.ExecuteAsync(operation);
}
