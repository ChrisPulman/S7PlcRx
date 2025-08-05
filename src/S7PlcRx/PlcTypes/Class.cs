﻿// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Contains the methods to convert a C# class to S7 data types.
/// </summary>
public static class Class
{
    /// <summary>
    /// Gets the size of the class in bytes.
    /// </summary>
    /// <param name="instance">An instance of the class.</param>
    /// <param name="numBytes">The offset of the current field.</param>
    /// <param name="isInnerProperty"><see langword="true" /> if this property belongs to a class being serialized as member of the class requested for serialization; otherwise, <see langword="false" />.</param>
    /// <returns>the number of bytes.</returns>
    public static double GetClassSize(object instance, double numBytes = 0.0, bool isInnerProperty = false)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        var properties = GetAccessableProperties(instance.GetType());
        foreach (var property in properties)
        {
            if (property.PropertyType.IsArray)
            {
                var elementType = property.PropertyType.GetElementType()!;
                var array = (Array?)property.GetValue(instance, null) ??
                    throw new ArgumentException($"Property {property.Name} on {instance} must have a non-null value to get it's size.", nameof(instance));

                if (array.Length <= 0)
                {
                    throw new Exception("Cannot determine size of class, because an array is defined which has no fixed size greater than zero.");
                }

                IncrementToEven(ref numBytes);
                for (var i = 0; i < array.Length; i++)
                {
                    numBytes = GetIncreasedNumberOfBytes(numBytes, elementType, property);
                }
            }
            else
            {
                numBytes = GetIncreasedNumberOfBytes(numBytes, property.PropertyType, property);
            }
        }

        if (!isInnerProperty)
        {
            // enlarge numBytes to next even number because S7-Structs in a DB always will be resized to an even byte count
            numBytes = Math.Ceiling(numBytes);
            if ((numBytes / 2) > Math.Floor(numBytes / 2.0))
            {
                numBytes++;
            }
        }

