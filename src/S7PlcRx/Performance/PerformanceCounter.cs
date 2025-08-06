// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx.Performance;

/// <summary>
/// Internal performance counter for tracking operations.
/// </summary>
internal sealed class PerformanceCounter
{
    private readonly object _lock = new();
    private readonly List<double> _responseTimes = [];
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>Gets the total number of operations.</summary>
    public long TotalOperations { get; private set; }

    /// <summary>Gets the total number of errors.</summary>
    public long TotalErrors { get; private set; }

    /// <summary>Records a successful operation.</summary>
    /// <param name="responseTime">The response time of the operation.</param>
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

    /// <summary>Records an error.</summary>
    public void RecordError()
    {
        lock (_lock)
        {
            TotalErrors++;
        }
    }

    /// <summary>Gets the operations per second.</summary>
    /// <returns>Operations per second.</returns>
    public double GetOperationsPerSecond()
    {
        lock (_lock)
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalOperations / elapsed.TotalSeconds : 0;
        }
    }

    /// <summary>Gets the average response time.</summary>
    /// <returns>Average response time in milliseconds.</returns>
    public double GetAverageResponseTime()
    {
        lock (_lock)
        {
            return _responseTimes.Count > 0 ? _responseTimes.Average() : 0;
        }
    }

    /// <summary>Gets the error rate.</summary>
    /// <returns>Error rate as a value between 0.0 and 1.0.</returns>
    public double GetErrorRate()
    {
        lock (_lock)
        {
            var totalOperations = TotalOperations + TotalErrors;
            return totalOperations > 0 ? (double)TotalErrors / totalOperations : 0;
        }
    }
}
