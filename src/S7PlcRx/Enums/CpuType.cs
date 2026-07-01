// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enums;
#else
namespace S7PlcRx.Enums;
#endif

/// <summary>Specifies the supported CPU types for Siemens programmable logic controllers (PLCs).</summary>
/// <remarks>Use this enumeration to indicate the model of CPU when configuring or communicating with Siemens PLC
/// devices. The available values correspond to common Siemens PLC families, such as LOGO!, S7-200, S7-300, S7-400,
/// S7-1200, and S7-1500. Selecting the correct CPU type ensures compatibility with device-specific protocols and
/// features.</remarks>
public enum CpuType
{
    /// <summary>The logo0ba8.</summary>
    Logo0BA8,

    /// <summary>The S7200.</summary>
    S7200,

    /// <summary>The S7300.</summary>
    S7300,

    /// <summary>The S7400.</summary>
    S7400,

    /// <summary>The Siemens S7 1200 CPU family.</summary>
    S71200,

    /// <summary>The Siemens S7 1500 CPU family.</summary>
    S71500,
}
