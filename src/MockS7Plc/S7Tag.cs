// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>
/// S7Tag.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct S7Tag
{
    /// <summary>
    /// The area.
    /// </summary>
    public int Area;
    /// <summary>
    /// The database number.
    /// </summary>
    public int DBNumber;
    /// <summary>
    /// The start.
    /// </summary>
    public int Start;
    /// <summary>
    /// The elements.
    /// </summary>
    public int Elements;
    /// <summary>
    /// The word length.
    /// </summary>
    public int WordLen;
}
