// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx
{
    /// <summary>
    /// S71200.
    /// </summary>
    public static class S71200
    {
        /// <summary>
        /// Creates the specified ip.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="rack">The rack default 0.</param>
        /// <param name="watchDogAddress">The watch dog address.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="watchDogValueToWrite">The watch dog value to write.</param>
        /// <param name="watchDogInterval">The watch dog interval.</param>
        /// <returns>A IRxS7 instance.</returns>
        public static IRxS7 Create(string ip, short rack = 0, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100)
        {
            if (rack < 0 || rack > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(rack), "Rack must be between 0 and 7");
            }

            return new RxS7(Enums.CpuType.S71200, ip, rack, 1, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
        }
    }
}
