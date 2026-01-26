// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enums;

/// <summary>
/// Specifies the type of data used in operations such as counters, timers, inputs, outputs, memory, or data blocks.
/// </summary>
/// <remarks>This enumeration is typically used to identify or select a specific data type when interacting with
/// systems that distinguish between these categories, such as programmable logic controllers (PLCs) or industrial
/// automation software. The values correspond to standard codes for each data type.</remarks>
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
    DataBlock = 132,
}
