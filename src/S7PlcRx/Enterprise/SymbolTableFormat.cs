// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enterprise;

/// <summary>
/// Specifies the supported formats for serializing or deserializing a symbol table.
/// </summary>
public enum SymbolTableFormat
{
    /// <summary>CSV format.</summary>
    Csv,

    /// <summary>JSON format.</summary>
    Json,

    /// <summary>XML format.</summary>
    Xml
}
