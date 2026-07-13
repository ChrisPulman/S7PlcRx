// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Provides factory methods for creating S7-400 PLC connections.</summary>
public static class S7400
{
    /// <summary>Defines the highest supported PLC rack number.</summary>
    private const short MaximumRack = 7;

    /// <summary>Defines the highest supported PLC slot number.</summary>
    private const short MaximumSlot = 31;

    /// <summary>
    /// Creates a new instance of an S7 PLC client configured for the specified IP address, rack, slot, and optional
    /// watchdog monitoring parameters.
    /// </summary>
    /// <remarks>If watchdog monitoring is enabled by specifying a non-null watchDogAddress, the
    /// client will periodically write the specified value to the given address at the defined interval. This can be
    /// used to implement a heartbeat or keep-alive mechanism with the PLC.</remarks>
    /// <param name="ip">The IP address of the S7 PLC to connect to.</param>
    /// <param name="rack">The rack number of the PLC. Must be between 0 and 7.</param>
    /// <param name="slot">The slot number of the PLC CPU. Must be between 1 and 31.</param>
    /// <param name="watchDogAddress">The address in the PLC memory to use for the watchdog mechanism, or null to disable watchdog monitoring.</param>
    /// <param name="interval">The polling interval, in milliseconds, for reading data from the PLC. Must be greater than 0.</param>
    /// <param name="watchDogValueToWrite">The value to write to the watchdog address during each interval if watchdog monitoring is enabled.</param>
    /// <param name="watchDogInterval">The interval, in milliseconds, at which the watchdog value is written if watchdog monitoring is enabled.
    /// Must be greater than 0.</param>
    /// <returns>An IRxS7 instance configured to communicate with the specified S7 PLC and optional watchdog monitoring.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if rack is not between 0 and 7, or if slot is not between 1 and 31.</exception>
    public static IRxS7 Create(string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100)
    {
        if (rack < 0 || rack > MaximumRack)
        {
            throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7");
        }

        if (slot < 1 || slot > MaximumSlot)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Slot must be between 1 and 31");
        }

        return new RxS7(new RxS7Options(
            new S7ConnectionOptions(Enums.CpuType.S7400, ip, rack, slot),
            new S7PollingOptions(interval),
            watchDogAddress is null ? null : new S7WatchdogOptions(watchDogAddress, watchDogValueToWrite, watchDogInterval)));
    }
}
