// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert from S7 strings to C# strings
/// An S7 String has a preceeding 2 byte header containing its capacity and length.
/// </summary>
public static class S7String
{
    private static Encoding stringEncoding = Encoding.ASCII;

    /// <summary>
    /// Gets or sets the Encoding used when serializing and deserializing S7String (Encoding.ASCII by default).
    /// </summary>
    /// <value>
    /// The string encoding.
    /// </value>
    /// <exception cref="System.ArgumentNullException">StringEncoding.</exception>
    /// <exception cref="ArgumentNullException">StringEncoding must not be null.</exception>
    public static Encoding StringEncoding
    {
        get => stringEncoding;
        set => stringEncoding = value ?? throw new ArgumentNullException(nameof(StringEncoding));
    }

    /// <summary>
    /// Converts S7 bytes to a string.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A string.</returns>
    /// <exception cref="S7PlcRx.PlcException">
    /// Malformed S7 String / too short
    /// or
    /// Malformed S7 String / length larger than capacity
    /// or
    /// Failed to parse {VarType.S7String} from data. Following fields were read: size: '{size}', actual length: '{length}', total number of bytes (including header): '{bytes.Length}'.
    /// </exception>
    public static string FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts S7 bytes from span to a string.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A string.</returns>
    /// <exception cref="S7PlcRx.PlcException">
    /// Malformed S7 String / too short
    /// or
    /// Malformed S7 String / length larger than capacity
    /// or
    /// Failed to parse {VarType.S7String} from data. Following fields were read: size: '{size}', actual length: '{length}', total number of bytes (including header): '{bytes.Length}'.
    /// </exception>
    public static string FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new PlcException(ErrorCode.ReadData, "Malformed S7 String / too short");
        }

        int size = bytes[0];
        int length = bytes[1];
        if (length > size)
        {
            throw new PlcException(ErrorCode.ReadData, "Malformed S7 String / length larger than capacity");
        }

        if (bytes.Length < 2 + length)
        {
            throw new PlcException(ErrorCode.ReadData, $"Insufficient data for S7 String. Expected {2 + length} bytes, got {bytes.Length}");
        }

        try
        {
            // For .NET Standard 2.0 compatibility, convert span to array for encoding
            var stringBytes = bytes.Slice(2, length).ToArray();
            return StringEncoding.GetString(stringBytes);
        }
        catch (Exception e)
        {
            throw new PlcException(
                ErrorCode.ReadData,
                $"Failed to parse {VarType.S7String} from data. Following fields were read: size: '{size}', actual length: '{length}', total number of bytes (including header): '{bytes.Length}'.",
                e);
        }
    }

    /// <summary>
    /// Converts a <see cref="T:string"/> to S7 string with 2-byte header.
    /// </summary>
    /// <param name="value">The string to convert to byte array.</param>
    /// <param name="reservedLength">The length (in characters) allocated in PLC for the string.</param>
    /// <returns>A <see cref="T:byte[]" /> containing the string header and string value with a maximum length of <paramref name="reservedLength"/> + 2.</returns>
    public static byte[] ToByteArray(string? value, int reservedLength)
    {
        Span<byte> buffer = stackalloc byte[2 + reservedLength];
        var bytesWritten = ToSpan(value, reservedLength, buffer);
        return buffer.Slice(0, bytesWritten).ToArray();
    }

    /// <summary>
    /// Converts a string to S7 string format in the specified span.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="reservedLength">The length allocated in PLC for the string.</param>
    /// <param name="destination">The destination span.</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentNullException">value.</exception>
    /// <exception cref="ArgumentException">
    /// The maximum string length supported is 254.
    /// or
    /// Destination span is too small.
    /// </exception>
    public static int ToSpan(string? value, int reservedLength, Span<byte> destination)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (reservedLength > 254)
        {
            throw new ArgumentException("The maximum string length supported is 254.");
        }

        if (destination.Length < 2 + reservedLength)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        var bytes = StringEncoding.GetBytes(value);
        if (bytes.Length > reservedLength)
        {
            throw new ArgumentException($"The provided string length ({bytes.Length}) is larger than the specified reserved length ({reservedLength}).");
        }

        // Clear the destination area
        destination.Slice(0, 2 + reservedLength).Clear();

        // Set header
        destination[0] = (byte)reservedLength;
        destination[1] = (byte)bytes.Length;

        // Copy string data
        if (bytes.Length > 0)
        {
            bytes.AsSpan().CopyTo(destination.Slice(2));
        }

        return 2 + reservedLength;
    }

    /// <summary>
    /// Tries to convert a string to S7 string format in the specified span.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <param name="reservedLength">The length allocated in PLC for the string.</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns>True if successful, false if the destination is too small.</returns>
    public static bool TryToSpan(string? value, int reservedLength, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;

        if (value is null || reservedLength > 254 || destination.Length < 2 + reservedLength)
        {
            return false;
        }

        var bytes = StringEncoding.GetBytes(value);
        if (bytes.Length > reservedLength)
        {
            return false;
        }

        bytesWritten = ToSpan(value, reservedLength, destination);
        return true;
    }

    /// <summary>
    /// Gets the total byte length for an S7 string with the specified reserved length.
    /// </summary>
    /// <param name="reservedLength">The reserved length for the string.</param>
    /// <returns>The total byte length including header.</returns>
    public static int GetByteLength(int reservedLength) => 2 + reservedLength;
}
