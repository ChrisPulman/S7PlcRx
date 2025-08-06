// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;

namespace S7PlcRx.Core;

internal static class TypeConverter
{
    /// <summary>
    /// Converts an array of T to an array of bytes using a more efficient approach.
    /// </summary>
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
    /// Converts an array of T represented as S7 binary data to an array of T.
    /// </summary>
    public static T[] ToArray<T>(byte[] bytes, Func<byte[], T> converter)
        where T : struct => ToArray(bytes.AsSpan(), converter);

    /// <summary>
    /// Converts a span of bytes represented as S7 binary data to an array of T.
    /// </summary>
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
