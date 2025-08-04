// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert from bytes to byte arrays.
/// </summary>
public static class Byte
{
    /// <summary>
    /// Converts a byte to byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(byte value) => [value];

    /// <summary>
    /// Writes a byte value to the specified span.
    /// </summary>
    /// <param name="value">The byte value.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(byte value, Span<byte> destination)
    {
        if (destination.Length < 1)
        {
            throw new ArgumentException("Destination span must be at least 1 byte", nameof(destination));
        }

        destination[0] = value;
    }

    /// <summary>
    /// Converts a byte array to byte.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A byte.</returns>
    public static byte FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts a span to byte.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A byte.</returns>
    public static byte FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 1)
        {
            throw new ArgumentException("Bytes span must contain at least 1 byte.");
        }

        return bytes[0];
    }

    /// <summary>
    /// Converts multiple byte values to the specified span.
    /// </summary>
    /// <param name="values">The byte values.</param>
    /// <param name="destination">The destination span.</param>
    public static void ToSpan(ReadOnlySpan<byte> values, Span<byte> destination)
    {
        if (destination.Length < values.Length)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        values.CopyTo(destination);
    }
}
