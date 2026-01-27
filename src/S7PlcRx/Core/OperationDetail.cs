// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Core;

/// <summary>
/// Represents the details of an operation, including its type, status, duration, and related metadata.
/// </summary>
public class OperationDetail
{
    /// <summary>Gets or sets the tag name.</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>Gets or sets the operation type.</summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether gets or sets whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the operation duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets any error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the data block number.</summary>
    public int DataBlockNumber { get; set; }
}
