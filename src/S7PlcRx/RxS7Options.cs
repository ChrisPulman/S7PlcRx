// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Composes connection, polling, and optional watchdog settings for <see cref="RxS7"/>.</summary>
/// <param name="connection">The PLC endpoint settings.</param>
/// <param name="polling">The polling settings, or <see langword="null"/> to use defaults.</param>
/// <param name="watchdog">The optional watchdog settings.</param>
public sealed class RxS7Options(
    S7ConnectionOptions connection,
    S7PollingOptions? polling = null,
    S7WatchdogOptions? watchdog = null)
{
    /// <summary>Gets the PLC endpoint settings.</summary>
    public S7ConnectionOptions Connection { get; } = connection;

    /// <summary>Gets the polling settings.</summary>
    public S7PollingOptions Polling { get; } = polling ?? new();

    /// <summary>Gets the optional watchdog settings.</summary>
    public S7WatchdogOptions? Watchdog { get; } = watchdog;
}
