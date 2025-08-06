// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Batch operation result with detailed metrics.
/// </summary>
public class BatchOperationResult
{
    /// <summary>Gets or sets the operation start time.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the operation end time.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets or sets the number of operations in the batch.</summary>
    public int OperationCount { get; set; }

    /// <summary>Gets or sets the number of successful operations.</summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>Gets or sets the number of failed operations.</summary>
    public int FailedOperations { get; set; }

    /// <summary>Gets the total processing time.</summary>
    public TimeSpan ProcessingTime => EndTime - StartTime;

    /// <summary>Gets the average time per operation.</summary>
    public double AverageTimePerOperation => OperationCount > 0
        ? ProcessingTime.TotalMilliseconds / OperationCount
        : 0;

    /// <summary>Gets operation details.</summary>
    public List<OperationDetail> OperationDetails { get; } = [];

    /// <summary>Gets error details for failed operations.</summary>
    public List<string> ErrorDetails { get; } = [];
}
