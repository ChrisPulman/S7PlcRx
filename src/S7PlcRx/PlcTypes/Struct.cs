// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

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
        if (structType == null)
        {
            throw new ArgumentNullException(nameof(structType));
        }

        var numBytes = 0.0;

        foreach (var info in structType.GetFields())
        {
            switch (info.FieldType.Name)
            {
                case "Boolean":
                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    numBytes++;
                    break;
                case "Int16":
                case "UInt16":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    numBytes += 2;
                    break;
                case "Int32":
                case "UInt32":
                case "TimeSpan":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    numBytes += 4;
                    break;
                case "Single":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    numBytes += 4;
                    break;
                case "Double":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    numBytes += 8;
                    break;
                case "String":
                    var attribute = info.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                    if (attribute == default(S7StringAttribute))
                    {
                        throw new ArgumentException("Please add S7StringAttribute to the string field");
                    }

                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    numBytes += attribute.ReservedLengthInBytes;
                    break;
                default:
                    numBytes += GetStructSize(info.FieldType);
                    break;
            }
        }

        return (int)numBytes;
    }

    /// <summary>
    /// Deserializes a byte array into an instance of the specified structure type.
    /// </summary>
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
        if (bytes == null)
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

        var infos = structValue.GetType()
#if NETSTANDARD1_3
                .GetTypeInfo().DeclaredFields;
#else
            .GetFields();
