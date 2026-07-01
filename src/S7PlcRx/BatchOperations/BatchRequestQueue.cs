// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.BatchOperations;
#else
namespace S7PlcRx.BatchOperations;
#endif

/// <summary>Represents a thread-safe queue for managing batch requests.</summary>
/// <remarks>This class provides methods to enqueue batch requests and retrieve all pending requests in a
/// thread-safe manner. It is intended for internal use where batching of requests is required.</remarks>
internal class BatchRequestQueue
{
    /// <summary>Stores the r eq ue s t s used by this instance.</summary>
    private readonly ConcurrentQueue<BatchRequest> _requests = new();

    /// <summary>Stores the lock used to synchronize queue draining.</summary>
#if NET8_0
    private readonly object _lockObject = new();
#else
    private readonly Lock _lockObject = new();
#endif

    /// <summary>Gets the count of pending requests.</summary>
    public int Count => _requests.Count;

    /// <summary>Gets a value indicating whether checks if the queue is empty.</summary>
    public bool IsEmpty => _requests.IsEmpty;

    /// <summary>Enqueues a batch request.</summary>
    /// <param name="request">The batch request to enqueue.</param>
    public void Enqueue(BatchRequest request) => _requests.Enqueue(request);

    /// <summary>Dequeues all pending requests.</summary>
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
