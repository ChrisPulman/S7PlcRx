// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Enterprise;

/// <summary>
/// Symbol table for symbolic addressing support.
/// </summary>
public sealed class SymbolTable
{
    /// <summary>
    /// Gets the collection of symbols indexed by name.
    /// </summary>
    public Dictionary<string, Symbol> Symbols { get; } = [];

    /// <summary>
    /// Gets the timestamp when the symbol table was loaded.
    /// </summary>
    public DateTime LoadedAt { get; } = DateTime.UtcNow;
}
