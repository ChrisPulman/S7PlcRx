// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

internal class ByteArray
{
    private List<byte> _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteArray"/> class.
    /// </summary>
    public ByteArray() => _list = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteArray"/> class.
    /// </summary>
    /// <param name="size">The size.</param>
    public ByteArray(int size) => _list = new(size);

    /// <summary>
    /// Gets the array.
    /// </summary>
    /// <value>The array.</value>
    public byte[] Array => _list.ToArray();

    /// <summary>
    /// Adds the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    public void Add(byte item) => _list.Add(item);

    /// <summary>
    /// Adds the specified items.
    /// </summary>
    /// <param name="items">The items.</param>
    public void Add(byte[] items) => _list.AddRange(items);

    /// <summary>
    /// Adds the specified byte array.
    /// </summary>
    /// <param name="byteArray">The byte array.</param>
    public void Add(ByteArray byteArray) => _list.AddRange(byteArray.Array);

    /// <summary>
    /// Clears this instance.
    /// </summary>
    public void Clear() => _list = [];
}
