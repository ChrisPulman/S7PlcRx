// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Initializes a new instance of the <see cref="ByteArray"/> class.
/// </summary>
/// <param name="size">The initial capacity.</param>
internal class ByteArray(int size) : IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(size);
    private int _position;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteArray"/> class.
    /// </summary>
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
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(byte item)
    {
        EnsureCapacity(_position + 1);
        _buffer[_position++] = item;
    }

    /// <summary>
    /// Adds the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
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
    /// Adds the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void Add(byte[] items) => Add(items.AsSpan());

    /// <summary>
    /// Adds the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    public void Add(ByteArray byteArray) => Add(byteArray.Span);

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear() => _position = 0;

    /// <summary>
    /// Copies data to the specified destination span.
    /// </summary>
    /// <param name="destination">The destination span.</param>
    /// <returns>True if the copy was successful, false if the destination is too small.</returns>
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
