// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides utility methods for converting between strings and byte arrays using ASCII encoding.
/// </summary>
/// <remarks>All methods in this class use ASCII encoding for conversions. These methods are intended for
/// scenarios where data is known to be ASCII-compatible. Non-ASCII characters will be replaced with '?' during encoding
/// and decoding. The class is internal and intended for use within the assembly.</remarks>
internal static class String
{
    /// <summary>
    /// Decodes a UTF-8 encoded byte array into a string.
    /// </summary>
    /// <param name="bytes">The byte array containing the UTF-8 encoded text to decode. Cannot be null.</param>
    /// <returns>A string representation of the decoded UTF-8 text. Returns an empty string if the array is empty.</returns>
    public static string FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts the specified read-only span of ASCII-encoded bytes to its equivalent string representation.
    /// </summary>
    /// <param name="bytes">A read-only span containing the bytes to decode as an ASCII string.</param>
    /// <returns>A string that represents the decoded ASCII characters. Returns an empty string if <paramref name="bytes"/> is
    /// empty.</returns>
    public static string FromSpan(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

#if NETSTANDARD2_0
        // Encoding APIs do not accept spans on netstandard2.0.
        return Encoding.ASCII.GetString(bytes.ToArray());
#else
        return Encoding.ASCII.GetString(bytes);
#endif
    }

    /// <summary>
    /// Converts a specified range of bytes from a byte array to a string.
    /// </summary>
    /// <param name="bytes">The byte array containing the data to convert.</param>
    /// <param name="start">The zero-based index in the array at which to begin conversion.</param>
    /// <param name="length">The number of bytes to convert starting from <paramref name="start"/>.</param>
    /// <returns>A string representation of the specified range of bytes, or an empty string if the range exceeds the bounds of
    /// the array.</returns>
    public static string FromByteArray(byte[] bytes, int start, int length)
    {
        if (bytes.Length < start + length)
        {
            return string.Empty;
        }

        return FromSpan(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// Converts the specified string to a byte array using ASCII encoding.
    /// </summary>
    /// <remarks>Characters in the input string that are not representable in ASCII are replaced with a
    /// question mark ("?") in the resulting byte array.</remarks>
    /// <param name="value">The string to convert to a byte array. If null or empty, an empty array is returned.</param>
    /// <returns>A byte array containing the ASCII-encoded bytes of the input string, or an empty array if the input is null or
    /// empty.</returns>
    public static byte[] ToByteArray(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        // Use high-performance ASCII encoding
        return Encoding.ASCII.GetBytes(value);
    }

    /// <summary>
    /// Encodes the specified string as ASCII bytes and writes the result to the provided destination span.
    /// </summary>
    /// <remarks>Characters in the input string that cannot be represented in ASCII are replaced with a
    /// question mark ('?').</remarks>
    /// <param name="value">The string to encode as ASCII. If null or empty, no bytes are written.</param>
    /// <param name="destination">The span to which the encoded ASCII bytes are written. Must be large enough to hold the encoded bytes.</param>
    /// <returns>The number of bytes written to the destination span. Returns 0 if the input string is null or empty.</returns>
    /// <exception cref="ArgumentException">Thrown if the destination span is not large enough to contain the encoded bytes.</exception>
    public static int ToSpan(string? value, Span<byte> destination)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

#if NETSTANDARD2_0
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > destination.Length)
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        bytes.AsSpan().CopyTo(destination);
        return bytes.Length;
#else
        if (!Encoding.ASCII.TryGetBytes(value, destination, out var bytesWritten))
        {
            throw new ArgumentException("Destination span is too small", nameof(destination));
        }

        return bytesWritten;
#endif
    }

    /// <summary>
    /// Attempts to encode the specified string as ASCII bytes and write the result to the provided destination buffer.
    /// </summary>
    /// <remarks>If the input string is null or empty, no bytes are written and the method returns true. The
    /// method returns false if the destination buffer is not large enough to hold the encoded bytes.</remarks>
    /// <param name="value">The string to encode as ASCII. Can be null or empty.</param>
    /// <param name="destination">The buffer that receives the ASCII-encoded bytes of the string.</param>
    /// <param name="bytesWritten">When this method returns, contains the number of bytes written to the destination buffer. Set to 0 if the input
    /// string is null or empty.</param>
    /// <returns>true if the string was successfully encoded and written to the destination buffer; otherwise, false.</returns>
    public static bool TryToSpan(string? value, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

#if NETSTANDARD2_0
        var bytes = Encoding.ASCII.GetBytes(value);
        if (bytes.Length > destination.Length)
        {
            return false;
        }

        bytes.AsSpan().CopyTo(destination);
        bytesWritten = bytes.Length;
        return true;
#else
        return Encoding.ASCII.TryGetBytes(value, destination, out bytesWritten);
#endif
    }
}
