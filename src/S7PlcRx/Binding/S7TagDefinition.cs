// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Binding;

/// <summary>
/// Describes a generated PLC tag/property binding.
/// </summary>
public sealed class S7TagDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="S7TagDefinition"/> class.
    /// </summary>
    /// <param name="name">The property and PLC tag name.</param>
    /// <param name="address">The S7 DB address.</param>
    /// <param name="valueType">The .NET value type.</param>
    /// <param name="pollIntervalMs">The read polling interval in milliseconds.</param>
    /// <param name="direction">The tag access direction.</param>
    /// <param name="arrayLength">The array/string element length.</param>
    public S7TagDefinition(string name, string address, Type valueType, int pollIntervalMs, S7TagDirection direction, int arrayLength = 1)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentNullException(nameof(name)) : name;
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentNullException(nameof(address)) : address;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        PollIntervalMs = pollIntervalMs;
        Direction = direction;
        ArrayLength = Math.Max(1, arrayLength);
    }

    /// <summary>
    /// Gets the property and PLC tag name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the S7 DB address.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Gets the .NET value type.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets the read polling interval in milliseconds.
    /// </summary>
    public int PollIntervalMs { get; }

    /// <summary>
    /// Gets the tag access direction.
    /// </summary>
    public S7TagDirection Direction { get; }

    /// <summary>
    /// Gets the array/string element length.
    /// </summary>
    public int ArrayLength { get; }

    /// <summary>
    /// Gets a value indicating whether this tag should be read on polling intervals.
    /// </summary>
    public bool CanRead => Direction != S7TagDirection.WriteOnly && PollIntervalMs > 0;

    /// <summary>
    /// Gets a value indicating whether this tag can write property changes to the PLC.
    /// </summary>
    public bool CanWrite => Direction != S7TagDirection.ReadOnly;
}
