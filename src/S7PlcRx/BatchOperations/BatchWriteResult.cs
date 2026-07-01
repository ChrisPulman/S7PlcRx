// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.BatchOperations;
#else
namespace S7PlcRx.BatchOperations;
#endif

/// <summary>
/// Represents the result of a batch write operation, including per-item success status, error messages, and overall
/// outcome.
/// </summary>
/// <remarks>Use this class to inspect which items in a batch write succeeded or failed, retrieve error details
/// for failed items, and determine whether the entire batch was successful or if a rollback was performed. The
/// dictionaries map item identifiers (such as tag names) to their respective statuses and error messages.</remarks>
public class BatchWriteResult
{
    /// <summary>Gets the success status for each tag.</summary>
    public Dictionary<string, bool> Success { get; } = [];

    /// <summary>Gets error messages for failed writes.</summary>
    public Dictionary<string, string> Errors { get; } = [];

    /// <summary>Gets or sets a value indicating whether gets whether all writes were successful.</summary>
    public bool OverallSuccess { get; set; }

    /// <summary>Gets or sets a value indicating whether gets whether rollback was performed.</summary>
    public bool RollbackPerformed { get; set; }

    /// <summary>Gets the count of successful writes.</summary>
    public int SuccessCount => CountValues(Success, true);

    /// <summary>Gets the count of failed writes.</summary>
    public int ErrorCount => CountValues(Success, false);

    /// <summary>Counts success dictionary values that match the expected state.</summary>
    /// <param name="values">The values to count.</param>
    /// <param name="expected">The expected success state.</param>
    /// <returns>The number of matching values.</returns>
    private static int CountValues(Dictionary<string, bool> values, bool expected)
    {
        var count = 0;
        foreach (var value in values.Values)
        {
            if (value == expected)
            {
                count++;
            }
        }

        return count;
    }
}
