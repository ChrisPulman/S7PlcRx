// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using S7PlcRx.Enums;

namespace S7PlcRx;

/// <summary>
/// Interface for PLCS7.
/// </summary>
public interface IRxS7 : ICancelable
{
    /// <summary>
    /// Gets the ip.
    /// </summary>
    /// <value>
    /// The ip.
    /// </value>
    string IP { get; }

    /// <summary>
    /// Gets the is connected.
    /// </summary>
    /// <value>
    /// The is connected.
    /// </value>
    IObservable<bool> IsConnected { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is connected.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is connected; otherwise, <c>false</c>.
    /// </value>
    bool IsConnectedValue { get; }

    /// <summary>
    /// Gets the last error.
    /// </summary>
    /// <value>
    /// The last error.
    /// </value>
    IObservable<string> LastError { get; }

    /// <summary>
    /// Gets the last error code.
    /// </summary>
    /// <value>
    /// The last error code.
    /// </value>
    IObservable<ErrorCode> LastErrorCode { get; }

    /// <summary>
    /// Gets the observe all.
    /// </summary>
    /// <value>
    /// The observe all.
    /// </value>
    IObservable<Tag?> ObserveAll { get; }

    /// <summary>
    /// Gets the type of the PLC.
    /// </summary>
    /// <value>
    /// The type of the PLC.
    /// </value>
    CpuType PLCType { get; }

    /// <summary>
    /// Gets the rack.
    /// </summary>
    /// <value>
    /// The rack.
    /// </value>
    short Rack { get; }

    /// <summary>
    /// Gets the slot.
    /// </summary>
    /// <value>
    /// The slot.
    /// </value>
    short Slot { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is paused.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is paused; otherwise, <c>false</c>.
    /// </value>
    IObservable<bool> IsPaused { get; }

    /// <summary>
    /// Gets the status.
    /// </summary>
    /// <value>
    /// The status.
    /// </value>
    IObservable<string> Status { get; }

    /// <summary>
    /// Gets the tag list to read.
    /// </summary>
    /// <value>
    /// The tag list to read.
    /// </value>
    Tags TagList { get; }

    /// <summary>
    /// Gets or sets a value indicating whether [show watch dog writing].
    /// </summary>
    /// <value><c>true</c> if [show watch dog writing]; otherwise, <c>false</c>.</value>
    bool ShowWatchDogWriting { get; set; }

    /// <summary>
    /// Gets the watch dog address.
    /// </summary>
    /// <value>The watch dog address.</value>
    string? WatchDogAddress { get; }

    /// <summary>
    /// Gets or sets the watch dog value to write.
    /// </summary>
    /// <value>The watch dog value to write.</value>
    ushort WatchDogValueToWrite { get; set; }

    /// <summary>
    /// Gets the watch dog writing time. (Sec).
    /// </summary>
    /// <value>The watch dog writing time. (Sec).</value>
    int WatchDogWritingTime { get; }

    /// <summary>
    /// Gets the read time.
    /// </summary>
    /// <value>
    /// The read time.
    /// </value>
    IObservable<long> ReadTime { get; }

    /// <summary>
    /// Observes the specified variable.
    /// </summary>
    /// <typeparam name="T">the type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <returns>An observable of T.</returns>
    IObservable<T?> Observe<T>(string? variable);

    /// <summary>
    /// Reads the specified variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <returns>A value of T.</returns>
    Task<T?> Value<T>(string? variable);

    /// <summary>
    /// Reads the specified variable with cancellation support.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A value of T.</returns>
    Task<T?> ValueAsync<T>(string? variable, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the specified variable.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="variable">The variable.</param>
    /// <param name="value">The value.</param>
    void Value<T>(string? variable, T? value);

    /// <summary>
    /// Gets the cpu information. AS Name, Module Name, Copyright, Serial Number, Module Type Name, Order Code, Version.
    /// </summary>
    /// <returns>A string Array.</returns>
    IObservable<string[]> GetCpuInfo();
}
