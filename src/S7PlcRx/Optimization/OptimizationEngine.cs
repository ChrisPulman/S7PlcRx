// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Cache;
#else
using S7PlcRx.Cache;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Optimization;
#else
namespace S7PlcRx.Optimization;
#endif

/// <summary>
/// Provides batch processing and caching for optimized requests, enabling efficient handling of repeated or concurrent
/// operations.
/// </summary>
/// <remarks>The OptimizationEngine is intended for internal use to improve performance by batching similar
/// requests and caching their results. It manages request queuing, cache maintenance, and periodic batch execution.
/// This class is not thread-safe for direct external manipulation, but its public methods are designed to be safe for
/// concurrent use. Dispose the instance when it is no longer needed to release resources.</remarks>
internal class OptimizationEngine : IDisposable
{
    /// <summary>Stores the r eq ue st qu e u e used by this instance.</summary>
    private readonly ConcurrentQueue<OptimizedRequest> _requestQueue = new();

    /// <summary>Stores the v al ue ca c h e used by this instance.</summary>
    private readonly ConcurrentDictionary<string, CachedValue> _valueCache = new();

    /// <summary>Stores the b at ch ti m e r used by this instance.</summary>
    private readonly Timer _batchTimer;

    /// <summary>Stores the p ro ce ss in gl o c k used by this instance.</summary>
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    /// <summary>Stores the a xb at ch si z e used by this instance.</summary>
    private readonly int _maxBatchSize;

    /// <summary>Initializes a new instance of the <see cref="OptimizationEngine"/> class.</summary>
    /// <param name="batchIntervalMs">The batch processing interval in milliseconds.</param>
    /// <param name="maxBatchSize">The maximum batch size per request.</param>
    public OptimizationEngine(int batchIntervalMs = 50, int maxBatchSize = 20)
    {
        _maxBatchSize = maxBatchSize;
        _batchTimer = new(
            ProcessBatchedRequests,
            null,
            TimeSpan.FromMilliseconds(batchIntervalMs),
            TimeSpan.FromMilliseconds(batchIntervalMs));
    }

    /// <summary>
    /// Gets the current statistics for the cache, including the number of cached values, pending requests, and the
    /// cache hit ratio.
    /// </summary>
    /// <remarks>Use this property to monitor cache performance and usage patterns. The returned statistics
    /// represent a snapshot at the time the property is accessed and may change as cache activity occurs.</remarks>
    public CacheStatistics CacheStats => new()
    {
        CachedValueCount = _valueCache.Count,
        PendingRequestCount = _requestQueue.Count,
        CacheHitRatio = CalculateCacheHitRatio()
    };

    /// <summary>Adds the specified request to the processing queue.</summary>
    /// <param name="request">The request to enqueue for processing. Cannot be null.</param>
    public void EnqueueRequest(OptimizedRequest request) => _requestQueue.Enqueue(request);

    /// <summary>
    /// Retrieves a cached value associated with the specified tag name if it exists and is not older than the specified
    /// maximum age.
    /// </summary>
    /// <param name="tagName">The tag name used to identify the cached value. Cannot be null.</param>
    /// <param name="maxAge">The maximum allowed age of the cached value. Values older than this duration are considered expired.</param>
    /// <returns>The cached value associated with the specified tag name if it exists and is not expired; otherwise, null.</returns>
    public object? GetCachedValue(string tagName, TimeSpan maxAge)
    {
        if (!_valueCache.TryGetValue(tagName, out var cachedValue) ||
            DateTime.UtcNow - cachedValue.Timestamp > maxAge)
        {
            return null;
        }

        cachedValue.HitCount++;
        return cachedValue.Value;
    }

