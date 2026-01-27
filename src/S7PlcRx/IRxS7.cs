// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using S7PlcRx.Enums;

namespace S7PlcRx;

/// <summary>
/// Defines an interface for reactive communication with a Siemens S7 PLC, providing observable access to connection
/// status, errors, tag values, and PLC information, as well as methods for reading and writing variables
/// asynchronously.
/// </summary>
/// <remarks>The IRxS7 interface exposes members for monitoring and interacting with a PLC in a reactive manner
/// using observables. It supports observing connection state, errors, and tag values, as well as reading and writing
/// variables with optional cancellation support. Implementations are expected to handle connection management and
/// provide up-to-date PLC information. Thread safety and subscription management depend on the specific
/// implementation.</remarks>
public interface IRxS7 : ICancelable
{
    /// <summary>
    /// Gets the IP address associated with the current instance.
    /// </summary>
    string IP { get; }

    /// <summary>
    /// Gets an observable sequence that indicates whether the connection is currently established.
    /// </summary>
    /// <remarks>Subscribers receive updates whenever the connection state changes. The sequence emits <see
    /// langword="true"/> when connected and <see langword="false"/> when disconnected.</remarks>
    IObservable<bool> IsConnected { get; }

    /// <summary>
    /// Gets a value indicating whether the connection is currently established.
    /// </summary>
    bool IsConnectedValue { get; }

    /// <summary>
    /// Gets an observable sequence that provides error messages encountered during operation.
    /// </summary>
    /// <remarks>Subscribers receive error messages as they occur. The sequence completes when the underlying
    /// process completes or is disposed. No errors are pushed after completion.</remarks>
    IObservable<string> LastError { get; }

    /// <summary>
    /// Gets an observable sequence that provides notifications of the most recent error code encountered by the
    /// component.
    /// </summary>
    /// <remarks>Subscribers receive updates whenever a new error occurs. The sequence completes when the
    /// component is disposed or no longer reports errors. Thread safety and emission timing depend on the
    /// implementation of the observable.</remarks>
    IObservable<ErrorCode> LastErrorCode { get; }

    /// <summary>
    /// Gets an observable sequence that emits all tag updates as they occur.
    /// </summary>
    /// <remarks>Subscribers receive notifications for every tag, including additions, updates, and removals.
    /// The sequence emits a value of <see langword="null"/> when a tag is removed.</remarks>
    IObservable<Tag?> ObserveAll { get; }

    /// <summary>
    /// Gets the type of programmable logic controller (PLC) associated with this instance.
    /// </summary>
    CpuType PLCType { get; }

    /// <summary>
    /// Gets the rack number associated with the device or connection.
    /// </summary>
    short Rack { get; }

    /// <summary>
    /// Gets the slot number associated with the current instance.
    /// </summary>
    short Slot { get; }

    /// <summary>
    /// Gets an observable sequence that indicates whether the operation is currently paused.
    /// </summary>
    /// <remarks>Subscribers receive a value of <see langword="true"/> when the operation is paused and <see
    /// langword="false"/> when it is active. The sequence emits updates whenever the paused state changes.</remarks>
    IObservable<bool> IsPaused { get; }

    /// <summary>
    /// Gets an observable sequence that provides status updates as strings.
    /// </summary>
    /// <remarks>Subscribers receive status notifications as they occur. The sequence may complete or error
    /// depending on the underlying implementation.</remarks>
    IObservable<string> Status { get; }

    /// <summary>
    /// Gets the collection of tags associated with the current instance.
    /// </summary>
    Tags TagList { get; }

    /// <summary>
    /// Gets or sets a value indicating whether WatchDog writing operations are displayed.
    /// </summary>
    bool ShowWatchDogWriting { get; set; }

    /// <summary>
    /// Gets the network address of the WatchDog service, if configured.
    /// </summary>
    string? WatchDogAddress { get; }

    /// <summary>
    /// Gets or sets the value to be written to the watchdog register.
    /// </summary>
    ushort WatchDogValueToWrite { get; set; }

    /// <summary>
    /// Gets the time interval, in milliseconds, used by the watchdog for writing operations.
    /// </summary>
    int WatchDogWritingTime { get; }

    /// <summary>
    /// Gets an observable sequence that provides the current read time in ticks.
    /// </summary>
    /// <remarks>Subscribers receive updates whenever the read time changes. The value represents the number
    /// of ticks elapsed, where one tick equals 100 nanoseconds.</remarks>
    IObservable<long> ReadTime { get; }

    /// <summary>
    /// Observes changes to the specified variable and returns a sequence of its values as they are updated.
    /// </summary>
    /// <typeparam name="T">The type of the variable to observe.</typeparam>
    /// <param name="variable">The name of the variable to observe. Can be null to observe the default variable, if applicable.</param>
    /// <returns>An observable sequence that emits the current and subsequent values of the specified variable. The value is null
    /// if the variable does not exist or has not been set.</returns>
    IObservable<T?> Observe<T>(string? variable);

    /// <summary>
    /// Asynchronously retrieves the value of the specified variable, if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="variable">The name of the variable whose value is to be retrieved. Can be null to indicate the default variable.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the variable if found;
    /// otherwise, null.</returns>
    Task<T?> Value<T>(string? variable);

    /// <summary>
    /// Asynchronously retrieves the value of the specified variable, if it exists.
    /// </summary>
    /// <typeparam name="T">The type of the value to retrieve.</typeparam>
    /// <param name="variable">The name of the variable to retrieve. Can be null to indicate the default variable, if supported.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the variable if found;
    /// otherwise, null.</returns>
    Task<T?> ValueAsync<T>(string? variable, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the value of a variable with the specified name and value.
    /// </summary>
    /// <typeparam name="T">The type of the value to assign to the variable.</typeparam>
    /// <param name="variable">The name of the variable to set. Can be null to indicate an unnamed or default variable.</param>
    /// <param name="value">The value to assign to the variable. Can be null if the variable type allows null values.</param>
    void Value<T>(string? variable, T? value);

    /// <summary>
    /// Retrieves an observable sequence containing information about the system's CPU.
    /// </summary>
    /// <remarks>Subscribers receive updates as the CPU information changes. The format and content of each
    /// string array may vary depending on the platform or implementation.</remarks>
    /// <returns>An observable sequence of string arrays, where each array contains details about the CPU. The sequence emits new
    /// arrays when CPU information is updated.</returns>
    IObservable<string[]> GetCpuInfo();
}
