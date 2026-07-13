// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Describes optional PLC watchdog writes.</summary>
/// <param name="address">The DBW watchdog address.</param>
/// <param name="valueToWrite">The value written during each watchdog cycle.</param>
/// <param name="intervalSeconds">The watchdog interval in seconds.</param>
public sealed class S7WatchdogOptions(
    string address,
    ushort valueToWrite = 4500,
    int intervalSeconds = 10)
{
    /// <summary>The default value written during each watchdog cycle.</summary>
    public const ushort DefaultValueToWrite = 4500;

    /// <summary>The default watchdog interval in seconds.</summary>
    public const int DefaultIntervalSeconds = 10;

    /// <summary>Gets the DBW watchdog address.</summary>
    public string Address { get; } = address;

    /// <summary>Gets the value written during each watchdog cycle.</summary>
    public ushort ValueToWrite { get; } = valueToWrite;

    /// <summary>Gets the watchdog interval in seconds.</summary>
    public int IntervalSeconds { get; } = intervalSeconds;
}
