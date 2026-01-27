// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx.Performance;

/// <summary>
/// Provides thread-safe tracking and calculation of operation performance metrics, including total operations, errors,
/// average response time, operations per second, and error rate.
/// </summary>
/// <remarks>Use this class to monitor and analyze the performance of a set of operations in real time. Metrics
/// are updated as operations and errors are recorded, and calculations are based on data collected since the instance
/// was created. This class is intended for internal use and is not thread-safe for external modification beyond its
/// provided methods.</remarks>
internal sealed class PerformanceCounter
{
    private readonly object _lock = new();
    private readonly List<double> _responseTimes = [];
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Gets the total number of operations performed by the instance.
    /// </summary>
    public long TotalOperations { get; private set; }

    /// <summary>Gets the total number of errors.</summary>
    public long TotalErrors { get; private set; }

    /// <summary>
    /// Records the response time of an operation and updates the total operation count.
    /// </summary>
    /// <remarks>Only the most recent 100 response times are retained for analysis. This method is
    /// thread-safe.</remarks>
    /// <param name="responseTime">The duration taken to complete the operation. Represents the response time to be recorded.</param>
    public void RecordOperation(TimeSpan responseTime)
    {
        lock (_lock)
        {
            TotalOperations++;
            _responseTimes.Add(responseTime.TotalMilliseconds);

            // Keep only recent response times (last 100)
            if (_responseTimes.Count > 100)
            {
                _responseTimes.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Increments the total error count in a thread-safe manner.
    /// </summary>
    /// <remarks>Use this method to record the occurrence of an error. This method is safe to call from
    /// multiple threads concurrently.</remarks>
    public void RecordError()
    {
        lock (_lock)
        {
            TotalErrors++;
        }
    }

    /// <summary>
    /// Calculates the average number of operations performed per second since tracking began.
    /// </summary>
    /// <remarks>This method is thread-safe. The returned value reflects the current rate based on the total
    /// operations and elapsed time since the start of tracking.</remarks>
    /// <returns>The average operations per second as a double. Returns 0 if no time has elapsed since tracking started.</returns>
    public double GetOperationsPerSecond()
    {
        lock (_lock)
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalOperations / elapsed.TotalSeconds : 0;
        }
    }

    /// <summary>
    /// Calculates the average response time from the recorded response times.
    /// </summary>
    /// <remarks>This method is thread-safe. The returned value reflects the current set of recorded response
    /// times at the moment of invocation.</remarks>
    /// <returns>The average response time, in milliseconds, of all recorded responses. Returns 0 if no response times have been
    /// recorded.</returns>
    public double GetAverageResponseTime()
    {
        lock (_lock)
        {
            return _responseTimes.Count > 0 ? _responseTimes.Average() : 0;
        }
    }

    /// <summary>
    /// Calculates the error rate as the proportion of errors to total operations and errors.
    /// </summary>
    /// <remarks>The error rate is computed as TotalErrors divided by the sum of TotalOperations and
    /// TotalErrors. This method is thread-safe.</remarks>
    /// <returns>A double value representing the error rate. Returns 0 if there are no recorded operations or errors.</returns>
    public double GetErrorRate()
    {
        lock (_lock)
        {
            var totalOperations = TotalOperations + TotalErrors;
            return totalOperations > 0 ? (double)TotalErrors / totalOperations : 0;
        }
    }
}
