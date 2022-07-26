﻿// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace S7PlcRx.PlcTypes
{
    /// <summary>
    /// Timer.
    /// </summary>
    internal static class Timer
    {
        /// <summary>
        /// From the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>A double.</returns>
        public static double FromByteArray(byte[] bytes)
        {
            var value = (short)Word.FromBytes(bytes[1], bytes[0]);
            var txt = value.ValToBinString();
            var wert = txt.Substring(4, 4).BinStringToInt32() * 100.0;
            wert += txt.Substring(8, 4).BinStringToInt32() * 10.0;
            wert += txt.Substring(12, 4).BinStringToInt32();
            switch (txt.Substring(2, 2))
            {
                case "00":
                    wert *= 0.01;
                    break;

                case "01":
                    wert *= 0.1;
                    break;

                case "10":
                    wert *= 1.0;
                    break;

                case "11":
                    wert *= 10.0;
                    break;
            }

            return wert;
        }

        /// <summary>
        /// To the array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>A double array.</returns>
        public static double[] ToArray(byte[] bytes)
        {
            var values = new double[bytes.Length / 2];

            var counter = 0;
            for (var cnt = 0; cnt < bytes.Length / 2; cnt++)
            {
                values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++] });
            }

            return values;
        }

        /// <summary>
        /// To the byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A byte array.</returns>
        public static byte[] ToByteArray(ushort value)
        {
            var bytes = new byte[2];
            const int x = 2;
            long valLong = value;
            for (var cnt = 0; cnt < x; cnt++)
            {
                var x1 = (long)Math.Pow(256, cnt);

                var x3 = valLong / x1;
                bytes[x - cnt - 1] = (byte)(x3 & 255);
                valLong -= bytes[x - cnt - 1] * x1;
            }

            return bytes;
        }

        /// <summary>
        /// To the byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A byte array.</returns>
        public static byte[] ToByteArray(ushort[] value)
        {
            var arr = new ByteArray();
            foreach (var val in value)
            {
                arr.Add(ToByteArray(val));
            }

            return arr.Array;
        }
    }
}
