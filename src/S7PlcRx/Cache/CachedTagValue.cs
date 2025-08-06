// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Cache;

/// <summary>
/// Cached tag value with performance metrics.
/// </summary>
public sealed class CachedTagValue
{
    /// <summary>Gets or sets the cached value.</summary>
    public object? Value { get; set; }

    /// <summary>Gets or sets when the value was cached.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the number of cache hits.</summary>
    public long HitCount { get; set; }
}
