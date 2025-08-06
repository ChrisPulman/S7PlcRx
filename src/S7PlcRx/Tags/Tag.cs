// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Tag for PLC.
/// </summary>
[Serializable]
public class Tag : ITag
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Tag"/> class.
    /// </summary>
    public Tag()
    {
        Name = string.Empty;
        Address = string.Empty;
        Value = new();
        Type = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag" /> class.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="type">The type.</param>
    public Tag(string address, Type type)
    {
        Name = address;
        Address = address;
        Value = new();
        Type = type;
        ArrayLength = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag"/> class.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="type">The type.</param>
    /// <param name="arrayLength">Length of the array.</param>
    public Tag(string address, Type type, int arrayLength)
    {
        Name = address;
        Address = address;
        Value = new();
        Type = type;
        ArrayLength = arrayLength;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="address">The address.</param>
    /// <param name="type">The type.</param>
    public Tag(string name, string address, Type type)
    {
        Name = name;
        Address = address;
        Value = new();
        Type = type;
        ArrayLength = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="address">The address.</param>
    /// <param name="type">The type.</param>
    /// <param name="arrayLength">Length of the array.</param>
    public Tag(string name, string address, Type type, int arrayLength)
    {
        Name = name;
        Address = address;
        Value = new();
        Type = type;
        ArrayLength = arrayLength;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tag" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="address">The address.</param>
    /// <param name="value">The value.</param>
    /// <param name="type">The type.</param>
    public Tag(string name, string address, object value, Type type)
    {
        Name = name;
        Address = address;
        Value = value;
        Type = type;
        ArrayLength = 1;
    }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    /// <value>The address.</value>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    /// <value>The value.</value>
    public object? Value { get; set; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>
    /// The new value.
    /// </value>
    public object? NewValue { get; internal set; }

    /// <summary>
    /// Gets the type.
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public Type Type { get; internal set; }

    /// <summary>
    /// Gets the length of the array.
    /// </summary>
    /// <value>
    /// The length of the array.
    /// </value>
    public int? ArrayLength { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether [do not poll].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [do not poll]; otherwise, <c>false</c>.
    /// </value>
    public bool DoNotPoll { get; internal set; }

    /// <summary>
    /// Sets the do not poll.
    /// </summary>
    /// <param name="value">if set to <c>true</c> [value].</param>
    public void SetDoNotPoll(bool value) => DoNotPoll = value;
}
