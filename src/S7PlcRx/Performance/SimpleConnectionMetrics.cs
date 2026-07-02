// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Performance;
#else
namespace S7PlcRx.Performance;
#endif

/// <summary>Provides metrics related to a connection, including reconnection count and uptime.</summary>
/// <remarks>This class is intended for internal use to track basic connection statistics. It is not
/// thread-safe.</remarks>
internal sealed class SimpleConnectionMetrics
{
    /// <summary>Stores the s ta rt ti m e used by this instance.</summary>
    private readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>Gets the number of reconnections.</summary>
    public int ReconnectionCount { get; private set; }

    /// <summary>Gets the connection uptime.</summary>
    /// <returns>Connection uptime.</returns>
    public TimeSpan GetUptime() => DateTime.UtcNow - _startTime;

    /// <summary>Records a reconnection.</summary>
    public void RecordReconnection() => ReconnectionCount++;
}
