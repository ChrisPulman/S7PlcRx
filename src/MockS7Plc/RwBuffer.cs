// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>Represents the Snap7 read/write callback buffer.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct RwBuffer
{
    /// <summary>Initializes a new instance of the <see cref="RwBuffer"/> struct.</summary>
    /// <param name="data">The telegram payload bytes.</param>
    public RwBuffer(byte[] data)
    {
        Data = data;
    }

    /// <summary>Gets the telegram payload bytes.</summary>
    [field: MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] // A telegram cannot exceed PDU size (960 bytes)
    public byte[] Data { get; }
}
