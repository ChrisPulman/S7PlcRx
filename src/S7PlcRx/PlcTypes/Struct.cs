// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enums;
#else
using S7PlcRx.Enums;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.PlcTypes;
#else
namespace S7PlcRx.PlcTypes;
#endif

/// <summary>
/// Provides utility methods for working with struct types, including calculating their size in bytes and converting
/// between structs and byte arrays.
/// </summary>
/// <remarks>The methods in this class are primarily intended for scenarios where struct data needs to be
/// serialized to or deserialized from byte arrays, such as communication with PLCs or binary protocols. The struct
/// types used with these methods should have public fields and, for string fields, must be decorated with the
/// S7StringAttribute to specify their encoding and length. All methods are static and thread-safe.</remarks>
public static class Struct
{
    /// <summary>The byte boundary required for S7 struct fields.</summary>
    private const int ByteAlignment = 2;

    /// <summary>The fraction of a byte occupied by one Boolean value.</summary>
    private const double BitSizeInBytes = 0.125;

    /// <summary>The offset of the third byte in a four-byte value.</summary>
    private const int ThirdByteOffset = 2;

    /// <summary>The offset of the fourth byte in a four-byte value.</summary>
    private const int FourthByteOffset = 3;

    /// <summary>
    /// Calculates the total size, in bytes, required to store an instance of the specified struct type, based on its
    /// fields and their types.
    /// </summary>
    /// <remarks>This method inspects the public fields of the provided struct type and calculates the size
    /// according to the field types, including handling of custom attributes such as S7StringAttribute for string
    /// fields. The calculation may not account for all platform-specific alignment or padding rules.</remarks>
    /// <param name="structType">The type of the struct for which to calculate the size. Must not be null.</param>
    /// <returns>The total size, in bytes, needed to represent an instance of the specified struct type.</returns>
    /// <exception cref="ArgumentNullException">Thrown if structType is null.</exception>
    /// <exception cref="ArgumentException">Thrown if a string field in the struct does not have the required S7StringAttribute.</exception>
    public static int GetStructSize(Type structType)
    {
        if (structType is null)
        {
            throw new ArgumentNullException(nameof(structType));
        }

        var numBytes = 0.0;

        foreach (var info in structType.GetFields())
        {
            numBytes = GetIncreasedNumberOfBytes(numBytes, info);
        }

        return (int)numBytes;
    }

    /// <summary>Deserializes a byte array into an instance of the specified structure type.</summary>
    /// <remarks>The method supports deserialization of structures containing fields of supported primitive
    /// types, strings with S7StringAttribute, and nested structures. All fields must be public. The structure's layout
    /// and field order must match the serialized byte format.</remarks>
    /// <param name="structType">The type of the structure to deserialize the byte array into. Must be a type with a parameterless constructor
    /// and supported field types.</param>
    /// <param name="bytes">The byte array containing the serialized data for the structure. The length must match the expected size of the
    /// structure.</param>
    /// <returns>An object representing the deserialized structure, or null if the byte array is null or does not match the
    /// expected size.</returns>
    /// <exception cref="ArgumentException">Thrown if an instance of the specified type cannot be created, or if a string field is missing the required
    /// S7StringAttribute, or if an invalid string type is specified for the S7StringAttribute.</exception>
    public static object? FromBytes(Type structType, byte[] bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        if (bytes.Length != GetStructSize(structType))
        {
            return null;
        }

        // and decode it
        var bytePos = 0;
        var bitPos = 0;
        var numBytes = 0.0;
        var structValue = Activator.CreateInstance(structType) ??
            throw new ArgumentException($"Failed to create an instance of the type {structType}.", nameof(structType));

        foreach (var info in GetStructFields(structValue.GetType()))
        {
            SetFieldValueFromBytes(info, structValue, bytes, ref bytePos, ref bitPos, ref numBytes);
        }

        return structValue;
    }

