// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Represents a value stored in the cache along with its timestamp and access count.
/// </summary>
/// <param name="value">The value to be cached. Can be null.</param>
/// <param name="timestamp">The date and time when the value was cached, in UTC.</param>
/// <param name="hitCount">The initial number of times the cached value has been accessed. Defaults to 0.</param>
internal class CachedValue(object? value, DateTime timestamp, long hitCount = 0)
{
    /// <summary>Gets the cached value.</summary>
    public object? Value { get; } = value;

    /// <summary>Gets when the value was cached.</summary>
    public DateTime Timestamp { get; } = timestamp;

    /// <summary>Gets or sets the number of times this value has been accessed.</summary>
    public long HitCount { get; set; } = hitCount;

    /// <summary>Gets whether this cached value has expired.</summary>
    /// <param name="maxAge">The maximum age for cached values.</param>
    /// <returns>True if the value has expired.</returns>
    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - Timestamp > maxAge;
}
