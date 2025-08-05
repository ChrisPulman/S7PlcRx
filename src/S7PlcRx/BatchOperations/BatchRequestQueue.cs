// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace S7PlcRx.BatchOperations;

/// <summary>
/// Batch request queue for optimizing multiple PLC operations.
/// </summary>
internal class BatchRequestQueue
{
    private readonly ConcurrentQueue<BatchRequest> _requests = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Gets the count of pending requests.
    /// </summary>
    public int Count => _requests.Count;

    /// <summary>
    /// Gets a value indicating whether checks if the queue is empty.
    /// </summary>
    public bool IsEmpty => _requests.IsEmpty;

    /// <summary>
    /// Enqueues a batch request.
    /// </summary>
    /// <param name="request">The batch request to enqueue.</param>
    public void Enqueue(BatchRequest request) => _requests.Enqueue(request);

    /// <summary>
    /// Dequeues all pending requests.
    /// </summary>
    /// <returns>A list of all pending batch requests.</returns>
    public List<BatchRequest> DequeueAll()
    {
        lock (_lockObject)
        {
            var result = new List<BatchRequest>();
            while (_requests.TryDequeue(out var request))
            {
                result.Add(request);
            }

            return result;
        }
    }
}
