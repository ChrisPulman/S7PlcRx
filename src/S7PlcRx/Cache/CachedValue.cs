// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Represents a cached value with metadata.
/// </summary>
internal class CachedValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CachedValue"/> class.
    /// </summary>
    /// <param name="value">The cached value.</param>
    /// <param name="timestamp">When the value was cached.</param>
    /// <param name="hitCount">The number of times this value has been accessed.</param>
    public CachedValue(object? value, DateTime timestamp, long hitCount = 0)
    {
        Value = value;
        Timestamp = timestamp;
        HitCount = hitCount;
    }

    /// <summary>Gets the cached value.</summary>
    public object? Value { get; }

    /// <summary>Gets when the value was cached.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Gets or sets the number of times this value has been accessed.</summary>
    public long HitCount { get; set; }

    /// <summary>Gets whether this cached value has expired.</summary>
    /// <param name="maxAge">The maximum age for cached values.</param>
    /// <returns>True if the value has expired.</returns>
    public bool IsExpired(TimeSpan maxAge) => DateTime.UtcNow - Timestamp > maxAge;
}
