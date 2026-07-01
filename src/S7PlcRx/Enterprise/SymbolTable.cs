// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>Represents a read-only table of named symbols and the time at which it was loaded.</summary>
public sealed class SymbolTable
{
    /// <summary>Gets the collection of symbols indexed by name.</summary>
    public Dictionary<string, Symbol> Symbols { get; } = [];

    /// <summary>Gets the timestamp when the symbol table was loaded.</summary>
    public DateTime LoadedAt { get; } = DateTime.UtcNow;
}
