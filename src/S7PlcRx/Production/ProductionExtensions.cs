// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace S7PlcRx.Production;

/// <summary>
/// Production-ready S7 PLC extensions providing enterprise-grade functionality.
/// </summary>
public static class ProductionExtensions
{
    private static readonly ConcurrentDictionary<string, CircuitBreaker> _circuitBreakers = new();

    /// <summary>
    /// Enables production-grade error handling with circuit breaker pattern.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="config">Error handling configuration.</param>
    /// <returns>Production error handler.</returns>
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
    /// Executes operations with production-grade error handling and retry logic.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="config">Error handling configuration.</param>
    /// <returns>The result of the operation.</returns>
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
    /// Performs comprehensive system validation for production readiness.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="validationConfig">Validation configuration.</param>
    /// <returns>Comprehensive validation results.</returns>
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
