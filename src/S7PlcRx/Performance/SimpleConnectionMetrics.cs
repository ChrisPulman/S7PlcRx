// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Performance;

/// <summary>
/// Simple connection metrics tracker for performance extensions.
/// </summary>
internal sealed class SimpleConnectionMetrics
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>Gets the number of reconnections.</summary>
    public int ReconnectionCount { get; private set; }

    /// <summary>Gets the connection uptime.</summary>
    /// <returns>Connection uptime.</returns>
    public TimeSpan GetUptime()
    {
        return DateTime.UtcNow - _startTime;
    }

    /// <summary>Records a reconnection.</summary>
    public void RecordReconnection()
    {
        ReconnectionCount++;
    }
}
