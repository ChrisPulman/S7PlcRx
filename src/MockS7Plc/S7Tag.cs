// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>Represents a Snap7 area tag.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct S7Tag
{
    /// <summary>Initializes a new instance of the <see cref="S7Tag"/> struct.</summary>
    /// <param name="area">The Snap7 area code.</param>
    /// <param name="dbNumber">The database number.</param>
    /// <param name="start">The start offset.</param>
    /// <param name="elements">The element count.</param>
    /// <param name="wordLen">The Snap7 word length code.</param>
    public S7Tag(int area, int dbNumber, int start, int elements, int wordLen)
    {
        Area = area;
        DBNumber = dbNumber;
        Start = start;
        Elements = elements;
        WordLen = wordLen;
    }

    /// <summary>Gets the Snap7 area code.</summary>
    public int Area { get; }

    /// <summary>Gets the database number.</summary>
    public int DBNumber { get; }

    /// <summary>Gets the start offset.</summary>
    public int Start { get; }

    /// <summary>Gets the element count.</summary>
    public int Elements { get; }

    /// <summary>Gets the Snap7 word length code.</summary>
    public int WordLen { get; }
}