    /// <summary>Converts the specified structure object to its byte array representation.</summary>
    /// <remarks>Supported field types include Boolean, Byte, Int16, UInt16, Int32, UInt32, Single, Double,
    /// String (with S7StringAttribute), and TimeSpan. All fields of the structure must be of these types for successful
    /// conversion.</remarks>
    /// <param name="structValue">The structure object to convert to a byte array. Must not be null. The object's fields must be of supported
    /// types.</param>
    /// <returns>A byte array containing the serialized representation of the structure. Returns an empty array if <paramref
    /// name="structValue"/> is null.</returns>
    /// <exception cref="ArgumentException">Thrown if a field value cannot be converted to its corresponding type, or if a string field is missing the
    /// required S7StringAttribute, or if an invalid string type is specified in the S7StringAttribute.</exception>
    public static byte[] ToBytes(object structValue)
    {
        if (structValue is null)
        {
            return [];
        }

        var type = structValue.GetType();

        var size = Struct.GetStructSize(type);
        var bytes = new byte[size];
        var bytePos = 0;
        var numBytes = 0.0;

        foreach (var info in GetStructFields(type))
        {
            var fieldBytes = GetFieldBytes(info, structValue, bytes, ref bytePos, ref numBytes);
            if (fieldBytes is not null)
            {
                WriteAlignedBytes(fieldBytes, bytes, ref bytePos, ref numBytes);
            }
        }

        return bytes;
    }

    /// <summary>Calculates the byte count after adding a field.</summary>
    /// <param name="numBytes">The current byte count.</param>
    /// <param name="info">The field metadata.</param>
    /// <returns>The updated byte count.</returns>
    private static double GetIncreasedNumberOfBytes(double numBytes, FieldInfo info)
    {
        switch (info.FieldType.Name)
        {
            case "Boolean":
                return numBytes + BitSizeInBytes;
            case "Byte":
                return Math.Ceiling(numBytes) + 1;
            case "Int16" or "UInt16":
                {
                    IncrementToEven(ref numBytes);
                    return numBytes + sizeof(short);
                }

            case "Int32" or "UInt32" or "Single" or "TimeSpan":
                {
                    IncrementToEven(ref numBytes);
                    return numBytes + sizeof(int);
                }

            case "Double":
                {
                    IncrementToEven(ref numBytes);
                    return numBytes + sizeof(double);
                }

            case "String":
                {
                    IncrementToEven(ref numBytes);
                    return numBytes + GetRequiredStringAttribute(info).ReservedLengthInBytes;
                }

            default:
                return numBytes + GetStructSize(info.FieldType);
        }
    }

    /// <summary>Returns the public fields for a struct type.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The field metadata collection.</returns>
    private static FieldInfo[] GetStructFields(Type type)
    {
#if NETSTANDARD1_3
        var fields = new List<FieldInfo>();
        foreach (var field in type.GetTypeInfo().DeclaredFields)
        {
            fields.Add(field);
        }

        return fields.ToArray();
#else
        return type.GetFields();
#endif
    }

