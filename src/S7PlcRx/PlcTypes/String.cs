// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// String.
/// </summary>
internal static class String
{
    /// <summary>
    /// From the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A string.</returns>
    public static string FromByteArray(byte[] bytes) => FromSpan(bytes.AsSpan());

    /// <summary>
    /// Converts bytes from span to string.
    /// </summary>
    /// <param name="bytes">The bytes span.</param>
    /// <returns>A string.</returns>
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
    /// Froms the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    /// <returns>A string.</returns>
    public static string FromByteArray(byte[] bytes, int start, int length)
    {
        if (bytes.Length < start + length)
        {
            return string.Empty;
        }

        return FromSpan(bytes.AsSpan(start, length));
    }

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
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
    /// Writes string to the specified span as ASCII bytes.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="destination">The destination span.</param>
    /// <returns>The number of bytes written.</returns>
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
    /// Tries to write string to the specified span as ASCII bytes.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns>True if successful, false if the destination is too small.</returns>
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
