// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using S7PlcRx.Core;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Represents a batch request for optimized PLC communication.
/// </summary>
internal class BatchRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchRequest"/> class.
    /// </summary>
    /// <param name="type">The request type.</param>
    /// <param name="tag">The tag to process.</param>
    /// <param name="priority">The priority of the request.</param>
    public BatchRequest(BatchRequestType type, Tag tag, RequestPriority priority = RequestPriority.Normal)
    {
        Type = type;
        Tag = tag;
        Priority = priority;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the request type.
    /// </summary>
    public BatchRequestType Type { get; }

    /// <summary>
    /// Gets the tag to process.
    /// </summary>
    public Tag Tag { get; }

    /// <summary>
    /// Gets the priority of the request.
    /// </summary>
    public RequestPriority Priority { get; }

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTime Timestamp { get; }
}
