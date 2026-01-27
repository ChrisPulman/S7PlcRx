// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Specifies the string encoding type used for S7 PLC string variables.
/// </summary>
/// <remarks>Use this enumeration to indicate whether a string variable should be interpreted as an ASCII
/// (S7String) or Unicode (S7WString) string when communicating with Siemens S7 PLCs. The encoding type determines how
/// string data is read from or written to the PLC.</remarks>
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