        return numBytes;
    }

    /// <summary>
    /// Sets the object's values with the given array of bytes.
    /// </summary>
    /// <param name="sourceClass">The object to fill in the given array of bytes.</param>
    /// <param name="bytes">The array of bytes.</param>
    /// <param name="numBytes">The offset for the current field.</param>
    /// <param name="isInnerClass"><see langword="true" /> if this class is the type of a member of the class to be serialized; otherwise, <see langword="false" />.</param>
    /// <returns>A double.</returns>
    public static double FromBytes(object sourceClass, byte[] bytes, double numBytes = 0, bool isInnerClass = false)
    {
        if (bytes == null)
        {
            return numBytes;
        }

        if (sourceClass == null)
        {
            throw new ArgumentNullException(nameof(sourceClass));
        }

        var properties = GetAccessableProperties(sourceClass.GetType());
        foreach (var property in properties)
        {
            if (property.PropertyType.IsArray)
            {
                var array = (Array?)property.GetValue(sourceClass, null) ??
                    throw new ArgumentException($"Property {property.Name} on sourceClass must be an array instance.", nameof(sourceClass));

                IncrementToEven(ref numBytes);
                var elementType = property.PropertyType.GetElementType()!;
                for (var i = 0; i < array.Length && numBytes < bytes.Length; i++)
                {
                    array.SetValue(
                        GetPropertyValue(elementType, property, bytes, ref numBytes),
                        i);
                }
            }
            else
            {
                property.SetValue(
                    sourceClass,
                    GetPropertyValue(property.PropertyType, property, bytes, ref numBytes),
                    null);
            }
        }

        return numBytes;
    }

    /// <summary>
    /// Creates a byte array depending on the struct type.
    /// </summary>
    /// <param name="sourceClass">The struct object.</param>
    /// <param name="bytes">The target byte array.</param>
    /// <param name="numBytes">The offset for the current field.</param>
    /// <returns>A byte array or null if fails.</returns>
    public static double ToBytes(object sourceClass, byte[] bytes, double numBytes = 0.0)
    {
        if (sourceClass == null)
        {
            throw new ArgumentNullException(nameof(sourceClass));
        }

        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        var properties = GetAccessableProperties(sourceClass.GetType());
        foreach (var property in properties)
        {
            var value = property.GetValue(sourceClass, null) ??
                throw new ArgumentException($"Property {property.Name} on sourceClass can't be null.", nameof(sourceClass));

            if (property.PropertyType.IsArray)
            {
                var array = (Array)value;
                IncrementToEven(ref numBytes);
                for (var i = 0; i < array.Length && numBytes < bytes.Length; i++)
                {
                    numBytes = SetBytesFromProperty(array.GetValue(i)!, property, bytes, numBytes);
                }
            }
            else
            {
                numBytes = SetBytesFromProperty(value, property, bytes, numBytes);
            }
        }

        return numBytes;
    }

    private static IEnumerable<PropertyInfo> GetAccessableProperties(Type classType) => classType
            .GetProperties(
                BindingFlags.SetProperty |
                BindingFlags.Public |
                BindingFlags.Instance)
            .Where(p => p.GetSetMethod() != null);

    private static double GetIncreasedNumberOfBytes(double numBytes, Type type, PropertyInfo? propertyInfo)
    {
        switch (type.Name)
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
                IncrementToEven(ref numBytes);
                numBytes += 2;
                break;
            case "Int32":
            case "UInt32":
                IncrementToEven(ref numBytes);
                numBytes += 4;
                break;
            case "Single":
                IncrementToEven(ref numBytes);
                numBytes += 4;
                break;
            case "Double":
                IncrementToEven(ref numBytes);
                numBytes += 8;
                break;
            case "String":
                var attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                if (attribute == default(S7StringAttribute))
                {
                    throw new ArgumentException("Please add S7StringAttribute to the string field");
                }

                IncrementToEven(ref numBytes);
                numBytes += attribute.ReservedLengthInBytes;
                break;
            default:
                var propertyClass = Activator.CreateInstance(type) ??
                    throw new ArgumentException($"Failed to create instance of type {type}.", nameof(type));
                numBytes = GetClassSize(propertyClass, numBytes, true);
                break;
        }

        return numBytes;
    }

    private static object? GetPropertyValue(Type propertyType, PropertyInfo? propertyInfo, byte[] bytes, ref double numBytes)
    {
        object? value = null;

        switch (propertyType.Name)
        {
            case "Boolean":
                // get the value
                var bytePos = (int)Math.Floor(numBytes);
                var bitPos = (int)((numBytes - bytePos) / 0.125);
                value = (bytes[bytePos] & (int)Math.Pow(2, bitPos)) != 0;
                numBytes += 0.125;
                break;
            case "Byte":
                numBytes = Math.Ceiling(numBytes);
                value = bytes[(int)numBytes];
                numBytes++;
                break;
            case "Int16":
                IncrementToEven(ref numBytes);

                // hier auswerten
                var source = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                value = source.ConvertToShort();
                numBytes += 2;
                break;
            case "UInt16":
                IncrementToEven(ref numBytes);

                // hier auswerten
                value = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                numBytes += 2;
                break;
            case "Int32":
                IncrementToEven(ref numBytes);
                var wordBuffer = new byte[4];
                Array.Copy(bytes, (int)numBytes, wordBuffer, 0, wordBuffer.Length);
                var sourceUInt = DWord.FromByteArray(wordBuffer);
                value = sourceUInt.ConvertToInt();
                numBytes += 4;
                break;
            case "UInt32":
                IncrementToEven(ref numBytes);
                var wordBuffer2 = new byte[4];
                Array.Copy(bytes, (int)numBytes, wordBuffer2, 0, wordBuffer2.Length);
                value = DWord.FromByteArray(wordBuffer2);
                numBytes += 4;
                break;
            case "Single":
                IncrementToEven(ref numBytes);

                // hier auswerten
                value = Real.FromByteArray(
                    [
                        bytes[(int)numBytes],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 3]]);
                numBytes += 4;
                break;
            case "Double":
                IncrementToEven(ref numBytes);
                var buffer = new byte[8];
                Array.Copy(bytes, (int)numBytes, buffer, 0, 8);

                // hier auswerten
                value = LReal.FromByteArray(buffer);
                numBytes += 8;
                break;
            case "String":
                var attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                if (attribute == default(S7StringAttribute))
                {
                    throw new ArgumentException("Please add S7StringAttribute to the string field");
                }

                IncrementToEven(ref numBytes);

                // get the value
                var sData = new byte[attribute.ReservedLengthInBytes];
                Array.Copy(bytes, (int)numBytes, sData, 0, sData.Length);
                value = attribute.Type switch
                {
                    S7StringType.S7String => S7String.FromByteArray(sData),
                    S7StringType.S7WString => S7WString.FromByteArray(sData),
                    _ => throw new ArgumentException("Please use a valid string type for the S7StringAttribute")
                };
                numBytes += sData.Length;
                break;
            default:
                var propClass = Activator.CreateInstance(propertyType) ??
                    throw new ArgumentException($"Failed to create instance of type {propertyType}.", nameof(propertyType));

                numBytes = FromBytes(propClass, bytes, numBytes);
                value = propClass;
                break;
        }

        return value;
    }

    private static double SetBytesFromProperty(object propertyValue, PropertyInfo? propertyInfo, byte[] bytes, double numBytes)
    {
        byte[]? bytes2 = null;

        int bytePos;
        switch (propertyValue.GetType().Name)
        {
            case "Boolean":
                // get the value
                bytePos = (int)Math.Floor(numBytes);
                var bitPos = (int)((numBytes - bytePos) / 0.125);
                if ((bool)propertyValue)
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
                bytes[bytePos] = (byte)propertyValue;
                numBytes++;
                break;
            case "Int16":
                bytes2 = Int.ToByteArray((short)propertyValue);
                break;
            case "UInt16":
                bytes2 = Word.ToByteArray((ushort)propertyValue);
                break;
            case "Int32":
                bytes2 = DInt.ToByteArray((int)propertyValue);
                break;
            case "UInt32":
                bytes2 = DWord.ToByteArray((uint)propertyValue);
                break;
            case "Single":
                bytes2 = Real.ToByteArray((float)propertyValue);
                break;
            case "Double":
                bytes2 = LReal.ToByteArray((double)propertyValue);
                break;
            case "String":
                var attribute = propertyInfo?.GetCustomAttributes<S7StringAttribute>().SingleOrDefault();
                if (attribute == default(S7StringAttribute))
                {
                    throw new ArgumentException("Please add S7StringAttribute to the string field");
                }

                bytes2 = attribute.Type switch
                {
                    S7StringType.S7String => S7String.ToByteArray((string)propertyValue, attribute.ReservedLength),
                    S7StringType.S7WString => S7WString.ToByteArray((string)propertyValue, attribute.ReservedLength),
                    _ => throw new ArgumentException("Please use a valid string type for the S7StringAttribute")
                };
                break;
            default:
                numBytes = ToBytes(propertyValue, bytes, numBytes);
                break;
        }

        if (bytes2 != null)
        {
            IncrementToEven(ref numBytes);

            bytePos = (int)numBytes;
            for (var bCnt = 0; bCnt < bytes2.Length; bCnt++)
            {
                bytes[bytePos + bCnt] = bytes2[bCnt];
            }

            numBytes += bytes2.Length;
        }

        return numBytes;
    }

    private static void IncrementToEven(ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        if (numBytes % 2 > 0)
        {
            numBytes++;
        }
    }
}
