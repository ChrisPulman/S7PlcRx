// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Cache;

namespace S7PlcRx.Optimization;

/// <summary>
/// Optimization extensions providing intelligent batching, caching, and performance monitoring.
/// </summary>
public static class S7OptimizationExtensions
{
    private static readonly ConcurrentDictionary<string, CachedTagValue> _valueCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Creates an intelligent tag monitor with adaptive polling based on change frequency.
    /// </summary>
    /// <typeparam name="T">The type of values to monitor.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The tag to monitor.</param>
    /// <param name="changeThreshold">Minimum change threshold for numeric types.</param>
    /// <param name="debounceMs">Debounce time in milliseconds to avoid rapid changes.</param>
    /// <returns>An observable that provides optimized tag monitoring.</returns>
    public static IObservable<SmartTagChange<T>> MonitorTagSmart<T>(
        this IRxS7 plc,
        string tagName,
        double changeThreshold = 0.0,
        int debounceMs = 100)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
        }

        return plc.Observe<T>(tagName)
            .Timestamp()
            .Scan(
                new { Previous = default(T), Current = default(T), PrevTime = DateTimeOffset.MinValue, IsFirst = true },
                (acc, timestamped) => new
                {
                    Previous = acc.Current,
                    Current = timestamped.Value,
                    PrevTime = acc.IsFirst ? timestamped.Timestamp : acc.PrevTime,
                    IsFirst = false
                })
            .Where(state => !state.IsFirst)
            .Where(state => IsSignificantChange(state.Previous, state.Current, changeThreshold))
            .Select(state => new SmartTagChange<T>
            {
                TagName = tagName,
                PreviousValue = state.Previous,
                CurrentValue = state.Current,
                ChangeTime = DateTimeOffset.UtcNow,
                ChangeAmount = CalculateChangeAmount(state.Previous, state.Current)
            })
            .Where(change => change != null)
            .Sample(TimeSpan.FromMilliseconds(debounceMs))
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Reads a value with intelligent caching to improve performance.
    /// </summary>
    /// <typeparam name="T">The type of value to read.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The tag name to read.</param>
    /// <param name="cacheTimeout">Cache timeout duration (default: 1 second).</param>
    /// <returns>The cached or fresh value.</returns>
    public static async Task<T?> ValueCached<T>(
        this IRxS7 plc,
        string tagName,
        TimeSpan? cacheTimeout = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
        }

        var timeout = cacheTimeout ?? TimeSpan.FromSeconds(1);
        var cacheKey = $"{plc.IP}_{tagName}";

        lock (_cacheLock)
        {
            if (_valueCache.TryGetValue(cacheKey, out var cachedValue) &&
                DateTime.UtcNow - cachedValue.Timestamp <= timeout)
            {
                cachedValue.HitCount++;
                return cachedValue.Value is T value ? value : default;
            }
        }

        // Read fresh value
        var freshValue = await plc.Value<T>(tagName);

        lock (_cacheLock)
        {
            _valueCache[cacheKey] = new CachedTagValue
            {
                Value = freshValue,
                Timestamp = DateTime.UtcNow,
                HitCount = 0
            };
        }

        return freshValue;
    }

    /// <summary>
    /// Clears the cache for all tags or a specific tag.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">Optional specific tag name to clear, or null to clear all.</param>
    public static void ClearCache(this IRxS7 plc, string? tagName = null)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        lock (_cacheLock)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                var keysToRemove = _valueCache.Keys
                    .Where(key => key.StartsWith($"{plc.IP}_"))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _valueCache.TryRemove(key, out _);
                }
            }
            else
            {
                var cacheKey = $"{plc.IP}_{tagName}";
                _valueCache.TryRemove(cacheKey, out _);
            }
        }
    }

    /// <summary>
    /// Gets cache statistics for performance monitoring.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <returns>Cache performance statistics.</returns>
    public static CacheStatistics GetCacheStatistics(this IRxS7 plc)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        lock (_cacheLock)
        {
            var plcEntries = _valueCache
                .Where(kvp => kvp.Key.StartsWith($"{plc.IP}_"))
                .ToList();

            var totalHits = plcEntries.Sum(kvp => kvp.Value.HitCount);
            var totalEntries = plcEntries.Count;

            return new CacheStatistics
            {
                TotalEntries = totalEntries,
                TotalHits = totalHits,
                HitRate = totalEntries > 0 ? (double)totalHits / (totalHits + totalEntries) : 0.0,
                OldestEntry = plcEntries.Count > 0 ? plcEntries.Min(kvp => kvp.Value.Timestamp) : DateTime.UtcNow,
                NewestEntry = plcEntries.Count > 0 ? plcEntries.Max(kvp => kvp.Value.Timestamp) : DateTime.UtcNow
            };
        }
    }

    private static bool IsSignificantChange<T>(T? previous, T? current, double threshold)
    {
        if (EqualityComparer<T>.Default.Equals(previous, current))
        {
            return false;
        }

        if (threshold <= 0)
        {
            return true;
        }

        if (IsNumericType(typeof(T)) && previous != null && current != null)
        {
            try
            {
                var prevVal = Convert.ToDouble(previous);
                var currVal = Convert.ToDouble(current);
                return Math.Abs(currVal - prevVal) >= threshold;
            }
            catch
            {
                return true;
            }
        }

        return true;
    }

    private static double CalculateChangeAmount<T>(T? previous, T? current)
    {
        if (IsNumericType(typeof(T)) && previous != null && current != null)
        {
            try
            {
                var prevVal = Convert.ToDouble(previous);
                var currVal = Convert.ToDouble(current);
                return Math.Abs(currVal - prevVal);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static bool IsNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType == typeof(byte) ||
               underlyingType == typeof(sbyte) ||
               underlyingType == typeof(short) ||
               underlyingType == typeof(ushort) ||
               underlyingType == typeof(int) ||
               underlyingType == typeof(uint) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(ulong) ||
               underlyingType == typeof(float) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(decimal);
    }
}
