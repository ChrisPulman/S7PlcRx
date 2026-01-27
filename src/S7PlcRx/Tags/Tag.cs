// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Represents a data tag with a name, address, value, type, and optional array length, typically used for storing or
/// transferring typed values identified by address or name.
/// </summary>
/// <remarks>The Tag class is commonly used to encapsulate a value along with its metadata, such as its address,
/// name, and type information. It supports both scalar and array values, as indicated by the ArrayLength property. The
/// DoNotPoll property can be used to control whether the tag should be excluded from polling operations in scenarios
/// such as industrial automation or data acquisition systems.</remarks>
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
    /// Gets or sets the address associated with the entity.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the name associated with the object.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the value associated with this instance.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets the new value associated with the change event.
    /// </summary>
    public object? NewValue { get; internal set; }

    /// <summary>
    /// Gets the runtime type information associated with the current instance.
    /// </summary>
    public Type Type { get; internal set; }

    /// <summary>
    /// Gets the length of the array, if known.
    /// </summary>
    public int? ArrayLength { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether polling operations should be suppressed for this instance.
    /// </summary>
    public bool DoNotPoll { get; internal set; }

    /// <summary>
    /// Sets a value indicating whether polling operations should be disabled.
    /// </summary>
    /// <param name="value">true to disable polling; otherwise, false.</param>
    public void SetDoNotPoll(bool value) => DoNotPoll = value;
}
