// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx
{
    /// <summary>
    /// S7300.
    /// </summary>
    public static class S7300
    {
        /// <summary>
        /// Creates the specified ip.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="rack">The rack.</param>
        /// <param name="slot">The slot.</param>
        /// <param name="watchDogAddress">The watch dog address.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="watchDogValueToWrite">The watch dog value to write.</param>
        /// <param name="watchDogInterval">The watch dog interval.</param>
        /// <returns>A IRxS7 instance.</returns>
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
}
