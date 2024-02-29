// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx
{
    /// <summary>
    /// S7200.
    /// </summary>
    public static class S7200
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
        public static IRxS7 Create(string ip, short rack, short slot, string? watchDogAddress = null, double interval = 100, ushort watchDogValueToWrite = 4500, int watchDogInterval = 100) =>
            new RxS7(Enums.CpuType.S7200, ip, rack, slot, watchDogAddress, interval, watchDogValueToWrite, watchDogInterval);
    }
}
