// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Cache performance statistics.
/// </summary>
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
}
