// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Binding;

/// <summary>
/// Defines the PLC access direction for a generated tag binding.
/// </summary>
public enum S7TagDirection
{
    /// <summary>
    /// The tag is read from and written to the PLC.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// The tag is read from the PLC only.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// The tag is written to the PLC only.
    /// </summary>
    WriteOnly
}
