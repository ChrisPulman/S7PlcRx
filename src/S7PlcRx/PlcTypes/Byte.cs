// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides utility methods for converting and manipulating byte values and byte arrays.
/// </summary>
/// <remarks>This static class includes methods for converting between single byte values and arrays or spans, as
/// well as writing byte data to spans. All members are static and designed for efficient, low-level byte operations.
/// Methods in this class do not perform validation beyond basic length checks and do not handle multi-byte conversions
/// or encoding.</remarks>
public static class Byte
{
    /// <summary>
    /// Converts the specified byte value to a single-element byte array.
    /// </summary>
    /// <param name="value">The byte value to include in the returned array.</param>
    /// <returns>A byte array containing the specified value as its only element.</returns>
    public static byte[] ToByteArray(byte value) => [value];

    /// <summary>
    /// Writes the specified byte value into the first position of the provided destination span.
    /// </summary>
    /// <param name="value">The byte value to write to the destination span.</param>
    /// <param name="destination">The span of bytes that will receive the value. Must have a length of at least 1.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> has a length less than 1.</exception>
    public static void ToSpan(byte value, Span<byte> destination)
    {
        if (destination.Length < 1)
        {
            throw new ArgumentException("Destination span must be at least 1 byte", nameof(destination));
        }

        destination[0] = value;
    }

    /// <summary>
    /// Creates a byte value from the specified byte array.
    /// </summary>
    /// <remarks>If the array contains more than one element, only the first element is used. If the array is
    /// empty, an exception may be thrown.</remarks>
    /// <param name="bytes">The array of bytes to convert. Must contain at least one element.</param>
    /// <returns>A byte value created from the first element of the specified array.</returns>
    public static byte FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Returns the first byte from the specified read-only span.
    /// </summary>
    /// <param name="bytes">A read-only span of bytes from which to retrieve the first byte. Must contain at least one byte.</param>
    /// <returns>The first byte in the <paramref name="bytes"/> span.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bytes"/> does not contain at least one byte.</exception>
    public static byte FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 1)
        {
            throw new ArgumentException("Bytes span must contain at least 1 byte.");
        }

        return bytes[0];
    }

    /// <summary>
    /// Copies the contents of the specified read-only byte span to the destination span.
    /// </summary>
    /// <param name="values">The read-only span containing the bytes to copy.</param>
    /// <param name="destination">The span that receives the copied bytes. Must be at least as large as <paramref name="values"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="destination"/> is smaller than <paramref name="values"/>.</exception>
    public static void ToSpan(ReadOnlySpan<byte> values, Span<byte> destination)
    {
        if (destination.Length < values.Length)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        values.CopyTo(destination);
    }
}
