// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;

namespace S7PlcRx.Core;

/// <summary>
/// Provides utility methods for converting between arrays of value types and their byte representations.
/// </summary>
/// <remarks>The methods in this class are intended for efficient serialization and deserialization of arrays of
/// value types, particularly when working with binary data formats such as those used in S7 communication. All methods
/// require a user-supplied converter function to handle the specific conversion logic for the value type. This class is
/// not thread-safe.</remarks>
internal static class TypeConverter
{
    /// <summary>
    /// Converts an array of value types to a contiguous byte array using the specified conversion function.
    /// </summary>
    /// <remarks>The resulting byte array is constructed by concatenating the byte arrays returned by the
    /// converter for each element in the input array, in order. This method uses pooled buffers for large arrays to
    /// reduce memory allocations.</remarks>
    /// <typeparam name="T">The value type of the elements in the input array.</typeparam>
    /// <param name="value">The array of value type elements to convert. Must not be null.</param>
    /// <param name="converter">A function that converts each element of type T to its byte array representation. Cannot be null.</param>
    /// <returns>A byte array containing the concatenated byte representations of all elements in the input array. Returns an
    /// empty array if the input array is empty.</returns>
    public static byte[] ToByteArray<T>(T[] value, Func<T, byte[]> converter)
        where T : struct
    {
        if (value.Length == 0)
        {
            return [];
        }

        var typeSize = Marshal.SizeOf<T>();
        var totalSize = typeSize * value.Length;

        // Use ArrayPool for large allocations
        byte[]? pooledArray = null;
        var buffer = totalSize > 1024
            ? pooledArray = ArrayPool<byte>.Shared.Rent(totalSize)
            : new byte[totalSize];

        try
        {
            var position = 0;
            foreach (var val in value)
            {
                var bytes = converter(val);
                bytes.AsSpan().CopyTo(buffer.AsSpan(position));
                position += bytes.Length;
            }

            return buffer.AsSpan(0, totalSize).ToArray();
        }
        finally
        {
            if (pooledArray != null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    /// <summary>
    /// Converts a byte array to an array of value type elements using the specified converter function.
    /// </summary>
    /// <typeparam name="T">The value type to convert each byte array segment to.</typeparam>
    /// <param name="bytes">The byte array to convert. Cannot be null.</param>
    /// <param name="converter">A function that converts a byte array segment to an instance of type T. Cannot be null.</param>
    /// <returns>An array of type T containing the converted elements. The array will be empty if the input byte array is empty.</returns>
    public static T[] ToArray<T>(byte[] bytes, Func<byte[], T> converter)
        where T : struct => ToArray(bytes.AsSpan(), converter);

    /// <summary>
    /// Converts a read-only span of bytes into an array of value types by applying a converter function to each segment
    /// representing a single value.
    /// </summary>
    /// <remarks>The method divides the input span into segments, each the size of type T, and applies the
    /// converter function to each segment. If the length of bytes is not a multiple of the size of T, any remaining
    /// bytes are ignored.</remarks>
    /// <typeparam name="T">The value type to convert each segment of bytes into. Must be a struct.</typeparam>
    /// <param name="bytes">The read-only span of bytes to be converted. The length must be a multiple of the size of type T.</param>
    /// <param name="converter">A function that converts a byte array representing a single value of type T into an instance of T.</param>
    /// <returns>An array of type T containing the converted values from the input byte span.</returns>
    public static T[] ToArray<T>(ReadOnlySpan<byte> bytes, Func<byte[], T> converter)
        where T : struct
    {
        var typeSize = Marshal.SizeOf<T>();
        var entries = bytes.Length / typeSize;
        var values = new T[entries];

        for (var i = 0; i < entries; ++i)
        {
            var slice = bytes.Slice(i * typeSize, typeSize);
            var buffer = slice.ToArray(); // Only allocate for the converter function
            values[i] = converter(buffer);
        }

        return values;
    }
}
