// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Describes periodic PLC tag polling.</summary>
/// <param name="intervalMilliseconds">The polling interval in milliseconds.</param>
public sealed class S7PollingOptions(double intervalMilliseconds = 100)
{
    /// <summary>The default polling interval in milliseconds.</summary>
    public const double DefaultIntervalMilliseconds = 100;

    /// <summary>Gets the polling interval in milliseconds.</summary>
    public double IntervalMilliseconds { get; } = intervalMilliseconds;
}
