// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Represents the result of a batch read operation, including the values read, per-tag success status, error messages,
/// and overall success information.
/// </summary>
/// <remarks>Use this class to access the outcome of a batch read, including which tags succeeded, which failed,
/// and any associated error messages. The dictionaries provide per-tag details, while the overall success and count
/// properties offer summary information. This class is typically used in scenarios where multiple items are read in a
/// single operation and individual results must be tracked.</remarks>
/// <typeparam name="T">The type of the values returned for each tag in the batch read operation.</typeparam>
public class BatchReadResult<T>
{
    /// <summary>Gets the successfully read values.</summary>
    public Dictionary<string, T> Values { get; } = [];

    /// <summary>Gets the success status for each tag.</summary>
    public Dictionary<string, bool> Success { get; } = [];

    /// <summary>Gets error messages for failed reads.</summary>
    public Dictionary<string, string> Errors { get; } = [];

    /// <summary>Gets or sets a value indicating whether gets whether all reads were successful.</summary>
    public bool OverallSuccess { get; set; }

    /// <summary>Gets the count of successful reads.</summary>
    public int SuccessCount => Success.Values.Count(s => s);

    /// <summary>Gets the count of failed reads.</summary>
    public int ErrorCount => Success.Values.Count(s => !s);
}
