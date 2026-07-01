// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>Provides extension methods for binary and numeric conversion helpers.</summary>
internal static class ConversionExtensions
{
    /// <summary>Provides bit-selection and bit-update extensions for bytes.</summary>
    /// <param name="data">The byte value.</param>
    extension(ref byte data)
    {
        /// <summary>Determines whether the specified bit is set in the byte value.</summary>
        /// <param name="bitPosition">The zero-based position of the bit to check.</param>
        /// <returns><see langword="true"/> if the bit at the specified position is set; otherwise, <see langword="false"/>.</returns>
        public bool SelectBit(int bitPosition)
        {
            var mask = 1 << bitPosition;
            var result = data & mask;

            return result != 0;
        }

        /// <summary>Sets a bit value on the byte at the specified bit index.</summary>
        /// <param name="index">The zero-based index of the bit to set.</param>
        /// <param name="value">The Boolean value to assign to the bit.</param>
        public void SetBit(int index, bool value)
        {
            if ((uint)index > 7)
            {
                return;
            }

            if (value)
            {
                var mask = (byte)(1 << index);
                data |= mask;
            }
            else
            {
                var mask = (byte)~(1 << index);
                data &= mask;
            }
        }
    }

    /// <summary>Provides reinterpretation conversion extensions for single-precision values.</summary>
    /// <param name="input">The floating-point value.</param>
    extension(float input)
    {
        /// <summary>Converts the specified single-precision floating-point value to a 32-bit unsigned integer.</summary>
        /// <returns>A 32-bit unsigned integer that has the same binary representation as the input value.</returns>
        public uint ConvertToUInt() => DWord.FromByteArray(LReal.ToByteArray(input));
    }

    /// <summary>Provides conversion extensions for signed 32-bit values.</summary>
    /// <param name="input">The signed 32-bit value.</param>
    extension(int input)
    {
        /// <summary>Converts the specified 32-bit signed integer to its equivalent 32-bit unsigned integer.</summary>
        /// <returns>A 32-bit unsigned integer that represents the hexadecimal value of the input integer.</returns>
        public uint ConvertToUInt() => uint.Parse(input.ToString("X"), NumberStyles.HexNumber);
    }

    /// <summary>Provides conversion extensions for signed 16-bit values.</summary>
    /// <param name="input">The signed 16-bit value.</param>
    extension(short input)
    {
        /// <summary>Converts a 16-bit signed integer to its equivalent 16-bit unsigned integer.</summary>
        /// <returns>A 16-bit unsigned integer that represents the hexadecimal value of the input.</returns>
        public ushort ConvertToUshort() => ushort.Parse(input.ToString("X"), NumberStyles.HexNumber);

        /// <summary>Converts the signed 16-bit integer to its binary string representation.</summary>
        /// <returns>A 16-character binary representation of the input value.</returns>
        public string ValToBinString()
        {
            var longValue = (long)input;
            var text = string.Empty;

            for (var bit = 15; bit >= 0; bit += -1)
            {
                text += (longValue & (long)Math.Pow(2, bit)) > 0 ? "1" : "0";
            }

            return text;
        }
    }

    /// <summary>Provides binary conversion extensions for strings.</summary>
    /// <param name="text">The binary string.</param>
    extension(string text)
    {
        /// <summary>Converts a string representation of a binary number to its 32-bit signed integer equivalent.</summary>
        /// <returns>A 32-bit signed integer equivalent to the binary number.</returns>
        public int BinStringToInt32()
        {
            var ret = 0;

            for (var i = 0; i < text.Length; i++)
            {
                ret = (ret << 1) | ((text[i] == '1') ? 1 : 0);
            }

            return ret;
        }

        /// <summary>Converts an 8-character binary string to its equivalent byte value.</summary>
        /// <returns>A byte value equivalent to the binary string if the input has exactly 8 characters; otherwise, null.</returns>
        public byte? BinStringToByte() => text.Length == 8 ? (byte)text.BinStringToInt32() : null;
    }

    /// <summary>Provides reinterpretation conversion extensions for unsigned 32-bit values.</summary>
    /// <param name="input">The unsigned integer value.</param>
    extension(uint input)
    {
        /// <summary>Converts the specified 32-bit unsigned integer to a double-precision floating-point number.</summary>
        /// <returns>A double-precision floating-point number whose binary representation matches the input value.</returns>
        public double ConvertToDouble() => LReal.FromByteArray(DWord.ToByteArray(input));

        /// <summary>Converts the specified 32-bit unsigned integer to its IEEE 754 single-precision floating-point representation.</summary>
        /// <returns>A single-precision floating-point value whose bit pattern is identical to the input value.</returns>
        public float ConvertToFloat() => Real.FromByteArray(DWord.ToByteArray(input));

        /// <summary>Converts the specified unsigned integer to a 32-bit signed integer.</summary>
        /// <returns>A 32-bit signed integer that represents the hexadecimal value of the input.</returns>
        public int ConvertToInt() => int.Parse(input.ToString("X"), NumberStyles.HexNumber);
    }

    /// <summary>Provides conversion extensions for unsigned 16-bit values.</summary>
    /// <param name="input">The unsigned 16-bit value.</param>
    extension(ushort input)
    {
        /// <summary>Converts the specified 16-bit unsigned integer to a 16-bit signed integer.</summary>
        /// <returns>A 16-bit signed integer that represents the hexadecimal value of the input.</returns>
        public short ConvertToShort() => short.Parse(input.ToString("X"), NumberStyles.HexNumber);
    }
}
