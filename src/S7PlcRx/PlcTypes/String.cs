// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.PlcTypes;

/// <summary>
/// String.
/// </summary>
internal static class String
{
    /// <summary>
    /// From the byte array.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>A string.</returns>
    public static string FromByteArray(byte[] bytes) => System.Text.Encoding.ASCII.GetString(bytes);

    /// <summary>
    /// To the byte array.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(string? value)
    {
        var ca = value!.ToCharArray();
        var bytes = new byte[value.Length];
        for (var cnt = 0; cnt <= ca.Length - 1; cnt++)
        {
            bytes[cnt] = (byte)Asc(ca[cnt].ToString());
        }

        return bytes;
    }

    private static int Asc(string s)
    {
        var b = System.Text.Encoding.ASCII.GetBytes(s);
        if (b.Length > 0)
        {
            return b[0];
        }

        return 0;
    }
}
