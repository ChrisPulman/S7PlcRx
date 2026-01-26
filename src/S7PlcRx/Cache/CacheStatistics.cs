// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Provides statistical information about the state and performance of a cache, including entry counts, hit rates, and
/// entry timestamps.
/// </summary>
/// <remarks>Use this class to monitor cache usage patterns and effectiveness. The statistics can help identify
/// cache performance issues or guide tuning decisions. All values represent a snapshot at the time the object is
/// created or updated; they do not update automatically.</remarks>
public sealed class CacheStatistics
{
    /// <summary>Gets or sets the total number of cached entries.</summary>
    public int TotalEntries { get; set; }

    /// <summary>Gets or sets the total number of cache hits.</summary>
    public long TotalHits { get; set; }

    /// <summary>Gets or sets the cache hit rate (0.0 to 1.0).</summary>
    public double HitRate { get; set; }

    /// <summary>Gets or sets the timestamp of the oldest cache entry.</summary>
    public DateTime OldestEntry { get; set; }

    /// <summary>Gets or sets the timestamp of the newest cache entry.</summary>
    public DateTime NewestEntry { get; set; }

    /// <summary>
    /// Gets the cached value count.
    /// </summary>
    /// <value>
    /// The cached value count.
    /// </value>
    public int CachedValueCount { get; internal set; }

    /// <summary>
    /// Gets the pending request count.
    /// </summary>
    /// <value>
    /// The pending request count.
    /// </value>
    public int PendingRequestCount { get; internal set; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    /// <value>
    /// The cache hit ratio.
    /// </value>
    public double CacheHitRatio { get; internal set; }
}
