// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for converting between S7 WString byte arrays and .NET strings.
/// </summary>
/// <remarks>The S7WString class supports encoding and decoding of S7 WString values, which are commonly used in
/// Siemens S7 PLCs. All methods are static and thread-safe. The S7 WString format includes a 4-byte header specifying
/// the reserved and actual string lengths, followed by the UTF-16 encoded string data.</remarks>
public static class S7WString
{
    /// <summary>
    /// Converts a byte array containing an S7 WString value to its corresponding .NET string representation.
    /// </summary>
    /// <remarks>The input array must follow the S7 WString format, where the first two bytes specify the
    /// maximum capacity, the next two bytes specify the actual string length, and the remaining bytes contain the
    /// UTF-16 encoded string data in big-endian order.</remarks>
    /// <param name="bytes">The byte array containing the S7 WString data, including the 4-byte header. Must not be null and must have a
    /// length of at least 4 bytes.</param>
    /// <returns>A string representing the decoded S7 WString value from the specified byte array.</returns>
    /// <exception cref="PlcException">Thrown if the input array is null, too short, contains malformed S7 WString data, or if decoding fails.</exception>
    public static string FromByteArray(byte[] bytes)
    {
        if (bytes?.Length < 4)
        {
            throw new PlcException(ErrorCode.ReadData, "Malformed S7 WString / too short");
        }

        var size = (bytes![0] << 8) | bytes[1];
        var length = (bytes[2] << 8) | bytes[3];

        if (length > size)
        {
            throw new PlcException(ErrorCode.ReadData, "Malformed S7 WString / length larger than capacity");
        }

        try
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 4, length * 2);
        }
        catch (Exception e)
        {
            throw new PlcException(
                ErrorCode.ReadData,
                $"Failed to parse {VarType.S7WString} from data. Following fields were read: size: '{size}', actual length: '{length}', total number of bytes (including header): '{bytes.Length}'.",
                e);
        }
    }

    /// <summary>
    /// Converts the specified string to a big-endian Unicode byte array with a reserved length prefix.
    /// </summary>
    /// <remarks>The returned byte array begins with a 4-byte header: the first two bytes represent the
    /// reserved length, and the next two bytes represent the actual string length, both in big-endian order. The string
    /// is encoded using big-endian Unicode (UTF-16BE).</remarks>
    /// <param name="value">The string to convert to a byte array. Cannot be null.</param>
    /// <param name="reservedLength">The number of characters to reserve in the output buffer. Must be less than or equal to 16,382 and greater than
    /// or equal to the length of <paramref name="value"/>.</param>
    /// <returns>A byte array containing a 4-byte header followed by the big-endian Unicode bytes of the string, padded to the
    /// reserved length if necessary.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="reservedLength"/> is greater than 16,382, or if the length of <paramref name="value"/>
    /// exceeds <paramref name="reservedLength"/>.</exception>
    public static byte[] ToByteArray(string? value, int reservedLength)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (reservedLength > 16382)
        {
            throw new ArgumentException("The maximum string length supported is 16382.");
        }

        var buffer = new byte[4 + (reservedLength * 2)];
        buffer[0] = (byte)((reservedLength >> 8) & 0xFF);
        buffer[1] = (byte)(reservedLength & 0xFF);
        buffer[2] = (byte)((value.Length >> 8) & 0xFF);
        buffer[3] = (byte)(value.Length & 0xFF);

        var stringLength = Encoding.BigEndianUnicode.GetBytes(value, 0, value.Length, buffer, 4) / 2;
        if (stringLength > reservedLength)
        {
            throw new ArgumentException($"The provided string length ({stringLength} is larger than the specified reserved length ({reservedLength}).");
        }

        return buffer;
    }
}
