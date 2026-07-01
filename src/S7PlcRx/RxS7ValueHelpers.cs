// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
using S7PlcRx.Reactive.PlcTypes;
#else
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Provides static value conversion helpers for <see cref="RxS7"/>.</summary>
internal static class RxS7ValueHelpers
{
    /// <summary>Gets the response payload length in bytes.</summary>
    /// <param name="response">The S7 response buffer.</param>
    /// <param name="responseLength">The response length in bytes.</param>
    /// <returns>The response payload length in bytes.</returns>
    internal static int GetReadResponseDataLengthBytes(byte[] response, int responseLength)
    {
        if (responseLength <= 25)
        {
            return 0;
        }

        var fallbackLength = responseLength - 25;
        var dataLength = Word.FromByteArray(response, 23);
        if (dataLength <= 0)
        {
            return fallbackLength;
        }

        var parsedLength = (dataLength + 7) / 8;

        return Math.Min(parsedLength, fallbackLength);
    }

    /// <summary>Parses a non-null byte buffer into a typed PLC value.</summary>
    /// <param name="varType">The PLC variable type.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="varCount">The variable count.</param>
    /// <returns>The parsed PLC value.</returns>
    internal static object? ParseNonNullBytes(VarType varType, byte[] bytes, int varCount) => varType switch
    {
        VarType.Byte or VarType.Word or VarType.Int or VarType.DWord or VarType.DInt or VarType.Real or VarType.LReal => ParseNumericBytes(varType, bytes, varCount),
        VarType.String => PlcTypes.String.FromByteArray(bytes),
        VarType.Timer => varCount == 1 ? PlcTypes.Timer.FromByteArray(bytes) : PlcTypes.Timer.ToArray(bytes),
        VarType.Counter => varCount == 1 ? Counter.FromByteArray(bytes) : Counter.ToArray(bytes),
        VarType.Bit => GetBit(bytes[0], 0),
        _ => default,
    };

    /// <summary>Throws when a bit offset is outside the S7 bit range.</summary>
    /// <param name="bitOffset">The bit offset.</param>
    /// <param name="tag">The related tag.</param>
    internal static void EnsureBitOffsetIsValid(int bitOffset, Tag tag)
    {
        if (bitOffset <= 7)
        {
            return;
        }

        throw new ArgumentException($"Addressing Error: You can only reference bitwise locations 0-7. Address {bitOffset} is invalid.", nameof(tag));
    }

    /// <summary>Gets a bit from a byte value.</summary>
    /// <param name="value">The byte value.</param>
    /// <param name="bitOffset">The bit offset.</param>
    /// <returns>true when the bit is set; otherwise, false.</returns>
    internal static bool GetBit(byte value, int bitOffset) => (value & (1 << bitOffset)) != 0;

    /// <summary>Applies a bool or integer bit value to a byte.</summary>
    /// <param name="value">The byte to update.</param>
    /// <param name="newValue">The new bit value.</param>
    /// <param name="bitOffset">The bit offset.</param>
    /// <returns>The updated byte.</returns>
    internal static byte ApplyBitWriteValue(byte value, object newValue, int bitOffset)
    {
        var text = newValue.ToString();
        if (bool.TryParse(text, out var parsedBool))
        {
            value = SetBit(value, bitOffset, parsedBool);
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            value = SetBit(value, bitOffset, parsedInt == 1);
        }

        return value;
    }

    /// <summary>Sets or clears a bit in a byte value.</summary>
    /// <param name="value">The byte value.</param>
    /// <param name="bitOffset">The bit offset.</param>
    /// <param name="set">true to set the bit; false to clear it.</param>
    /// <returns>The updated byte.</returns>
    internal static byte SetBit(byte value, int bitOffset, bool set)
    {
        var mask = (byte)(1 << bitOffset);
        return set ? (byte)(value | mask) : (byte)(value & (value ^ mask));
    }

    /// <summary>Parses a numeric byte buffer into a scalar or array value.</summary>
    /// <param name="varType">The numeric PLC variable type.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="varCount">The variable count.</param>
    /// <returns>The parsed numeric value.</returns>
    private static object? ParseNumericBytes(VarType varType, byte[] bytes, int varCount) => varType switch
    {
        VarType.Byte => varCount == 1 ? bytes[0] : bytes,
        VarType.Word => varCount == 1 ? Word.FromByteArray(bytes) : Word.ToArray(bytes),
        VarType.Int => varCount == 1 ? Int.FromByteArray(bytes) : Int.ToArray(bytes),
        VarType.DWord => varCount == 1 ? DWord.FromByteArray(bytes) : DWord.ToArray(bytes),
        VarType.DInt => varCount == 1 ? DInt.FromByteArray(bytes) : DInt.ToArray(bytes),
        VarType.Real => varCount == 1 ? Real.FromByteArray(bytes) : Real.ToArray(bytes),
        VarType.LReal => varCount == 1 ? LReal.FromByteArray(bytes) : LReal.ToArray(bytes),
        _ => default,
    };
}
