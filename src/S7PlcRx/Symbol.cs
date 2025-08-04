// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Represents a PLC symbol with metadata.
/// </summary>
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
