// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Optimization;

/// <summary>
/// Represents a request for tag optimization, including its type, priority, and associated metadata.
/// </summary>
/// <param name="tag">The tag to be processed by the optimization request. Cannot be null.</param>
/// <param name="requestType">The type of optimization to perform for the request.</param>
/// <param name="priority">The priority level assigned to the request. Defaults to OptimizationRequestPriority.Normal if not specified.</param>
internal class OptimizedRequest(Tag tag, OptimizedRequestType requestType, OptimizationRequestPriority priority = OptimizationRequestPriority.Normal)
{
    /// <summary>
    /// Gets the tag to process.
    /// </summary>
    public Tag Tag { get; } = tag;

    /// <summary>
    /// Gets the type of the optimized request associated with this instance.
    /// </summary>
    public OptimizedRequestType RequestType { get; } = requestType;

    /// <summary>
    /// Gets the priority level assigned to the optimization request.
    /// </summary>
    public OptimizationRequestPriority Priority { get; } = priority;

    /// <summary>
    /// Gets the UTC timestamp indicating when the object was created.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the completion source for async operations.
    /// </summary>
    public TaskCompletionSource<bool>? CompletionSource { get; } = new TaskCompletionSource<bool>();
}
