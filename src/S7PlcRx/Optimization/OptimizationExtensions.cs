// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Cache;

namespace S7PlcRx.Optimization;

/// <summary>
/// Provides extension methods for IRxS7 to enable optimized tag monitoring, intelligent value caching, and cache
/// management for PLC data access.
/// </summary>
/// <remarks>These extensions enhance performance and usability when interacting with PLC tags by offering
/// adaptive polling, caching strategies, and cache statistics. All methods require a valid IRxS7 instance and are
/// designed to be thread-safe. Use these methods to reduce unnecessary network traffic, improve responsiveness, and
/// monitor tag changes efficiently.</remarks>
public static class OptimizationExtensions
{
    private static readonly ConcurrentDictionary<string, CachedTagValue> _valueCache = new();
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Observes changes to a specified PLC tag and emits significant value changes as a stream of smart change events.
    /// </summary>
    /// <remarks>The returned observable emits a new event only when the tag value changes by at least the
    /// specified threshold and after the debounce interval has elapsed. This method uses a publish/subscribe mechanism
    /// to share the observable sequence among multiple subscribers.</remarks>
    /// <typeparam name="T">The type of the tag value to monitor.</typeparam>
    /// <param name="plc">The PLC instance that provides access to the tag to be monitored. Cannot be null.</param>
    /// <param name="tagName">The name of the tag to observe for changes. Cannot be null or empty.</param>
    /// <param name="changeThreshold">The minimum change required between consecutive tag values to consider the change significant. Use 0.0 to report
    /// all changes.</param>
    /// <param name="debounceMs">The minimum interval, in milliseconds, between emitted change events. Used to debounce rapid changes.</param>
    /// <returns>An observable sequence of smart tag change events containing details about each significant change detected in
    /// the tag value.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="tagName"/> is null or empty.</exception>
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
    /// Asynchronously retrieves the value of the specified PLC tag, using a cached value if available and not expired.
    /// </summary>
    /// <remarks>If a cached value for the specified tag exists and has not expired, it is returned
    /// immediately. Otherwise, the method reads a fresh value from the PLC and updates the cache. This method is
    /// thread-safe.</remarks>
    /// <typeparam name="T">The type of the value to retrieve from the PLC tag.</typeparam>
    /// <param name="plc">The PLC interface used to access the tag value. Cannot be null.</param>
    /// <param name="tagName">The name of the PLC tag to read. Cannot be null or empty.</param>
    /// <param name="cacheTimeout">The maximum duration for which a cached value is considered valid. If null, a default of one second is used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the value of the specified tag, or
    /// the cached value if it is still valid. Returns null if the tag value cannot be retrieved.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="tagName"/> is null or empty.</exception>
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
    /// Clears cached values for the specified PLC instance, optionally restricting the operation to a single tag.
    /// </summary>
    /// <remarks>This method is thread-safe. Clearing the cache may affect subsequent read operations, which
    /// will retrieve fresh values from the PLC rather than cached results.</remarks>
    /// <param name="plc">The PLC instance whose cache entries will be cleared. Cannot be null.</param>
    /// <param name="tagName">The name of the tag to clear from the cache. If null or empty, all cache entries for the specified PLC are
    /// cleared.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
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
    /// Retrieves cache usage statistics for the specified PLC instance, including entry count, hit count, hit rate, and
    /// entry timestamps.
    /// </summary>
    /// <remarks>This method provides insight into the cache performance and usage for a particular PLC. The
    /// statistics can be used to monitor cache effectiveness or diagnose caching issues. The method is
    /// thread-safe.</remarks>
    /// <param name="plc">The PLC instance for which to obtain cache statistics. Cannot be null.</param>
    /// <returns>A CacheStatistics object containing aggregated cache metrics for the specified PLC. If no cache entries exist
    /// for the PLC, the statistics will reflect zero entries and hits.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
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

    /// <summary>
    /// Determines whether the change between two values is considered significant based on the specified threshold.
    /// </summary>
    /// <remarks>For numeric types, the method compares the absolute difference to the threshold. For
    /// non-numeric types, any inequality is considered significant unless the threshold is less than or equal to zero.
    /// If either value is <see langword="null"/>, or both are equal, the change is not considered
    /// significant.</remarks>
    /// <typeparam name="T">The type of the values to compare. Must support equality comparison and, for threshold-based comparison,
    /// conversion to <see cref="double"/> if numeric.</typeparam>
    /// <param name="previous">The previous value to compare. Can be <see langword="null"/>.</param>
    /// <param name="current">The current value to compare. Can be <see langword="null"/>.</param>
    /// <param name="threshold">The minimum difference required for a numeric change to be considered significant. If less than or equal to
    /// zero, any change is considered significant.</param>
    /// <returns>Returns <see langword="true"/> if the change between <paramref name="previous"/> and <paramref name="current"/>
    /// is significant according to the threshold; otherwise, <see langword="false"/>.</returns>
    private static bool IsSignificantChange<T>(T? previous, T? current, double threshold)
    {
        if (previous == null || current == null)
        {
            return false;
        }

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

    /// <summary>
    /// Calculates the absolute difference between two numeric values of the specified type.
    /// </summary>
    /// <remarks>This method only operates on numeric types that can be converted to <see cref="double"/>. If
    /// the type parameter <typeparamref name="T"/> is not numeric or conversion fails, the method returns 0.</remarks>
    /// <typeparam name="T">The numeric type to compare. Must be a type supported by <see cref="Convert.ToDouble(object)"/>.</typeparam>
    /// <param name="previous">The previous value to compare. If <paramref name="previous"/> is <see langword="null"/>, the method returns 0.</param>
    /// <param name="current">The current value to compare. If <paramref name="current"/> is <see langword="null"/>, the method returns 0.</param>
    /// <returns>The absolute difference between <paramref name="current"/> and <paramref name="previous"/> as a <see
    /// cref="double"/>. Returns 0 if either value is <see langword="null"/>, not numeric, or cannot be converted.</returns>
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

    /// <summary>
    /// Determines whether the specified type represents a numeric value, including nullable numeric types.
    /// </summary>
    /// <remarks>This method considers the following types as numeric: byte, sbyte, short, ushort, int, uint,
    /// long, ulong, float, double, and decimal, as well as their nullable forms.</remarks>
    /// <param name="type">The type to evaluate. This can be a non-nullable or nullable numeric type.</param>
    /// <returns>true if the type is a numeric type (such as int, double, decimal, or their nullable equivalents); otherwise,
    /// false.</returns>
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