    /// <summary>Assigns a field value from the supplied byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="bitPos">The current bit position.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetFieldValueFromBytes(
        FieldInfo info,
        object structValue,
        byte[] bytes,
        ref int bytePos,
        ref int bitPos,
        ref double numBytes)
    {
        switch (info.FieldType.Name)
        {
            case "Boolean":
                {
                    SetBooleanFieldFromBytes(info, structValue, bytes, ref bytePos, ref bitPos, ref numBytes);
                    break;
                }

            case "Byte":
                {
                    SetByteFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "Int16":
                {
                    info.SetValue(structValue, ReadWord(bytes, ref numBytes).ConvertToShort());
                    break;
                }

            case "UInt16":
                {
                    info.SetValue(structValue, ReadWord(bytes, ref numBytes));
                    break;
                }

            case "Int32":
                {
                    SetInt32FieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "UInt32":
                {
                    SetUInt32FieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "Single":
                {
                    SetSingleFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "Double":
                {
                    SetDoubleFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "String":
                {
                    SetStringFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            case "TimeSpan":
                {
                    SetTimeSpanFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }

            default:
                {
                    SetNestedFieldFromBytes(info, structValue, bytes, ref numBytes);
                    break;
                }
        }
    }

    /// <summary>Sets a Boolean field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="bitPos">The current bit position.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetBooleanFieldFromBytes(
        FieldInfo info,
        object structValue,
        byte[] bytes,
        ref int bytePos,
        ref int bitPos,
        ref double numBytes)
    {
        bytePos = (int)Math.Floor(numBytes);
        bitPos = (int)((numBytes - bytePos) / BitSizeInBytes);
        info.SetValue(structValue, (bytes[bytePos] & (1 << bitPos)) != 0);
        numBytes += BitSizeInBytes;
    }

    /// <summary>Sets a byte field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetByteFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        info.SetValue(structValue, bytes[(int)numBytes]);
        numBytes++;
    }

    /// <summary>Reads a word from a byte buffer.</summary>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    /// <returns>The word value.</returns>
    private static ushort ReadWord(byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var value = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
        numBytes += sizeof(ushort);
        return value;
    }

    /// <summary>Sets a signed 32-bit integer field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetInt32FieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var sourceUInt = DWord.FromBytes(
            bytes[(int)numBytes + FourthByteOffset],
            bytes[(int)numBytes + ThirdByteOffset],
            bytes[(int)numBytes + 1],
            bytes[(int)numBytes]);
        info.SetValue(structValue, sourceUInt.ConvertToInt());
        numBytes += sizeof(int);
    }

    /// <summary>Sets an unsigned 32-bit integer field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetUInt32FieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        info.SetValue(structValue, DWord.FromBytes(
            bytes[(int)numBytes],
            bytes[(int)numBytes + 1],
            bytes[(int)numBytes + ThirdByteOffset],
            bytes[(int)numBytes + FourthByteOffset]));
        numBytes += sizeof(uint);
    }

    /// <summary>Sets a single precision field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetSingleFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        info.SetValue(structValue, Real.FromSpan(bytes.AsSpan((int)numBytes, sizeof(float))));
        numBytes += sizeof(float);
    }

    /// <summary>Sets a double precision field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetDoubleFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        var data = new byte[sizeof(double)];
        Array.Copy(bytes, (int)numBytes, data, 0, data.Length);
        info.SetValue(structValue, LReal.FromByteArray(data));
        numBytes += sizeof(double);
    }

    /// <summary>Sets a string field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetStringFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        var attribute = GetRequiredStringAttribute(info);
        IncrementToEven(ref numBytes);
        var stringData = new byte[attribute.ReservedLengthInBytes];
        Array.Copy(bytes, (int)numBytes, stringData, 0, stringData.Length);
        info.SetValue(structValue, attribute.Type switch
        {
            S7StringType.S7String => S7String.FromByteArray(stringData),
            S7StringType.S7WString => S7WString.FromByteArray(stringData),
            _ => throw new ArgumentException("Please use a valid string type for the S7StringAttribute")
        });
        numBytes += stringData.Length;
    }

    /// <summary>Sets a time span field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetTimeSpanFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        info.SetValue(structValue, TimeSpan.FromSpan(bytes.AsSpan((int)numBytes, sizeof(int))));
        numBytes += sizeof(int);
    }

    /// <summary>Sets a nested struct field from a byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being populated.</param>
    /// <param name="bytes">The source bytes.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetNestedFieldFromBytes(FieldInfo info, object structValue, byte[] bytes, ref double numBytes)
    {
        var buffer = new byte[GetStructSize(info.FieldType)];
        if (buffer.Length == 0)
        {
            return;
        }

        Buffer.BlockCopy(bytes, (int)Math.Ceiling(numBytes), buffer, 0, buffer.Length);
        info.SetValue(structValue, FromBytes(info.FieldType, buffer));
        numBytes += buffer.Length;
    }

    /// <summary>Gets serialized bytes for a field, or writes the field directly for bit and byte values.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being serialized.</param>
    /// <param name="bytes">The destination bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="numBytes">The current byte count.</param>
    /// <returns>The serialized field bytes, or null when the field was written directly.</returns>
    private static byte[]? GetFieldBytes(FieldInfo info, object structValue, byte[] bytes, ref int bytePos, ref double numBytes)
    {
        switch (info.FieldType.Name)
        {
            case "Boolean":
                {
                    SetBooleanFieldBytes(info, structValue, bytes, ref bytePos, ref numBytes);
                    return null;
                }

            case "Byte":
                {
                    SetByteFieldBytes(info, structValue, bytes, ref bytePos, ref numBytes);
                    return null;
                }

            case "Int16":
                return Int.ToByteArray(GetValueOrThrow<short>(info, structValue));
            case "UInt16":
                return Word.ToByteArray(GetValueOrThrow<ushort>(info, structValue));
            case "Int32":
                return DInt.ToByteArray(GetValueOrThrow<int>(info, structValue));
            case "UInt32":
                return DWord.ToByteArray(GetValueOrThrow<uint>(info, structValue));
            case "Single":
                return Real.ToByteArray(GetValueOrThrow<float>(info, structValue));
            case "Double":
                return LReal.ToByteArray(GetValueOrThrow<double>(info, structValue));
            case "String":
                return GetStringFieldBytes(info, structValue);
            case "TimeSpan":
                return TimeSpan.ToByteArray((System.TimeSpan)info.GetValue(structValue)!);
            default:
                return null;
        }
    }

    /// <summary>Sets a Boolean field in the destination byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being serialized.</param>
    /// <param name="bytes">The destination bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetBooleanFieldBytes(FieldInfo info, object structValue, byte[] bytes, ref int bytePos, ref double numBytes)
    {
        var bitPos = (int)((numBytes - bytePos) / BitSizeInBytes);
        if (GetValueOrThrow<bool>(info, structValue))
        {
            bytes[bytePos] |= (byte)(1 << bitPos); // is true
        }
        else
        {
            bytes[bytePos] &= (byte)~(1 << bitPos); // is false
        }

        numBytes += BitSizeInBytes;
    }

    /// <summary>Sets a byte field in the destination byte buffer.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being serialized.</param>
    /// <param name="bytes">The destination bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void SetByteFieldBytes(FieldInfo info, object structValue, byte[] bytes, ref int bytePos, ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        bytePos = (int)numBytes;
        bytes[bytePos] = GetValueOrThrow<byte>(info, structValue);
        numBytes++;
    }

    /// <summary>Gets serialized bytes for a string field.</summary>
    /// <param name="info">The field metadata.</param>
    /// <param name="structValue">The struct instance being serialized.</param>
    /// <returns>The serialized string bytes.</returns>
    private static byte[] GetStringFieldBytes(FieldInfo info, object structValue)
    {
        var attribute = GetRequiredStringAttribute(info);
        return attribute.Type switch
        {
            S7StringType.S7String => S7String.ToByteArray((string?)info.GetValue(structValue), attribute.ReservedLength),
            S7StringType.S7WString => S7WString.ToByteArray((string?)info.GetValue(structValue), attribute.ReservedLength),
            _ => throw new ArgumentException("Please use a valid string type for the S7StringAttribute")
        };
    }

    /// <summary>Writes byte-aligned field bytes into the destination buffer.</summary>
    /// <param name="source">The source bytes.</param>
    /// <param name="destination">The destination bytes.</param>
    /// <param name="bytePos">The current byte position.</param>
    /// <param name="numBytes">The current byte count.</param>
    private static void WriteAlignedBytes(byte[] source, byte[] destination, ref int bytePos, ref double numBytes)
    {
        IncrementToEven(ref numBytes);
        bytePos = (int)numBytes;
        source.CopyTo(destination, bytePos);
        numBytes += source.Length;
    }

    /// <summary>Gets a non-null field value or throws.</summary>
    /// <typeparam name="TValue">The field value type.</typeparam>
    /// <param name="fieldInfo">The field metadata.</param>
    /// <param name="structValue">The struct instance.</param>
    /// <returns>The field value.</returns>
    private static TValue GetValueOrThrow<TValue>(FieldInfo fieldInfo, object structValue)
        where TValue : struct => (fieldInfo.GetValue(structValue) as TValue?) ??
            throw new ArgumentException($"Failed to convert value of field {fieldInfo.Name} of {structValue} to type {typeof(TValue)}");

    /// <summary>Gets the required S7 string attribute for a field.</summary>
    /// <param name="fieldInfo">The field metadata.</param>
    /// <returns>The required S7 string attribute.</returns>
    private static S7StringAttribute GetRequiredStringAttribute(FieldInfo fieldInfo)
    {
        var attribute = GetS7StringAttribute(fieldInfo);
        return attribute ?? throw new ArgumentException("Please add S7StringAttribute to the string field");
    }

    /// <summary>Gets the S7 string attribute for a field.</summary>
    /// <param name="fieldInfo">The field metadata.</param>
    /// <returns>The S7 string attribute, or null when one is not present.</returns>
    private static S7StringAttribute? GetS7StringAttribute(FieldInfo fieldInfo)
    {
        S7StringAttribute? result = null;
        foreach (var attribute in fieldInfo.GetCustomAttributes<S7StringAttribute>())
        {
            if (result is not null)
            {
                throw new InvalidOperationException($"Multiple {nameof(S7StringAttribute)} attributes were found on {fieldInfo.Name}.");
            }

            result = attribute;
        }

        return result;
    }

    /// <summary>Rounds the specified value up to the nearest even integer.</summary>
    /// <param name="numBytes">The value to round.</param>
    private static void IncrementToEven(ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        if (numBytes % ByteAlignment == 0)
        {
            return;
        }

        numBytes++;
    }
}
