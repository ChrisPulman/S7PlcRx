// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Production error handler with enterprise-grade reliability features.
/// </summary>
public sealed class ProductionErrorHandler
{
    private readonly IRxS7 _plc;
    private readonly ProductionErrorConfig _config;
    private readonly CircuitBreaker _circuitBreaker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionErrorHandler"/> class.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="config">The error handling configuration.</param>
    public ProductionErrorHandler(IRxS7 plc, ProductionErrorConfig config)
    {
        _plc = plc;
        _config = config;
        _circuitBreaker = new CircuitBreaker(config);
    }

    /// <summary>
    /// Executes an operation with comprehensive error handling.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        return await _circuitBreaker.ExecuteAsync(operation);
    }
}
