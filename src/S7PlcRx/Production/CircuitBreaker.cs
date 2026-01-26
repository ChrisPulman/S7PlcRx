// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Production;

/// <summary>
/// Provides a thread-safe implementation of the circuit breaker pattern to prevent repeated execution of failing
/// operations and to allow recovery after a specified timeout.
/// </summary>
/// <remarks>The circuit breaker monitors consecutive operation failures and transitions between Closed, Open, and
/// HalfOpen states based on the provided configuration. When the failure threshold is reached, the circuit breaker
/// enters the Open state and blocks further operations until the timeout elapses. After the timeout, it transitions to
/// HalfOpen to test if operations can succeed before fully closing again. This class is thread-safe and intended for
/// use in scenarios where repeated failures should be prevented from overwhelming a system or external
/// dependency.</remarks>
/// <param name="config">The configuration settings that control circuit breaker thresholds, retry behavior, and timeouts.</param>
public sealed class CircuitBreaker(ProductionErrorConfig config)
{
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTime _lastFailureTime;

    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    /// <remarks>The state indicates whether the circuit breaker is allowing operations to proceed (Closed),
    /// temporarily blocking operations due to failures (Open), or testing if operations can resume
    /// (HalfOpen).</remarks>
    public CircuitBreakerState State { get; private set; } = CircuitBreakerState.Closed;

    /// <summary>
    /// Gets the total number of operations that have been performed.
    /// </summary>
    public long TotalOperations { get; private set; }

    /// <summary>
    /// Gets the total number of operations that have completed successfully.
    /// </summary>
    public long SuccessfulOperations { get; private set; }

    /// <summary>
    /// Gets the total number of operations that have failed.
    /// </summary>
    public long FailedOperations { get; private set; }

    /// <summary>
    /// Gets the percentage of operations that completed successfully.
    /// </summary>
    public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations * 100 : 0;

    /// <summary>
    /// Executes the specified asynchronous operation within the circuit breaker, applying retry and failure handling
    /// policies as configured.
    /// </summary>
    /// <remarks>If the circuit breaker is open due to previous failures, the operation will be blocked until
    /// the configured timeout has elapsed. Upon successful execution, the circuit breaker state is reset. This method
    /// is thread-safe.</remarks>
    /// <typeparam name="T">The type of the result returned by the asynchronous operation.</typeparam>
    /// <param name="operation">A function that represents the asynchronous operation to execute. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous execution of the operation. The task result contains the value returned
    /// by the operation if it completes successfully.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the operation parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the circuit breaker is open and the timeout period has not elapsed, preventing the operation from
    /// being executed.</exception>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation), "Operation cannot be null");
        }

        lock (_lock)
        {
            TotalOperations++;

            if (State == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _lastFailureTime < config.CircuitBreakerTimeout)
                {
                    throw new InvalidOperationException("Circuit breaker is open - operation blocked");
                }

                State = CircuitBreakerState.HalfOpen;
            }
        }

        try
        {
            var result = await ExecuteWithRetry(operation);

            lock (_lock)
            {
                SuccessfulOperations++;
                _consecutiveFailures = 0;
                State = CircuitBreakerState.Closed;
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

                if (_consecutiveFailures >= config.CircuitBreakerThreshold)
                {
                    State = CircuitBreakerState.Open;
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Executes the specified asynchronous operation with automatic retries according to the configured retry policy.
    /// </summary>
    /// <remarks>The method retries the operation if it throws an exception, up to the maximum number of retry
    /// attempts specified in the configuration. If all attempts fail, the last encountered exception is thrown. The
    /// delay between retries is determined by the configuration and may use exponential backoff.</remarks>
    /// <typeparam name="T">The type of the result returned by the asynchronous operation.</typeparam>
    /// <param name="operation">A function that represents the asynchronous operation to execute. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value returned by the operation
    /// if it succeeds.</returns>
    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation)
    {
        var attempts = 0;
        Exception? lastException = null;

        while (attempts <= config.MaxRetryAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts <= config.MaxRetryAttempts)
                {
                    var delay = config.UseExponentialBackoff
                        ? config.BaseRetryDelayMs * (int)Math.Pow(2, attempts - 1)
                        : config.BaseRetryDelayMs;

                    await Task.Delay(delay);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }
}
