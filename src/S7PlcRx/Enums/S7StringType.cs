// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// String type.
/// </summary>
public enum S7StringType
{
    /// <summary>
    /// ASCII string.
    /// </summary>
    S7String = VarType.S7String,

    /// <summary>
    /// Unicode string.
    /// </summary>
    S7WString = VarType.S7WString
}
