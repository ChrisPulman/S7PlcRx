// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Provides factory methods for creating connections to Siemens S7-200 PLC devices.
/// </summary>
public static class S7200
{
    /// <summary>
    /// Creates a new instance of an S7-200 PLC client for communication over TCP/IP with optional watchdog
    /// monitoring.
    /// </summary>
    /// <remarks>If a watchdog address is specified, the client will periodically write the specified
    /// value to the PLC at the given interval to support connection monitoring or fail-safe logic. Ensure that the
    /// PLC is configured to handle the watchdog mechanism as expected.</remarks>
    /// <param name="ip">The IP address of the S7-200 PLC to connect to. Cannot be null or empty.</param>
    /// <param name="rack">The rack number of the PLC CPU module. Typically 0 for S7-200 devices.</param>
    /// <param name="slot">The slot number of the PLC CPU module. Typically 0 or 1 for S7-200 devices.</param>
    /// <param name="watchDogAddress">The address in the PLC memory to use for the watchdog mechanism, or null to disable watchdog monitoring.</param>
    /// <param name="interval">The polling interval, in milliseconds, for regular communication with the PLC. Must be greater than 0.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog address during each watchdog cycle.</param>
    /// <param name="watchDogInterval">The interval, in milliseconds, at which the watchdog value is written. Must be greater than 0.</param>
    /// <returns>An IRxS7 instance configured to communicate with the specified S7-200 PLC, with optional watchdog monitoring
    /// enabled.</returns>
    public static IRxS7 Create(string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100) =>
        new RxS7(Enums.CpuType.S7200, ip, rack, slot, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
}
