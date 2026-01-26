// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Provides metrics related to a connection, including reconnection count and uptime.
/// </summary>
/// <remarks>This class is intended for internal use to track basic connection statistics. It is not
/// thread-safe.</remarks>
internal sealed class SimpleConnectionMetrics
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>Gets the number of reconnections.</summary>
    public int ReconnectionCount { get; private set; }

    /// <summary>Gets the connection uptime.</summary>
    /// <returns>Connection uptime.</returns>
    public TimeSpan GetUptime() => DateTime.UtcNow - _startTime;

    /// <summary>Records a reconnection.</summary>
    public void RecordReconnection() => ReconnectionCount++;
}
