// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Class.
/// </summary>
internal static class Class
{
    /// <summary>
    /// Creates a struct of a specified type by an array of bytes.
    /// </summary>
    /// <param name="sourceClass">The source class.</param>
    /// <param name="classType">The struct type.</param>
    /// <param name="bytes">The array of bytes.</param>
    public static void FromBytes(object? sourceClass, Type classType, byte[] bytes)
    {
        if (bytes == null)
        {
            return;
        }

        if (bytes.Length != GetClassSize(classType))
        {
            return;
        }

        // and decode it
        int bytePos;
        int bitPos;
        var numBytes = 0.0;

        foreach (var property in sourceClass?.GetType().GetProperties()!)
        {
            switch (property.PropertyType.Name)
            {
                case "Boolean":

                    // get the value
                    bytePos = (int)Math.Floor(numBytes);
                    bitPos = (int)((numBytes - bytePos) / 0.125);
                    if ((bytes[bytePos] & (int)Math.Pow(2, bitPos)) != 0)
                    {
                        property.SetValue(sourceClass, true, null);
                    }
                    else
                    {
                        property.SetValue(sourceClass, false, null);
                    }

                    numBytes += 0.125;
                    break;

                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    property.SetValue(sourceClass, bytes[(int)numBytes], null);
                    numBytes++;
                    break;

                case "Int16":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // hier auswerten
                    var source = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                    property.SetValue(sourceClass, source.ConvertToShort(), null);
                    numBytes += 2;
                    break;

                case "ushort":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // hier auswerten
                    property.SetValue(sourceClass, Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]), null);
                    numBytes += 2;
                    break;

                case "Int32":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // hier auswerten
                    var sourceUInt = DWord.FromBytes(
                        bytes[(int)numBytes + 3],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 0]);
                    property.SetValue(sourceClass, sourceUInt.ConvertToInt(), null);
                    numBytes += 4;
                    break;

                case "uint":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // hier auswerten
                    property.SetValue(sourceClass, DWord.FromBytes(bytes[(int)numBytes], bytes[(int)numBytes + 1], bytes[(int)numBytes + 2], bytes[(int)numBytes + 3]), null);
                    numBytes += 4;
                    break;

                case "Single":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // hier auswerten
                    property.SetValue(sourceClass, LReal.FromByteArray([bytes[(int)numBytes], bytes[(int)numBytes + 1], bytes[(int)numBytes + 2], bytes[(int)numBytes + 3]]), null);
                    numBytes += 4;
                    break;

                default:
                    var buffer = new byte[GetClassSize(property.PropertyType)];
                    if (buffer.Length == 0)
                    {
                        continue;
                    }

                    Buffer.BlockCopy(bytes, (int)Math.Ceiling(numBytes), buffer, 0, buffer.Length);
                    var propClass = Activator.CreateInstance(property.PropertyType);
                    FromBytes(propClass, property.PropertyType, buffer);
                    property.SetValue(sourceClass, propClass, null);
                    numBytes += buffer.Length;
                    break;
            }
        }
    }

    /// <summary>
    /// Gets the size of the struct in bytes.
    /// </summary>
    /// <param name="classType">the type of the class.</param>
    /// <returns>the number of bytes.</returns>
    public static int GetClassSize(Type classType)
    {
        var numBytes = 0.0;

        foreach (var property in classType.GetProperties())
        {
            switch (property.PropertyType.Name)
            {
                case "Boolean":
                    numBytes += 0.125;
                    break;

                case "Byte":
                    numBytes = Math.Ceiling(numBytes);
                    numBytes++;
                    break;

                case "Int16":
                case "ushort":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    numBytes += 2;
                    break;

                case "Int32":
                case "uint":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    numBytes += 4;
                    break;

                case "Single":
                case "Double":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    numBytes += 4;
                    break;

                default:
                    numBytes += GetClassSize(property.PropertyType);
                    break;
            }
        }

        return (int)numBytes;
    }

    /// <summary>
    /// Creates a byte array depending on the struct type.
    /// </summary>
    /// <param name="sourceClass">The struct object.</param>
    /// <returns>A byte array or null if fails.</returns>
    public static byte[] ToBytes(object sourceClass)
    {
        var type = sourceClass.GetType();

        var size = GetClassSize(type);
        var bytes = new byte[size];
        byte[]? bytes2;

        int bytePos;
        int bitPos;
        var numBytes = 0.0;

        foreach (var property in sourceClass.GetType().GetProperties())
        {
            bytes2 = default;
            switch (property.PropertyType.Name)
            {
                case "Boolean":

                    // get the value
                    bytePos = (int)Math.Floor(numBytes);
                    bitPos = (int)((numBytes - bytePos) / 0.125);
                    if ((bool)property.GetValue(sourceClass, null)!)
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
                    bytes[bytePos] = (byte)property.GetValue(sourceClass, null)!;
                    numBytes++;
                    break;

                case "Int16":
                    bytes2 = Int.ToByteArray((short)property.GetValue(sourceClass, null)!);
                    break;

                case "ushort":
                    bytes2 = Word.ToByteArray((ushort)property.GetValue(sourceClass, null)!);
                    break;

                case "Int32":
                    bytes2 = DInt.ToByteArray((int)property.GetValue(sourceClass, null)!);
                    break;

                case "uint":
                    bytes2 = DWord.ToByteArray((uint)property.GetValue(sourceClass, null)!);
                    break;

                case "Single":
                    bytes2 = LReal.ToByteArray((float)property.GetValue(sourceClass, null)!);
                    break;
            }

            if (bytes2 != null)
            {
                // add them
                numBytes = Math.Ceiling(numBytes);
                if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
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
