// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Specifies metadata for mapping a field or property to an S7 string type with a defined reserved length.
/// </summary>
/// <remarks>Apply this attribute to a field or property to indicate how it should be represented as an S7 string
/// in communication with Siemens S7 PLCs. The attribute defines both the S7 string type and the reserved length, which
/// are used for serialization and deserialization. Only one instance of this attribute can be applied to a given field
/// or property.</remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class S7StringAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="S7StringAttribute"/> class with the specified string type and reserved length.
    /// </summary>
    /// <param name="type">The type of S7 string to use. Must be a defined value of the S7StringType enumeration.</param>
    /// <param name="reservedLength">The reserved length for the string. Specifies the maximum number of characters the string can hold.</param>
    /// <exception cref="ArgumentException">Thrown if the specified type is not a valid value of the S7StringType enumeration.</exception>
    public S7StringAttribute(S7StringType type, int reservedLength)
    {
        if (!Enum.IsDefined(typeof(S7StringType), type))
        {
            throw new ArgumentException("Please use a valid value for the string type");
        }

        Type = type;
        ReservedLength = reservedLength;
    }

    /// <summary>
    /// Gets the type of the S7 string represented by this instance.
    /// </summary>
    public S7StringType Type { get; }

    /// <summary>
    /// Gets the number of characters reserved for the value.
    /// </summary>
    public int ReservedLength { get; }

    /// <summary>
    /// Gets the total number of bytes reserved for the string, including any protocol-specific header or length fields.
    /// </summary>
    /// <remarks>The reserved length in bytes depends on the string type. For S7String, the value includes 2
    /// bytes for header information; for S7WString, it includes 4 bytes for header information and accounts for UTF-16
    /// encoding. This value is typically used to allocate buffers or validate data boundaries when working with S7
    /// string types.</remarks>
    public int ReservedLengthInBytes => Type == S7StringType.S7String ? ReservedLength + 2 : (ReservedLength * 2) + 4;
}
