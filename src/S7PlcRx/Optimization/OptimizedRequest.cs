// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Represents an optimized request for batch processing.
/// </summary>
internal class OptimizedRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizedRequest"/> class.
    /// </summary>
    /// <param name="tag">The tag to process.</param>
    /// <param name="requestType">The type of request.</param>
    /// <param name="priority">The request priority.</param>
    public OptimizedRequest(Tag tag, OptimizedRequestType requestType, OptimizationRequestPriority priority = OptimizationRequestPriority.Normal)
    {
        Tag = tag;
        RequestType = requestType;
        Priority = priority;
        Timestamp = DateTime.UtcNow;
        CompletionSource = new TaskCompletionSource<bool>();
    }

    /// <summary>
    /// Gets the tag to process.
    /// </summary>
    public Tag Tag { get; }

    /// <summary>
    /// Gets the request type.
    /// </summary>
    public OptimizedRequestType RequestType { get; }

    /// <summary>
    /// Gets the request priority.
    /// </summary>
    public OptimizationRequestPriority Priority { get; }

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Gets the completion source for async operations.
    /// </summary>
    public TaskCompletionSource<bool>? CompletionSource { get; }
}
