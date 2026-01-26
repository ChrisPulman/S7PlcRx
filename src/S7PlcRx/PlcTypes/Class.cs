// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using S7PlcRx.Enums;

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Provides static methods for serializing and deserializing class and struct instances to and from byte arrays, as
/// well as calculating the size of a class in bytes for serialization purposes.
/// </summary>
/// <remarks>This class is intended for scenarios where objects need to be converted to a byte representation,
/// such as communication with PLCs or other systems requiring structured binary formats. All methods operate statically
/// and require the caller to supply instances and byte arrays as needed. Properties within serialized classes must be
/// accessible and, for string fields, decorated with the appropriate S7StringAttribute. Methods may throw exceptions if
/// required attributes are missing or if input values are invalid. Thread safety is not guaranteed; callers should
/// ensure appropriate synchronization if accessing shared objects.</remarks>
public static class Class
{
    /// <summary>
    /// Calculates the total size, in bytes, of the specified object's accessible properties, including arrays and
    /// nested properties as applicable.
    /// </summary>
    /// <remarks>This method inspects the public properties of the object's type to determine the total size.
    /// Array properties must have a non-null value and a length greater than zero. The calculation accounts for
    /// S7-Struct alignment by rounding up to the next even byte count unless calculating for an inner
    /// property.</remarks>
    /// <param name="instance">The object instance whose class size is to be calculated. Cannot be null.</param>
    /// <param name="numBytes">The initial byte count to start the calculation from. Typically set to 0.0 for a new calculation.</param>
    /// <param name="isInnerProperty">Indicates whether the calculation is for an inner property. If <see langword="false"/>, the result is rounded up
    /// to the next even byte count to match S7-Struct alignment requirements.</param>
    /// <returns>The total size, in bytes, of the object's accessible properties. The value is rounded up to the next even number
    /// if <paramref name="isInnerProperty"/> is <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="instance"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if an array property on <paramref name="instance"/> has a null value.</exception>
    /// <exception cref="Exception">Thrown if an array property on <paramref name="instance"/> has a length less than or equal to zero.</exception>
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
    /// Populates the properties of the specified object instance from the provided byte array, deserializing each
    /// property value according to its type.
    /// </summary>
    /// <remarks>Properties that are arrays are deserialized element by element. The method updates numBytes
    /// to reflect the number of bytes read. If bytes is shorter than required for all properties, only the available
    /// bytes are used.</remarks>
    /// <param name="sourceClass">The object instance whose properties will be set from the byte array. Must not be null.</param>
    /// <param name="bytes">The byte array containing serialized property values to be assigned to the object. If null, no properties are
    /// set and the method returns the value of numBytes.</param>
    /// <param name="numBytes">The starting offset, in bytes, within the byte array from which to begin deserialization. This value is
    /// incremented as properties are read.</param>
    /// <param name="isInnerClass">Indicates whether the object instance represents an inner class. This may affect how properties are
    /// deserialized.</param>
    /// <returns>The total number of bytes consumed from the byte array during deserialization.</returns>
    /// <exception cref="ArgumentNullException">Thrown if sourceClass is null.</exception>
    /// <exception cref="ArgumentException">Thrown if a property on sourceClass that is expected to be an array is not initialized.</exception>
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
    /// Serializes the accessible properties of the specified source object into the provided byte array, starting at
    /// the given offset.
    /// </summary>
    /// <remarks>If a property of the source object is an array, its elements are serialized sequentially into
    /// the byte array. Serialization stops if the end of the byte array is reached before all properties are
    /// written.</remarks>
    /// <param name="sourceClass">The object whose properties will be serialized into the byte array. Cannot be null. All accessible properties
    /// must have non-null values.</param>
    /// <param name="bytes">The byte array that receives the serialized property values. Cannot be null.</param>
    /// <param name="numBytes">The starting offset, in bytes, within the array at which serialization begins. If not specified, serialization
    /// starts at the beginning of the array.</param>
    /// <returns>The total number of bytes written to the array after serialization is complete.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceClass"/> or <paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if any accessible property of <paramref name="sourceClass"/> is null.</exception>
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

    /// <summary>
    /// Retrieves the public instance properties of the specified type that have accessible set methods.
    /// </summary>
    /// <remarks>Only properties with public set methods are included. Properties with non-public or
    /// inaccessible setters are excluded.</remarks>
    /// <param name="classType">The type to inspect for public instance properties with accessible setters. Cannot be null.</param>
    /// <returns>An enumerable collection of PropertyInfo objects representing the public instance properties of the specified
    /// type that can be set. The collection will be empty if no such properties exist.</returns>
    private static IEnumerable<PropertyInfo> GetAccessableProperties(Type classType) => classType
            .GetProperties(
                BindingFlags.SetProperty |
                BindingFlags.Public |
                BindingFlags.Instance)
            .Where(p => p.GetSetMethod() != null);

