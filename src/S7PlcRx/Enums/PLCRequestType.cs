// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enums;
#else
namespace S7PlcRx.Enums;
#endif

/// <summary>Specifies the type of request to perform on a programmable logic controller (PLC), such as reading or writing data.</summary>
internal enum PLCRequestType
{
    /// <summary>The read.</summary>
    Read,

    /// <summary>The write.</summary>
    Write,
}
