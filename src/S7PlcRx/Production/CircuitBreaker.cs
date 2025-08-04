// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Circuit breaker implementation for production reliability.
/// </summary>
public sealed class CircuitBreaker
{
    private readonly ProductionErrorConfig _config;
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTime _lastFailureTime;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreaker"/> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    public CircuitBreaker(ProductionErrorConfig config) => _config = config;

    /// <summary>Gets the current circuit breaker state.</summary>
    public CircuitBreakerState State => _state;

    /// <summary>Gets the total number of operations executed.</summary>
    public long TotalOperations { get; private set; }

    /// <summary>Gets the number of successful operations.</summary>
    public long SuccessfulOperations { get; private set; }

    /// <summary>Gets the number of failed operations.</summary>
    public long FailedOperations { get; private set; }

    /// <summary>Gets the success rate as a percentage.</summary>
    public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations * 100 : 0;

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation), "Operation cannot be null");
        }

        lock (_lock)
        {
            TotalOperations++;

            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime < _config.CircuitBreakerTimeout)
                {
                    throw new InvalidOperationException("Circuit breaker is open - operation blocked");
                }

                _state = CircuitBreakerState.HalfOpen;
            }
        }

        try
        {
            var result = await ExecuteWithRetry(operation);

            lock (_lock)
            {
                SuccessfulOperations++;
                _consecutiveFailures = 0;
                _state = CircuitBreakerState.Closed;
            }

            return result;
        }
        catch (Exception)
        {
            lock (_lock)
            {
                FailedOperations++;
                _consecutiveFailures++;
                _lastFailureTime = DateTime.UtcNow;

                if (_consecutiveFailures >= _config.CircuitBreakerThreshold)
                {
                    _state = CircuitBreakerState.Open;
                }
            }

            throw;
        }
    }

    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation)
    {
        var attempts = 0;
        Exception? lastException = null;

        while (attempts <= _config.MaxRetryAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts <= _config.MaxRetryAttempts)
                {
                    var delay = _config.UseExponentialBackoff
                        ? _config.BaseRetryDelayMs * (int)Math.Pow(2, attempts - 1)
                        : _config.BaseRetryDelayMs;

                    await Task.Delay(delay);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }
}