#endif

        foreach (var info in infos)
        {
            switch (info.FieldType.Name)
            {
                case "Boolean":
                    // get the value
                    bytePos = (int)Math.Floor(numBytes);
                    bitPos = (int)((numBytes - (double)bytePos) / 0.125);
                    if ((bytes[bytePos] & (int)Math.Pow(2, bitPos)) != 0)
                    {
                        info.SetValue(structValue, true);
                    }
                    else
                    {
                        info.SetValue(structValue, false);
                    }

                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    info.SetValue(structValue, (byte)bytes[(int)numBytes]);
                    numBytes++;
                    break;
                case "Int16":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    var source = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                    info.SetValue(structValue, source.ConvertToShort());
                    numBytes += 2;
                    break;
                case "UInt16":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    info.SetValue(structValue, Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]));
                    numBytes += 2;
                    break;
                case "Int32":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    var sourceUInt = DWord.FromBytes(
                        bytes[(int)numBytes + 3],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 0]);
                    info.SetValue(structValue, sourceUInt.ConvertToInt());
                    numBytes += 4;
                    break;
                case "UInt32":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    info.SetValue(structValue, DWord.FromBytes(
                        bytes[(int)numBytes],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 3]));
                    numBytes += 4;
                    break;
                case "Single":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    info.SetValue(structValue, Real.FromByteArray(
                        [bytes[(int)numBytes],
                            bytes[(int)numBytes + 1],
                            bytes[(int)numBytes + 2],
                            bytes[(int)numBytes + 3]]));
                    numBytes += 4;
                    break;
                case "Double":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    var data = new byte[8];
                    Array.Copy(bytes, (int)numBytes, data, 0, 8);
                    info.SetValue(structValue, LReal.FromByteArray(data));
                    numBytes += 8;
                    break;
                case "String":
                    var attribute = info.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                    if (attribute == default(S7StringAttribute))
                    {
                        throw new ArgumentException("Please add S7StringAttribute to the string field");
                    }

                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    var sData = new byte[attribute.ReservedLengthInBytes];
                    Array.Copy(bytes, (int)numBytes, sData, 0, sData.Length);
                    switch (attribute.Type)
                    {
                        case S7StringType.S7String:
                            info.SetValue(structValue, S7String.FromByteArray(sData));
                            break;
                        case S7StringType.S7WString:
                            info.SetValue(structValue, S7WString.FromByteArray(sData));
                            break;
                        default:
                            throw new ArgumentException("Please use a valid string type for the S7StringAttribute");
                    }

                    numBytes += sData.Length;
                    break;
                case "TimeSpan":
                    numBytes = Math.Ceiling(numBytes);
                    if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                    {
                        numBytes++;
                    }

                    // get the value
                    info.SetValue(structValue, TimeSpan.FromByteArray(
                    [
                        bytes[(int)numBytes + 0],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 3]
                    ]));
                    numBytes += 4;
                    break;
                default:
                    var buffer = new byte[GetStructSize(info.FieldType)];
                    if (buffer.Length == 0)
                    {
                        continue;
                    }

                    Buffer.BlockCopy(bytes, (int)Math.Ceiling(numBytes), buffer, 0, buffer.Length);
                    info.SetValue(structValue, FromBytes(info.FieldType, buffer));
                    numBytes += buffer.Length;
                    break;
            }
        }

        return structValue;
    }

    /// <summary>
    /// Converts the specified structure object to its byte array representation.
    /// </summary>
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
        if (structValue == null)
        {
            return [];
        }

        var type = structValue.GetType();

        var size = Struct.GetStructSize(type);
        var bytes = new byte[size];
        byte[]? bytes2 = null;

        var bytePos = 0;
        var numBytes = 0.0;

        foreach (var info in type.GetFields())
        {
            static TValue GetValueOrThrow<TValue>(FieldInfo fi, object structValue)
                where TValue : struct => fi.GetValue(structValue) as TValue? ??
                    throw new ArgumentException($"Failed to convert value of field {fi.Name} of {structValue} to type {typeof(TValue)}");

            bytes2 = null;
            switch (info.FieldType.Name)
            {
                case "Boolean":
                    // get the value
                    var bitPos = (int)Math.Floor(numBytes);
                    bitPos = (int)((numBytes - bytePos) / 0.125);
                    if (GetValueOrThrow<bool>(info, structValue))
                    {
                        bytes[bytePos] |= (byte)Math.Pow(2, bitPos);            // is true
                    }
                    else
                    {
                        bytes[bytePos] &= (byte)(~(byte)Math.Pow(2, bitPos));   // is false
                    }

                    numBytes += 0.125;
                    break;
                case "Byte":
                    numBytes = (int)Math.Ceiling(numBytes);
                    bytePos = (int)numBytes;
                    bytes[bytePos] = GetValueOrThrow<byte>(info, structValue);
                    numBytes++;
                    break;
                case "Int16":
                    bytes2 = Int.ToByteArray(GetValueOrThrow<short>(info, structValue));
                    break;
                case "UInt16":
                    bytes2 = Word.ToByteArray(GetValueOrThrow<ushort>(info, structValue));
                    break;
                case "Int32":
                    bytes2 = DInt.ToByteArray(GetValueOrThrow<int>(info, structValue));
                    break;
                case "UInt32":
                    bytes2 = DWord.ToByteArray(GetValueOrThrow<uint>(info, structValue));
                    break;
                case "Single":
                    bytes2 = Real.ToByteArray(GetValueOrThrow<float>(info, structValue));
                    break;
                case "Double":
                    bytes2 = LReal.ToByteArray(GetValueOrThrow<double>(info, structValue));
                    break;
                case "String":
                    var attribute = info.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                    if (attribute == default(S7StringAttribute))
                    {
                        throw new ArgumentException("Please add S7StringAttribute to the string field");
                    }

                    bytes2 = attribute.Type switch
                    {
                        S7StringType.S7String => S7String.ToByteArray((string?)info.GetValue(structValue), attribute.ReservedLength),
                        S7StringType.S7WString => S7WString.ToByteArray((string?)info.GetValue(structValue), attribute.ReservedLength),
                        _ => throw new ArgumentException("Please use a valid string type for the S7StringAttribute")
                    };
                    break;
                case "TimeSpan":
                    bytes2 = TimeSpan.ToByteArray((System.TimeSpan)info.GetValue(structValue)!);
                    break;
            }

            if (bytes2 != null)
            {
                // add them
                numBytes = Math.Ceiling(numBytes);
                if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
                {
                    numBytes++;
                }

                bytePos = (int)numBytes;
                for (var bCnt = 0; bCnt < bytes2.Length; bCnt++)
                {
                    bytes[bytePos + bCnt] = bytes2[bCnt];
                }

                numBytes += bytes2.Length;
            }
        }

        return bytes;
    }
}