    /// <summary>Updates the cached value associated with the specified tag name, or adds a new entry if the tag does not exist.</summary>
    /// <param name="tagName">The unique tag name used to identify the cached value. Cannot be null.</param>
    /// <param name="value">The value to store in the cache for the specified tag name.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateCache(string tagName, object value) => _valueCache.AddOrUpdate(
            tagName,
            new CachedValue(value, DateTime.UtcNow),
            (_, existing) => new CachedValue(value, DateTime.UtcNow, existing.HitCount));

    /// <summary>Removes all cache entries that have expired based on the specified maximum age.</summary>
    /// <remarks>Use this method to periodically clean up expired items and free memory. The method compares
    /// each entry's timestamp to the current UTC time minus the specified maximum age.</remarks>
    /// <param name="maxAge">The maximum duration a cache entry is considered valid. Entries older than this value are removed.</param>
    public void ClearExpiredCache(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var expiredKeys = new List<string>();
        foreach (var kvp in _valueCache)
        {
            if (kvp.Value.Timestamp < cutoff)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _ = _valueCache.TryRemove(key, out _);
        }
    }

    /// <summary>Disposes the optimization engine.</summary>
    public void Dispose()
    {
        _batchTimer?.Dispose();
        _processingLock?.Dispose();
        _valueCache.Clear();
    }

    /// <summary>Processes a batch of optimized read or write requests targeting the same data block to improve performance.</summary>
    /// <remarks>Batching requests reduces network round trips and can significantly enhance throughput when
    /// multiple operations target the same data block. Each request's completion is signaled via its associated
    /// completion source.</remarks>
    /// <param name="requests">A list of <see cref="OptimizedRequest"/> objects representing the requests to be processed as a batch. Cannot be
    /// null.</param>
    private static void ProcessDataBlockBatch(List<OptimizedRequest> requests)
    {
        // Implementation would batch read/write requests for the same data block
        // This would significantly improve performance by reducing network round trips
        foreach (var request in requests)
        {
            try
            {
                // Process individual request (this would be replaced with actual batch logic)
                request.CompletionSource?.SetResult(true);
            }
            catch (Exception ex)
            {
                request.CompletionSource?.SetException(ex);
            }
        }
    }

    /// <summary>Processes a batch of optimized requests by grouping them for efficient data block access.</summary>
    /// <remarks>Requests are grouped by their associated data block to maximize batch processing efficiency.
    /// If an error occurs while processing a group, the error is logged and processing continues with the remaining
    /// groups.</remarks>
    /// <param name="requests">The collection of optimized requests to process. Cannot be null and must contain only valid requests.</param>
    private static void ProcessRequestBatch(List<OptimizedRequest> requests)
    {
        // Group requests by data block for optimal batch reading
        var groupedRequests = new Dictionary<int, List<OptimizedRequest>>();
        foreach (var request in requests)
        {
            var dataBlock = GetDataBlockFromAddress(request.Tag.Address);
            if (!groupedRequests.TryGetValue(dataBlock, out var group))
            {
                group = [];
                groupedRequests[dataBlock] = group;
            }

            group.Add(request);
        }

        var orderedGroups = new List<KeyValuePair<int, List<OptimizedRequest>>>(groupedRequests);
        orderedGroups.Sort(static (left, right) => right.Value.Count.CompareTo(left.Value.Count));

        foreach (var group in orderedGroups)
        {
            try
            {
                ProcessDataBlockBatch(group.Value);
            }
            catch (Exception ex)
            {
                // Log error and continue with other groups
                System.Diagnostics.Debug.WriteLine($"Batch processing error for DB{group.Key}: {ex.Message}");
            }
        }
    }

    /// <summary>Extracts the data block number from a PLC address string in the format "DB{number}.{...}".</summary>
    /// <remarks>This method returns -1 if the address is null, empty, does not start with "DB", or does not
    /// contain a valid data block number before the first dot.</remarks>
    /// <param name="address">The PLC address string from which to extract the data block number. The address must start with "DB" followed by
    /// the block number and a dot (e.g., "DB10.DBX0.0").</param>
    /// <returns>The data block number as an integer if the address is valid and contains a parsable block number; otherwise, -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDataBlockFromAddress(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return -1;
        }

        var nonNullAddress = address!;

        if (!nonNullAddress.StartsWith("DB", StringComparison.Ordinal))
        {
            return -1;
        }

        var dotIndex = nonNullAddress.IndexOf('.');
        if (dotIndex <= 2)
        {
            return -1;
        }

        return int.TryParse(nonNullAddress[2..dotIndex], out var dbNumber) ? dbNumber : -1;
    }

    /// <summary>Processes a batch of queued requests, up to the configured maximum batch size.</summary>
    /// <remarks>This method is intended to be invoked by a timer or background scheduler. If the processing
    /// lock cannot be acquired within 100 milliseconds, the method exits without processing any requests. The method
    /// processes requests in batches to improve throughput and efficiency.</remarks>
    /// <param name="state">An optional state object provided by the timer or scheduling mechanism. This parameter is not used by the
    /// method.</param>
    private async void ProcessBatchedRequests(object? state)
    {
        // Quick timeout to avoid blocking
        if (!await _processingLock.WaitAsync(100))
        {
            return;
        }

        try
        {
            var requests = new List<OptimizedRequest>();

            // Dequeue up to maxBatchSize requests
            for (var processed = 0; _requestQueue.TryDequeue(out var request) && processed < _maxBatchSize; processed++)
            {
                requests.Add(request);
            }

            if (requests.Count > 0)
            {
                ProcessRequestBatch(requests);
            }
        }
        finally
        {
            _ = _processingLock.Release();
        }
    }

    /// <summary>Calculates the ratio of cache hits to total cache requests.</summary>
    /// <remarks>The cache hit ratio provides an indication of how effectively the cache is serving requests.
    /// A higher ratio suggests better cache performance. If the cache is empty or no requests have been made, the
    /// method returns 0.0.</remarks>
    /// <returns>A double value representing the cache hit ratio. Returns 0.0 if there are no cache entries or requests.</returns>
    private double CalculateCacheHitRatio()
    {
        if (_valueCache.IsEmpty)
        {
            return 0.0;
        }

        var totalHits = 0L;
        foreach (var cachedValue in _valueCache.Values)
        {
            totalHits += cachedValue.HitCount;
        }

        var totalRequests = _valueCache.Count + totalHits;

        return totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;
    }
}
