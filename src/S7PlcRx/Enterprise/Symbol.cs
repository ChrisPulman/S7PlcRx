// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>
/// Represents a programmable logic controller (PLC) symbol, including its name, address, data type, length, and
/// description.
/// </summary>
/// <remarks>Use the Symbol class to define and manage metadata for PLC variables, such as their symbolic name,
/// address, and data type. This class is typically used in applications that interact with PLCs for automation or
/// monitoring purposes.</remarks>
public sealed class Symbol
{
    /// <summary>Gets or sets the symbol name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the PLC address.</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Gets or sets the data type.</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>Gets or sets the length for array types.</summary>
    public int Length { get; set; } = 1;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;
}
