// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Provides factory methods for creating connections to Siemens S7-300 PLC devices.
/// </summary>
public static class S7300
{
    /// <summary>
    /// Creates a new instance of an S7 PLC connection with the specified configuration parameters.
    /// </summary>
    /// <param name="ip">The IP address of the S7 PLC to connect to.</param>
    /// <param name="rack">The rack number of the PLC. Must be between 0 and 7, inclusive.</param>
    /// <param name="slot">The slot number of the PLC. Must be between 1 and 31, inclusive.</param>
    /// <param name="watchDogAddress">The address in the PLC memory to use for the watchdog mechanism, or null to disable the watchdog.</param>
    /// <param name="interval">The polling interval, in milliseconds, for communication with the PLC. Must be greater than 0.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog address during each interval.</param>
    /// <param name="watchDogInterval">The interval, in milliseconds, at which the watchdog value is written. Must be greater than 0.</param>
    /// <returns>An object implementing the IRxS7 interface that represents the configured PLC connection.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value of <paramref name="rack"/> is not between 0 and 7, or when the value of <paramref
    /// name="slot"/> is not between 1 and 31.</exception>
    public static IRxS7 Create(string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100)
    {
        if (rack < 0 || rack > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7");
        }

        if (slot < 1 || slot > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be between 1 and 31");
        }

        return new RxS7(Enums.CpuType.S7300, ip, rack, slot, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
    }
}
