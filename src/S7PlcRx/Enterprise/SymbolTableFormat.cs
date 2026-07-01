// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>Specifies the supported formats for serializing or deserializing a symbol table.</summary>
public enum SymbolTableFormat
{
    /// <summary>CSV format.</summary>
    Csv,

    /// <summary>JSON format.</summary>
    Json,

    /// <summary>XML format.</summary>
    Xml
}
