// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Performance;
#else
namespace S7PlcRx.Performance;
#endif

/// <summary>
/// Represents a set of performance statistics for a programmable logic controller (PLC) connection, including operation
/// counts, error metrics, response times, and connection status information.
/// </summary>
/// <remarks>Use this class to track and analyze the operational performance and reliability of a PLC connection
/// over time. The statistics provided can assist in monitoring system health, diagnosing issues, and optimizing
/// performance. All properties are read-write, allowing for aggregation and updating of statistics as needed. This
/// class is not thread-safe; synchronize access if used concurrently.</remarks>
public sealed class PerformanceStatistics
{
    /// <summary>Gets or sets the PLC identifier.</summary>
    public string PLCIdentifier { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of operations.</summary>
    public long TotalOperations { get; set; }

    /// <summary>Gets or sets the total number of errors.</summary>
    public long TotalErrors { get; set; }

    /// <summary>Gets or sets the average response time in milliseconds.</summary>
    public double AverageResponseTime { get; set; }

    /// <summary>Gets or sets the operations per second.</summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>Gets or sets the error rate (0.0 to 1.0).</summary>
    public double ErrorRate { get; set; }

    /// <summary>Gets or sets the connection uptime.</summary>
    public TimeSpan ConnectionUptime { get; set; }

    /// <summary>Gets or sets the number of reconnections.</summary>
    public int ReconnectionCount { get; set; }

    /// <summary>Gets or sets when these statistics were last updated.</summary>
    public DateTime LastUpdated { get; set; }
}
