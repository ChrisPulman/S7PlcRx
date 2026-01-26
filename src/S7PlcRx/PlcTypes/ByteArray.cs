// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides a dynamically sized buffer for accumulating bytes, with efficient memory management using array pooling.
/// </summary>
/// <remarks>The buffer automatically grows as data is added. The internal array is rented from the shared array
/// pool and returned when disposed. This class is not thread-safe.</remarks>
/// <param name="size">The initial capacity of the internal buffer, in bytes. Must be greater than zero.</param>
internal class ByteArray(int size) : IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(size);
    private int _position;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteArray"/> class with a default capacity of 32 bytes.
    /// </summary>
    /// <remarks>This constructor is useful when the required initial capacity is not known in advance. The
    /// internal buffer will automatically expand as needed when additional bytes are added.</remarks>
    public ByteArray()
        : this(32)
    {
    }

    /// <summary>
    /// Gets the current data as a span.
    /// </summary>
    /// <value>The current data as a span.</value>
    public ReadOnlySpan<byte> Span => _buffer.AsSpan(0, _position);

    /// <summary>
    /// Gets the current data as memory.
    /// </summary>
    /// <value>The current data as memory.</value>
    public ReadOnlyMemory<byte> Memory => _buffer.AsMemory(0, _position);

    /// <summary>
    /// Gets the array. Use Span property for better performance when possible.
    /// </summary>
    /// <value>The array.</value>
    public byte[] Array => Span.ToArray();

    /// <summary>
    /// Gets the current position (length of data).
    /// </summary>
    public int Length => _position;

    /// <summary>
    /// Adds a byte value to the end of the buffer.
    /// </summary>
    /// <param name="item">The byte value to add to the buffer.</param>
    public void Add(byte item)
    {
        EnsureCapacity(_position + 1);
        _buffer[_position++] = item;
    }

    /// <summary>
    /// Adds the specified sequence of bytes to the buffer.
    /// </summary>
    /// <remarks>The buffer is automatically resized if necessary to accommodate the new items. The method
    /// does not throw an exception if the span is empty; in that case, the buffer remains unchanged.</remarks>
    /// <param name="items">A read-only span containing the bytes to add. If empty, no action is taken.</param>
    public void Add(ReadOnlySpan<byte> items)
    {
        if (items.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_position + items.Length);
        items.CopyTo(_buffer.AsSpan(_position));
        _position += items.Length;
    }

    /// <summary>
    /// Adds the specified array of bytes to the collection.
    /// </summary>
    /// <param name="items">An array of bytes to add. Cannot be null.</param>
    public void Add(byte[] items) => Add(items.AsSpan());

    /// <summary>
    /// Adds the contents of the specified <see cref="ByteArray"/> to the collection.
    /// </summary>
    /// <param name="byteArray">The <see cref="ByteArray"/> instance whose contents will be added. Cannot be null.</param>
    public void Add(ByteArray byteArray) => Add(byteArray.Span);

    /// <summary>
    /// Resets the current position to the beginning, effectively clearing any progress or state tracked by the
    /// instance.
    /// </summary>
    public void Clear() => _position = 0;

    /// <summary>
    /// Attempts to copy the written bytes to the specified destination buffer.
    /// </summary>
    /// <remarks>No data is copied if the destination buffer is too small. The method does not modify the
    /// destination buffer if it returns false.</remarks>
    /// <param name="destination">The buffer to which the written bytes will be copied. Must have a length greater than or equal to the number of
    /// bytes written.</param>
    /// <returns>true if the copy operation succeeds; otherwise, false.</returns>
    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length < _position)
        {
            return false;
        }

        Span.CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting
    /// unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release
    /// only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null!;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Ensures that the internal buffer has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <remarks>If the current buffer is smaller than the required capacity, a larger buffer is allocated and
    /// existing data is copied to it. The previous buffer is returned to the shared array pool. This method is intended
    /// for internal use to optimize buffer management and reduce allocations.</remarks>
    /// <param name="required">The minimum required capacity of the internal buffer. Must be greater than zero.</param>
    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        var newCapacity = Math.Max(required, _buffer.Length * 2);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);

        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }
}
