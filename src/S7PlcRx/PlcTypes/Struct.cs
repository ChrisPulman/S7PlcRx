// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// Struct.
/// </summary>
internal static class Struct
{
    /// <summary>
    /// Creates a struct of a specified type by an array of bytes.
    /// </summary>
    /// <param name="structType">The struct type.</param>
    /// <param name="bytes">The array of bytes.</param>
    /// <returns>
    /// The object depending on the struct type or null if fails(array-length != struct-length.
    /// </returns>
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

        var numBytes = 0.0;
        var structValue = Activator.CreateInstance(structType);

        foreach (var info in structValue!.GetType().GetFields())
        {
            switch (info.FieldType.Name)
            {
                case "Boolean":

                    // and decode it
                    // get the value
                    var bytePos = (int)Math.Floor(numBytes);
                    var bitPos = (int)((numBytes - bytePos) / 0.125);
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
                    info.SetValue(structValue, bytes[(int)numBytes]);
                    numBytes++;
                    break;

                case "Int16":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // Evaluating here
                    var source = Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]);
                    info.SetValue(structValue, source.ConvertToShort());
                    numBytes += 2;
                    break;

                case "ushort":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // Evaluating here
                    info.SetValue(structValue, Word.FromBytes(bytes[(int)numBytes + 1], bytes[(int)numBytes]));
                    numBytes += 2;
                    break;

                case "Int32":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // Evaluating here
                    var sourceUInt = DWord.FromBytes(
                        bytes[(int)numBytes + 3],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 0]);
                    info.SetValue(structValue, sourceUInt.ConvertToInt());
                    numBytes += 4;
                    break;

                case "uint":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // Evaluating here
                    info.SetValue(structValue, DWord.FromBytes(
                        bytes[(int)numBytes],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 3]));
                    numBytes += 4;
                    break;

                case "Double":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    // Evaluating here
                    info.SetValue(structValue, Real.FromByteArray(new byte[]
                    {
                        bytes[(int)numBytes],
                        bytes[(int)numBytes + 1],
                        bytes[(int)numBytes + 2],
                        bytes[(int)numBytes + 3]
                    }));
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
    /// Gets the size of the struct in bytes.
    /// </summary>
    /// <param name="structType">the type of the struct.</param>
    /// <returns>the number of bytes.</returns>
    public static int GetStructSize(Type structType)
    {
        var numBytes = 0.0;

        var infos = structType.GetFields();
        foreach (var info in infos)
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

                case "Float":
                case "Double":
                    numBytes = Math.Ceiling(numBytes);
                    if (((numBytes / 2) - Math.Floor(numBytes / 2.0)) > 0)
                    {
                        numBytes++;
                    }

                    numBytes += 4;
                    break;

                default:
                    numBytes += GetStructSize(info.FieldType);
                    break;
            }
        }

        return (int)numBytes;
    }

    /// <summary>
    /// Creates a byte array depending on the struct type.
    /// </summary>
    /// <param name="structValue">The struct object.</param>
    /// <returns>A byte array or null if fails.</returns>
    public static byte[] ToBytes(object structValue)
    {
        var type = structValue.GetType();

        var size = GetStructSize(type);
        var bytes = new byte[size];
        var numBytes = 0.0;

        var infos = type.GetFields();
        foreach (var info in infos)
        {
            byte[]? bytes2 = null;
            int bytePos;
            switch (info.FieldType.Name)
            {
                case "Boolean":

                    // get the value
                    bytePos = (int)Math.Floor(numBytes);
                    var bitPos = (int)((numBytes - bytePos) / 0.125);
                    if ((bool)info.GetValue(structValue)!)
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
                    bytes[bytePos] = (byte)info.GetValue(structValue)!;
                    numBytes++;
                    break;

                case "Int16":
                    bytes2 = Int.ToByteArray((short)info.GetValue(structValue)!);
                    break;

                case "ushort":
                    bytes2 = Word.ToByteArray((ushort)info.GetValue(structValue)!);
                    break;

                case "Int32":
                    bytes2 = DInt.ToByteArray((int)info.GetValue(structValue)!);
                    break;

                case "uint":
                    bytes2 = DWord.ToByteArray((uint)info.GetValue(structValue)!);
                    break;

                case "Single":
                    bytes2 = Real.ToByteArray((float)info.GetValue(structValue)!);
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
