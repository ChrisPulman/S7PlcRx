// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using S7PlcRx.Cache;

namespace S7PlcRx.Optimization;

/// <summary>
/// Advanced batch processing and caching optimizations for S7 PLC communication.
/// </summary>
internal class OptimizationEngine : IDisposable
{
    private readonly ConcurrentQueue<OptimizedRequest> _requestQueue = new();
    private readonly ConcurrentDictionary<string, CachedValue> _valueCache = new();
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly int _maxBatchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizationEngine"/> class.
    /// </summary>
    /// <param name="batchIntervalMs">The batch processing interval in milliseconds.</param>
    /// <param name="maxBatchSize">The maximum batch size per request.</param>
    public OptimizationEngine(int batchIntervalMs = 50, int maxBatchSize = 20)
    {
        _maxBatchSize = maxBatchSize;
        _batchTimer = new Timer(
            ProcessBatchedRequests,
            null,
            TimeSpan.FromMilliseconds(batchIntervalMs),
            TimeSpan.FromMilliseconds(batchIntervalMs));
    }

    /// <summary>
    /// Gets the current cache statistics.
    /// </summary>
    public CacheStatistics CacheStats => new()
    {
        CachedValueCount = _valueCache.Count,
        PendingRequestCount = _requestQueue.Count,
        CacheHitRatio = CalculateCacheHitRatio()
    };

    /// <summary>
    /// Enqueues a request for batch processing.
    /// </summary>
    /// <param name="request">The request to enqueue.</param>
    public void EnqueueRequest(OptimizedRequest request) => _requestQueue.Enqueue(request);

    /// <summary>
    /// Gets a cached value if available and valid.
    /// </summary>
    /// <param name="tagName">The tag name.</param>
    /// <param name="maxAge">The maximum age for cached values.</param>
    /// <returns>The cached value if available, null otherwise.</returns>
    public object? GetCachedValue(string tagName, TimeSpan maxAge)
    {
        if (_valueCache.TryGetValue(tagName, out var cachedValue) &&
            DateTime.UtcNow - cachedValue.Timestamp <= maxAge)
        {
            cachedValue.HitCount++;
            return cachedValue.Value;
        }

        return null;
    }

    /// <summary>
    /// Updates the cache with a new value.
    /// </summary>
    /// <param name="tagName">The tag name.</param>
    /// <param name="value">The value to cache.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateCache(string tagName, object value) => _valueCache.AddOrUpdate(
            tagName,
            new CachedValue(value, DateTime.UtcNow),
            (_, existing) => new CachedValue(value, DateTime.UtcNow, existing.HitCount));

    /// <summary>
    /// Clears expired cache entries.
    /// </summary>
    /// <param name="maxAge">The maximum age for cached values.</param>
    public void ClearExpiredCache(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var expiredKeys = _valueCache
            .Where(kvp => kvp.Value.Timestamp < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _valueCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Disposes the optimization engine.
    /// </summary>
    public void Dispose()
    {
        _batchTimer?.Dispose();
        _processingLock?.Dispose();
        _valueCache.Clear();
    }

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

    private static void ProcessRequestBatch(List<OptimizedRequest> requests)
    {
        // Group requests by data block for optimal batch reading
        var groupedRequests = requests
            .GroupBy(r => GetDataBlockFromAddress(r.Tag.Address))
            .OrderByDescending(g => g.Count()); // Process larger groups first

        foreach (var group in groupedRequests)
        {
            try
            {
                ProcessDataBlockBatch([.. group]);
            }
            catch (Exception ex)
            {
                // Log error and continue with other groups
                System.Diagnostics.Debug.WriteLine($"Batch processing error for DB{group.Key}: {ex.Message}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDataBlockFromAddress(string? address)
    {
        if (string.IsNullOrEmpty(address) || address?.StartsWith("DB") == false)
        {
            return -1;
        }

        var dotIndex = address!.IndexOf('.');
        if (dotIndex <= 2)
        {
            return -1;
        }

        if (int.TryParse(address.Substring(2, dotIndex - 2), out var dbNumber))
        {
            return dbNumber;
        }

        return -1;
    }

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
            _processingLock.Release();
        }
    }

    private double CalculateCacheHitRatio()
    {
        if (_valueCache.IsEmpty)
        {
            return 0.0;
        }

        var totalHits = _valueCache.Values.Sum(v => v.HitCount);
        var totalRequests = _valueCache.Count + totalHits;

        return totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;
    }
}
