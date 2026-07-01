// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Binding;
#else
namespace S7PlcRx.Binding;
#endif

/// <summary>Defines the PLC access direction for a generated tag binding.</summary>
public enum S7TagDirection
{
    /// <summary>The tag is read from and written to the PLC.</summary>
    ReadWrite,

    /// <summary>The tag is read from the PLC only.</summary>
    ReadOnly,

    /// <summary>The tag is written to the PLC only.</summary>
    WriteOnly
}
