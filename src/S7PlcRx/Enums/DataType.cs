// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Data Type.
/// </summary>
internal enum DataType
{
    /// <summary>
    /// The counter.
    /// </summary>
    Counter = 28,

    /// <summary>
    /// The timer.
    /// </summary>
    Timer = 29,

    /// <summary>
    /// The input.
    /// </summary>
    Input = 129,

    /// <summary>
    /// The output.
    /// </summary>
    Output = 130,

    /// <summary>
    /// The memory.
    /// </summary>
    Memory = 131,

    /// <summary>
    /// The data block.
    /// </summary>
    DataBlock = 132
}
