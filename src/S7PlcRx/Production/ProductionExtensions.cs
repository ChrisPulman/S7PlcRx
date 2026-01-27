// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace S7PlcRx.Production;

/// <summary>
/// Provides extension methods for enabling production-grade error handling, retry logic, and system validation on PLC
/// instances using the circuit breaker pattern.
/// </summary>
/// <remarks>These extension methods are intended to enhance the reliability and readiness of PLC-based systems in
/// production environments. They offer mechanisms for robust error handling, configurable retry strategies, and
/// comprehensive validation routines to assess system health and readiness for production deployment. All methods
/// require a valid IRxS7 PLC instance and may utilize user-supplied or default configuration objects. Thread safety is
/// ensured for shared resources such as circuit breakers.</remarks>
public static class ProductionExtensions
{
    private static readonly ConcurrentDictionary<string, CircuitBreaker> _circuitBreakers = new();

    /// <summary>
    /// Enables production error handling for the specified PLC using the provided configuration.
    /// </summary>
    /// <param name="plc">The PLC instance to enable production error handling for. Cannot be null.</param>
    /// <param name="config">The configuration settings to use for production error handling. Cannot be null.</param>
    /// <returns>A new instance of <see cref="ProductionErrorHandler"/> configured for the specified PLC.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> or <paramref name="config"/> is null.</exception>
    public static ProductionErrorHandler EnableProductionErrorHandling(
        this IRxS7 plc,
        ProductionErrorConfig config)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        return new ProductionErrorHandler(config);
    }

    /// <summary>
    /// Executes the specified asynchronous PLC operation with error handling and circuit breaker protection.
    /// </summary>
    /// <remarks>This method ensures that the provided operation is executed with error handling policies
    /// defined by the specified or default configuration. It uses a circuit breaker to prevent repeated execution of
    /// failing operations, which can help protect the PLC and improve system resilience.</remarks>
    /// <typeparam name="T">The type of the result returned by the operation.</typeparam>
    /// <param name="plc">The PLC instance on which to perform the operation. Cannot be null.</param>
    /// <param name="operation">A function that represents the asynchronous operation to execute. Cannot be null.</param>
    /// <param name="config">An optional configuration object that specifies error handling and circuit breaker behavior. If null, a default
    /// configuration is used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value returned by the operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> or <paramref name="operation"/> is null.</exception>
    public static async Task<T> ExecuteWithErrorHandling<T>(
        this IRxS7 plc,
        Func<Task<T>> operation,
        ProductionErrorConfig? config = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        var errorConfig = config ?? new ProductionErrorConfig();
        var circuitBreakerKey = $"{plc.IP}_{plc.PLCType}";
        var circuitBreaker = _circuitBreakers.GetOrAdd(circuitBreakerKey, _ => new CircuitBreaker(errorConfig));

        return await circuitBreaker.ExecuteAsync(operation);
    }

    /// <summary>
    /// Performs a comprehensive validation of the specified PLC to determine its readiness for production deployment.
    /// </summary>
    /// <remarks>The validation process includes checks for connectivity, performance, and reliability. The
    /// method aggregates results and determines production readiness based on the provided or default configuration.
    /// The returned result includes timestamps, scores, and any critical errors encountered during
    /// validation.</remarks>
    /// <param name="plc">The PLC instance to validate for production readiness. Cannot be null.</param>
    /// <param name="validationConfig">An optional configuration object that specifies validation parameters and thresholds. If null, default
    /// validation settings are used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a SystemValidationResult object with
    /// detailed validation outcomes, including overall readiness, scores, and any detected issues.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the plc parameter is null.</exception>
    public static async Task<SystemValidationResult> ValidateProductionReadiness(
        this IRxS7 plc,
        ProductionValidationConfig? validationConfig = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        var config = validationConfig ?? new ProductionValidationConfig();
        var result = new SystemValidationResult
        {
            ValidationStartTime = DateTime.UtcNow,
            PLCIdentifier = $"{plc.IP}:{plc.PLCType}"
        };

        try
        {
            await ValidateConnectivity(plc, result);
            await ValidatePerformance(plc, result, config);
            await ValidateReliability(plc, result, config);

            result.OverallScore = CalculateOverallScore(result);
            result.IsProductionReady = result.OverallScore >= config.MinimumProductionScore;
        }
        catch (Exception ex)
        {
            result.CriticalErrors.Add($"Validation failed: {ex.Message}");
            result.IsProductionReady = false;
        }

        result.ValidationEndTime = DateTime.UtcNow;
        return result;
    }

    /// <summary>
    /// Performs a connectivity validation test on the specified PLC and records the result in the provided validation
    /// result object.
    /// </summary>
    /// <remarks>This method adds a new connectivity test entry to the <paramref name="result"/> object,
    /// including details about the PLC's CPU information if available. The method does not throw exceptions; any errors
    /// encountered during validation are captured in the test result.</remarks>
    /// <param name="plc">The PLC instance to validate connectivity for. Must not be null and should be connected before calling this
    /// method.</param>
    /// <param name="result">The validation result object to which the connectivity test outcome will be added. Must not be null.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private static async Task ValidateConnectivity(IRxS7 plc, SystemValidationResult result)
    {
        var connectivityTest = new ValidationTest { TestName = "Connectivity", StartTime = DateTime.UtcNow };

        try
        {
            if (!plc.IsConnectedValue)
            {
                connectivityTest.Success = false;
                connectivityTest.ErrorMessage = "PLC is not connected";
            }
            else
            {
                var cpuInfo = await plc.GetCpuInfo();
                connectivityTest.Success = cpuInfo?.Length > 0;
                connectivityTest.Details.Add($"CPU Info: {string.Join(", ", cpuInfo ?? [])}");
            }
        }
        catch (Exception ex)
        {
            connectivityTest.Success = false;
            connectivityTest.ErrorMessage = ex.Message;
        }

        connectivityTest.EndTime = DateTime.UtcNow;
        result.ValidationTests.Add(connectivityTest);
    }

    /// <summary>
    /// Performs a performance validation test on the specified PLC and records the results in the provided validation
    /// result object.
    /// </summary>
    /// <remarks>This method measures the response time of the PLC by invoking a CPU information request and
    /// compares it to the maximum acceptable response time specified in the configuration. The outcome, including
    /// success status and response time details, is recorded in the validation result. If an exception occurs during
    /// the test, the error message is captured in the result.</remarks>
    /// <param name="plc">The PLC interface to be tested for performance. Cannot be null.</param>
    /// <param name="result">The result object to which the performance validation outcome will be added. Cannot be null.</param>
    /// <param name="config">The configuration settings that define acceptable performance thresholds for the validation. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ValidatePerformance(IRxS7 plc, SystemValidationResult result, ProductionValidationConfig config)
    {
        var performanceTest = new ValidationTest { TestName = "Performance", StartTime = DateTime.UtcNow };

        try
        {
            var responseStart = DateTime.UtcNow;
            await plc.GetCpuInfo();
            var responseTime = DateTime.UtcNow - responseStart;

            performanceTest.Details.Add($"Response Time: {responseTime.TotalMilliseconds:F0}ms");

            if (responseTime > config.MaxAcceptableResponseTime)
            {
                performanceTest.Success = false;
                performanceTest.ErrorMessage = $"Response time ({responseTime.TotalMilliseconds:F0}ms) exceeds maximum ({config.MaxAcceptableResponseTime.TotalMilliseconds:F0}ms)";
            }
            else
            {
                performanceTest.Success = true;
            }
        }
        catch (Exception ex)
        {
            performanceTest.Success = false;
            performanceTest.ErrorMessage = ex.Message;
        }

        performanceTest.EndTime = DateTime.UtcNow;
        result.ValidationTests.Add(performanceTest);
    }

    /// <summary>
    /// Performs a reliability validation test on the specified PLC by executing multiple consecutive operations and
    /// records the results in the provided validation result.
    /// </summary>
    /// <remarks>The method executes a series of consecutive operations against the PLC to determine its
    /// reliability, based on the configured number of attempts and minimum reliability rate. The outcome, including
    /// success rate and any error messages, is recorded in the validation result. This method does not throw on
    /// individual operation failures; instead, it aggregates results to determine overall reliability.</remarks>
    /// <param name="plc">The PLC interface to be tested for reliability. Cannot be null.</param>
    /// <param name="result">The validation result object to which the reliability test outcome will be added. Cannot be null.</param>
    /// <param name="config">The configuration settings that specify the number of test iterations and the minimum acceptable reliability
    /// rate. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ValidateReliability(IRxS7 plc, SystemValidationResult result, ProductionValidationConfig config)
    {
        var reliabilityTest = new ValidationTest { TestName = "Reliability", StartTime = DateTime.UtcNow };

        try
        {
            var consecutiveOperations = config.ReliabilityTestCount;
            var successCount = 0;

            for (var i = 0; i < consecutiveOperations; i++)
            {
                try
                {
                    await plc.GetCpuInfo();
                    successCount++;
                }
                catch
                {
                    // Count failures
                }

                if (i < consecutiveOperations - 1)
                {
                    await Task.Delay(100);
                }
            }

            var successRate = (double)successCount / consecutiveOperations;
            reliabilityTest.Details.Add($"Success Rate: {successRate:P2} ({successCount}/{consecutiveOperations})");

            if (successRate >= config.MinimumReliabilityRate)
            {
                reliabilityTest.Success = true;
            }
            else
            {
                reliabilityTest.Success = false;
                reliabilityTest.ErrorMessage = $"Reliability rate ({successRate:P2}) below minimum ({config.MinimumReliabilityRate:P2})";
            }
        }
        catch (Exception ex)
        {
            reliabilityTest.Success = false;
            reliabilityTest.ErrorMessage = ex.Message;
        }

        reliabilityTest.EndTime = DateTime.UtcNow;
        result.ValidationTests.Add(reliabilityTest);
    }

    /// <summary>
    /// Calculates the overall success rate of validation tests as a percentage.
    /// </summary>
    /// <param name="result">The result object containing the collection of validation tests to evaluate. Cannot be null.</param>
    /// <returns>A double value representing the percentage of successful validation tests. Returns 0 if there are no validation
    /// tests.</returns>
    private static double CalculateOverallScore(SystemValidationResult result)
    {
        if (result.ValidationTests.Count == 0)
        {
            return 0;
        }

        var successfulTests = result.ValidationTests.Count(t => t.Success);
        return (double)successfulTests / result.ValidationTests.Count * 100;
    }
}
