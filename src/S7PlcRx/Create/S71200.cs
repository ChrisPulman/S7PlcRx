// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Provides factory methods for creating connections to Siemens S7-1200 PLC devices.
/// </summary>
public static class S71200
{
    /// <summary>
    /// Creates a new instance of an S7 PLC connection with the specified configuration parameters.
    /// </summary>
    /// <param name="ip">The IP address of the S7 PLC to connect to.</param>
    /// <param name="rack">The rack number of the PLC. Must be between 0 and 7. The default is 0.</param>
    /// <param name="watchDogAddress">The address of the watchdog variable in the PLC memory. If null, the watchdog feature is disabled.</param>
    /// <param name="interval">The polling interval, in milliseconds, for reading data from the PLC. The default is 100 milliseconds.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog variable, if specified. The default is 4500.</param>
    /// <param name="watchDogInterval">The interval, in milliseconds, at which the watchdog value is written. The default is 100 milliseconds.</param>
    /// <returns>An object implementing the IRxS7 interface that represents the configured PLC connection.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value of rack is less than 0 or greater than 7.</exception>
    public static IRxS7 Create(string ip, short rack = 0, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100)
    {
        if (rack < 0 || rack > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7");
        }

        return new RxS7(Enums.CpuType.S71200, ip, rack, 1, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
    }
}
