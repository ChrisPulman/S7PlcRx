// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace MockS7Plc;

/// <summary>
/// RwBuffer.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct RwBuffer
{
    /// <summary>
    /// The data.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)] // A telegram cannot exceed PDU size (960 bytes)
    public byte[] Data;
}
