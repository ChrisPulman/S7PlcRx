// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Represents a cached value along with metadata about its storage and usage.
/// </summary>
/// <remarks>This class is typically used to store a value retrieved from a data source, along with the time it
/// was cached and the number of times it has been accessed. It is intended for use in caching scenarios where tracking
/// cache usage and freshness is important.</remarks>
public sealed class CachedTagValue
{
    /// <summary>Gets or sets the cached value.</summary>
    public object? Value { get; set; }

    /// <summary>Gets or sets when the value was cached.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the number of cache hits.</summary>
    public long HitCount { get; set; }
}
