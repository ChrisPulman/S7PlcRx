// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Provides factory methods for creating connections to Siemens S7-1500 PLC devices.
/// </summary>
public static class S71500
{
    /// <summary>
    /// Creates a new instance of an S7 PLC client configured for the specified IP address, rack, slot, and optional
    /// watchdog monitoring.
    /// </summary>
    /// <remarks>If <paramref name="watchDogAddress"/> is specified, the client will periodically
    /// write <paramref name="watchDogValueToWrite"/> to the given address at the specified <paramref
    /// name="watchDogInterval"/>. This can be used to implement a heartbeat or keep-alive mechanism with the
    /// PLC.</remarks>
    /// <param name="ip">The IP address of the S7 PLC to connect to.</param>
    /// <param name="rack">The rack number of the PLC. Must be between 0 and 7.</param>
    /// <param name="slot">The slot number of the PLC CPU module. Must be between 1 and 31.</param>
    /// <param name="watchDogAddress">The address of the watchdog variable in the PLC memory to monitor. If null, watchdog monitoring is disabled.</param>
    /// <param name="interval">The polling interval, in milliseconds, for communication with the PLC. Must be positive.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog variable when monitoring is enabled.</param>
    /// <param name="watchDogInterval">The interval, in seconds, at which the watchdog value is written. Must be positive.</param>
    /// <returns>An IRxS7 instance configured to communicate with the specified S7 PLC and optional watchdog monitoring.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value of <paramref name="rack"/> is not between 0 and 7, or <paramref name="slot"/> is not
    /// between 1 and 31.</exception>
    public static IRxS7 Create(string ip, short rack = 0, short slot = 1, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 10)
    {
        if (rack < 0 || rack > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7");
        }

        if (slot < 1 || slot > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be between 1 and 31");
        }

        return new RxS7(Enums.CpuType.S71500, ip, rack, slot, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
    }
}
