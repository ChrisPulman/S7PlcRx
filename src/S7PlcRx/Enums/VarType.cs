// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Variable Type.
/// </summary>
internal enum VarType
{
    /// <summary>
    /// The bit.
    /// </summary>
    Bit,

    /// <summary>
    /// The byte.
    /// </summary>
    Byte,

    /// <summary>
    /// The word.
    /// </summary>
    Word,

    /// <summary>
    /// The d word.
    /// </summary>
    DWord,

    /// <summary>
    /// The int.
    /// </summary>
    Int,

    /// <summary>
    /// The d int.
    /// </summary>
    DInt,

    /// <summary>
    /// The real.
    /// </summary>
    Real,

    /// <summary>
    /// The l real.
    /// </summary>
    LReal,

    /// <summary>
    /// The string.
    /// </summary>
    String,

    /// <summary>
    /// S7 String variable type (variable).
    /// </summary>
    S7String,

    /// <summary>
    /// S7 WString variable type (variable).
    /// </summary>
    S7WString,

    /// <summary>
    /// The timer.
    /// </summary>
    Timer,

    /// <summary>
    /// The counter.
    /// </summary>
    Counter,

    /// <summary>
    /// DateTIme variable type.
    /// </summary>
    DateTime,

    /// <summary>
    /// IEC date (legacy) variable type.
    /// </summary>
    Date,

    /// <summary>
    /// DateTimeLong variable type.
    /// </summary>
    DateTimeLong,

    /// <summary>
    /// S7 TIME variable type - serialized as S7 DInt and deserialized as C# TimeSpan.
    /// </summary>
    Time
}
