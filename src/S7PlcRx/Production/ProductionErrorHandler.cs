// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Production error handler with enterprise-grade reliability features.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProductionErrorHandler"/> class.
/// </remarks>
/// <param name="config">The error handling configuration.</param>
public sealed class ProductionErrorHandler(ProductionErrorConfig config)
{
    private readonly CircuitBreaker _circuitBreaker = new(config);

    /// <summary>
    /// Executes an operation with comprehensive error handling.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation) => await _circuitBreaker.ExecuteAsync(operation);
}