    /// <summary>
    /// Calculates the increased number of bytes required to represent a value of the specified type, optionally
    /// considering custom property attributes.
    /// </summary>
    /// <remarks>This method supports primitive types, strings with S7StringAttribute, and complex types by
    /// recursively calculating their size. The calculation may align sizes to even byte boundaries for certain
    /// types.</remarks>
    /// <param name="numBytes">The initial number of bytes to be increased based on the type and property information.</param>
    /// <param name="type">The type of the value for which the byte size is being calculated. Determines how the number of bytes is
    /// adjusted.</param>
    /// <param name="propertyInfo">Optional property metadata used to retrieve custom attributes, such as S7StringAttribute, which may affect the
    /// byte calculation for certain types.</param>
    /// <returns>The total number of bytes required to represent the value, adjusted according to the type and any relevant
    /// property attributes.</returns>
    /// <exception cref="ArgumentException">Thrown if the type is 'String' and the property does not have an S7StringAttribute, or if an instance of the
    /// specified type cannot be created.</exception>
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

    /// <summary>
    /// Retrieves the value of a property from a byte array, interpreting the bytes according to the specified property
    /// type.
    /// </summary>
    /// <remarks>This method supports several primitive types, including Boolean, Byte, Int16, UInt16, Int32,
    /// UInt32, Single, Double, and String, as well as custom types. For string properties, an S7StringAttribute must be
    /// present to specify the string format and length. The method advances the numBytes reference to track the
    /// position in the byte array after reading each value.</remarks>
    /// <param name="propertyType">The type of the property to extract. Determines how the bytes are interpreted and what value is returned.</param>
    /// <param name="propertyInfo">Metadata about the property, used for extracting additional information such as custom attributes. Can be null
    /// if not required for the property type.</param>
    /// <param name="bytes">The byte array containing the raw data from which the property value is extracted.</param>
    /// <param name="numBytes">A reference to the current position within the byte array. Updated to reflect the number of bytes consumed
    /// during extraction.</param>
    /// <returns>An object representing the extracted property value, typed according to the specified property type. Returns
    /// null if the value cannot be determined.</returns>
    /// <exception cref="ArgumentException">Thrown if the property type is string and the property does not have a required S7StringAttribute, or if an
    /// invalid string type is specified for the S7StringAttribute. Also thrown if an instance of the specified property
    /// type cannot be created.</exception>
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

    /// <summary>
    /// Serializes the specified property value into the provided byte array, starting at the given offset, and returns
    /// the updated offset after writing the value.
    /// </summary>
    /// <remarks>For string properties, the method relies on the S7StringAttribute to determine the
    /// serialization format and reserved length. The method supports multiple primitive types and will use
    /// type-specific serialization logic. If the property type is not explicitly handled, a generic serialization
    /// method is invoked. The caller is responsible for ensuring that the byte array has sufficient capacity for the
    /// serialized data.</remarks>
    /// <param name="propertyValue">The value of the property to serialize. Supported types include Boolean, Byte, Int16, UInt16, Int32, UInt32,
    /// Single, Double, and String.</param>
    /// <param name="propertyInfo">Metadata about the property being serialized. Required for string properties to retrieve custom serialization
    /// attributes; can be null for other types.</param>
    /// <param name="bytes">The byte array into which the property value will be written. The array must be large enough to accommodate the
    /// serialized data at the specified offset.</param>
    /// <param name="numBytes">The starting offset, in bytes, within the array where the property value will be written. The method returns the
    /// offset after writing.</param>
    /// <returns>The offset, in bytes, immediately following the serialized property value in the array.</returns>
    /// <exception cref="ArgumentException">Thrown if the property value is a string and the corresponding property does not have a valid S7StringAttribute,
    /// or if the attribute specifies an unsupported string type.</exception>
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

    /// <summary>
    /// Rounds the specified value up to the nearest even integer.
    /// </summary>
    /// <remarks>This method first rounds the value up to the nearest integer, then increments it if the
    /// result is odd to ensure it is even. The input value is modified in place.</remarks>
    /// <param name="numBytes">A reference to the value to be rounded. The value will be updated to the next even integer greater than or equal
    /// to its original value.</param>
    private static void IncrementToEven(ref double numBytes)
    {
        numBytes = Math.Ceiling(numBytes);
        if (numBytes % 2 > 0)
        {
            numBytes++;
        }
    }
}
