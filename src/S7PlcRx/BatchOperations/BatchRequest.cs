// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Represents a single request to be processed as part of a batch operation.
/// </summary>
/// <param name="type">The type of the batch request to perform.</param>
/// <param name="tag">The tag associated with the request to be processed.</param>
/// <param name="priority">The priority level assigned to the request. Defaults to RequestPriority.Normal.</param>
internal class BatchRequest(BatchRequestType type, Tag tag, RequestPriority priority = RequestPriority.Normal)
{
    /// <summary>
    /// Gets the request type.
    /// </summary>
    public BatchRequestType Type { get; } = type;

    /// <summary>
    /// Gets the tag to process.
    /// </summary>
    public Tag Tag { get; } = tag;

    /// <summary>
    /// Gets the priority of the request.
    /// </summary>
    public RequestPriority Priority { get; } = priority;

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}
