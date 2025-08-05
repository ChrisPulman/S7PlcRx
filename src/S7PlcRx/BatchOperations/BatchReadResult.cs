// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Result of a batch read operation.
/// </summary>
/// <typeparam name="T">The type of values read.</typeparam>
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
