// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace S7PlcRx.PlcTypes
{
    /// <summary>
    /// Real PLC Type.
    /// </summary>
    internal static class Real
    {
        /// <summary>
        /// Froms the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>A double.</returns>
        public static double FromByteArray(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var v1 = bytes[0];
            var v2 = bytes[1];
            var v3 = bytes[2];
            var v4 = bytes[3];

            if (v1 + v2 + v3 + v4 == 0)
            {
                return 0d;
            }

            // form the string
            var txt = ValToBinString(v1) + ValToBinString(v2) + ValToBinString(v3) + ValToBinString(v4);

            // first sign
            var vz = int.Parse(txt.Substring(0, 1));
            var exd = Conversion.BinStringToInt32(txt.Substring(1, 8));
            var ma = txt.Substring(9, 23);
            var mantisse = 1d;
            var faktor = 1d;

            // All which is the number of the
            for (var cnt = 0; cnt <= 22; cnt++)
            {
                faktor /= 2.0;

                // corresponds to 2^-y
                if (ma.Substring(cnt, 1) == "1")
                {
                    mantisse += faktor;
                }
            }

            return Math.Pow(-1, vz) * Math.Pow(2, exd - 127) * mantisse;
        }

        /// <summary>
        /// Froms the d word.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A double.</returns>
        public static double FromDWord(int value)
        {
            var b = DInt.ToByteArray(value);
            return (double)FromByteArray(b);
        }

        /// <summary>
        /// Froms the d word.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A double.</returns>
        public static double FromDWord(uint value)
        {
            var b = DWord.ToByteArray(value);
            var d = FromByteArray(b);
            return d;
        }

        /// <summary>
        /// To the array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <returns>A double.</returns>
        public static double[] ToArray(byte[] bytes)
        {
            var length = bytes?.Length;
            var values = new double[length!.Value / 4];

            var counter = 0;
            for (var cnt = 0; cnt < bytes!.Length / 4; cnt++)
            {
                values[cnt] = FromByteArray(new byte[] { bytes[counter++], bytes[counter++], bytes[counter++], bytes[counter++] });
            }

            return values;
        }

        /// <summary>
        /// To the byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A byte.</returns>
        public static byte[] ToByteArray(double value)
        {
            var bytes = new byte[4];
            if (value != 0f)
            {
                string binString;
                if (value < 0)
                {
                    value *= -1;
                    binString = "1";
                }
                else
                {
                    binString = "0";
                }

                var exponent = (int)Math.Floor((double)Math.Log(value) / Math.Log(2.0));
                value = (value / Math.Pow(2, exponent)) - 1;

                binString += ValToBinString((byte)(exponent + 127));
                for (var cnt = 1; cnt <= 23; cnt++)
                {
                    if (!(value - Math.Pow(2, -cnt) < 0))
                    {
                        value -= Math.Pow(2, -cnt);
                        binString += "1";
                    }
                    else
                    {
                        binString += "0";
                    }
                }

                bytes[0] = (byte)BinStringToByte(binString.Substring(0, 8))!;
                bytes[1] = (byte)BinStringToByte(binString.Substring(8, 8))!;
                bytes[2] = (byte)BinStringToByte(binString.Substring(16, 8))!;
                bytes[3] = (byte)BinStringToByte(binString.Substring(24, 8))!;
            }
            else
            {
                bytes[0] = 0;
                bytes[1] = 0;
                bytes[2] = 0;
                bytes[3] = 0;
            }

            return bytes;
        }

        /// <summary>
        /// To the byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A byte.</returns>
        public static byte[] ToByteArray(double[] value)
        {
            var arr = new ByteArray();
            for (var i = 0; i < value?.Length; i++)
            {
                var val = value[i];
                arr.Add(ToByteArray(val));
            }

            return arr.Array;
        }

        private static byte? BinStringToByte(string txt)
        {
            var ret = 0;

            if (txt.Length == 8)
            {
                int cnt;
                for (cnt = 7; cnt >= 0; cnt += -1)
                {
                    if (int.Parse(txt.Substring(cnt, 1)) == 1)
                    {
                        ret += (int)Math.Pow(2, txt.Length - 1 - cnt);
                    }
                }

                return (byte)ret;
            }

            return null;
        }

        private static string ValToBinString(byte value)
        {
            var txt = string.Empty;

            for (var cnt = 7; cnt >= 0; cnt += -1)
            {
                if ((value & (byte)Math.Pow(2, cnt)) > 0)
                {
                    txt += "1";
                }
                else
                {
                    txt += "0";
                }
            }

            return txt;
        }
    }
}
