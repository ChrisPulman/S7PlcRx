// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
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
/// Provides extension methods for IRxS7 to enable optimized tag monitoring, intelligent value caching, and cache
/// management for PLC data access.
/// </summary>
/// <remarks>These extensions enhance performance and usability when interacting with PLC tags by offering
/// adaptive polling, caching strategies, and cache statistics. All methods require a valid IRxS7 instance and are
/// designed to be thread-safe. Use these methods to reduce unnecessary network traffic, improve responsiveness, and
/// monitor tag changes efficiently.</remarks>
public static class OptimizationExtensions
{
    /// <summary>Stores the v al ue ca c h e used by this instance.</summary>
    private static readonly ConcurrentDictionary<string, CachedTagValue> _valueCache = new();

    /// <summary>Stores the lock used to protect shared cache mutations.</summary>
#if NET8_0
    private static readonly object _cacheLock = new();
#else
    private static readonly Lock _cacheLock = new();
#endif

    /// <summary>Provides optimized cache and smart-monitoring extensions for PLC instances.</summary>
    /// <param name="plc">The PLC instance.</param>
    extension(IRxS7 plc)
    {
        /// <summary>Observes changes to a specified PLC tag and emits significant value changes.</summary>
        /// <typeparam name="T">The type of the tag value to monitor.</typeparam>
        /// <param name="tagName">The name of the tag to observe for changes.</param>
        /// <param name="changeThreshold">The minimum change required between consecutive tag values.</param>
        /// <param name="debounceMs">The minimum interval, in milliseconds, between emitted change events.</param>
        /// <returns>An observable sequence of smart tag change events.</returns>
        public IObservable<SmartTagChange<T>> MonitorTagSmart<T>(
            string tagName,
            double changeThreshold = 0.0,
            int debounceMs = 100)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
#else
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
            }
#endif

            return plc.Observe<T>(tagName)
                .Timestamp()
                .Scan(
                    (Previous: default(T), Current: default(T), PrevTime: DateTimeOffset.MinValue, IsFirst: true),
                    (acc, timestamped) => (
                        Previous: acc.Current,
                        Current: timestamped.Value,
                        PrevTime: acc.IsFirst ? timestamped.Timestamp : acc.PrevTime,
                        IsFirst: false))
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
                .Where(change => change is not null)
                .Sample(TimeSpan.FromMilliseconds(debounceMs))
                .Publish()
                .RefCount();
        }

        /// <summary>Asynchronously retrieves the value of the specified PLC tag, using a cached value if available and not expired.</summary>
        /// <typeparam name="T">The type of the value to retrieve from the PLC tag.</typeparam>
        /// <param name="tagName">The name of the PLC tag to read.</param>
        /// <param name="cacheTimeout">The maximum duration for which a cached value is considered valid.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task<T?> ValueCached<T>(
            string tagName,
            TimeSpan? cacheTimeout = null)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(tagName);
#else
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Tag name cannot be null or empty", nameof(tagName));
            }
#endif

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

        /// <summary>Clears cached values for the specified PLC instance.</summary>
        /// <param name="tagName">The name of the tag to clear from the cache.</param>
        public void ClearCache(string? tagName = null)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            lock (_cacheLock)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    var prefix = $"{plc.IP}_";
                    var keysToRemove = new List<string>();
                    foreach (var key in _valueCache.Keys)
                    {
                        if (key.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            keysToRemove.Add(key);
                        }
                    }

                    foreach (var key in keysToRemove)
                    {
                        _ = _valueCache.TryRemove(key, out _);
                    }
                }
                else
                {
                    var cacheKey = $"{plc.IP}_{tagName}";
                    _ = _valueCache.TryRemove(cacheKey, out _);
                }
            }
        }

        /// <summary>Retrieves cache usage statistics for the specified PLC instance.</summary>
        /// <returns>A CacheStatistics object containing aggregated cache metrics for the specified PLC.</returns>
        public CacheStatistics GetCacheStatistics()
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            lock (_cacheLock)
            {
                var prefix = $"{plc.IP}_";
                var totalHits = 0L;
                var totalEntries = 0;
                var now = DateTime.UtcNow;
                var oldestEntry = now;
                var newestEntry = now;
                foreach (var kvp in _valueCache)
                {
                    if (!kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    totalEntries++;
                    UpdateCacheStatistics(kvp.Value, totalEntries, ref totalHits, ref oldestEntry, ref newestEntry);
                }

                return new CacheStatistics
                {
                    TotalEntries = totalEntries,
                    TotalHits = totalHits,
                    HitRate = totalEntries > 0 ? (double)totalHits / (totalHits + totalEntries) : 0.0,
                    OldestEntry = totalEntries > 0 ? oldestEntry : now,
                    NewestEntry = totalEntries > 0 ? newestEntry : now
                };
            }
        }
    }

    /// <summary>Determines whether the change between two values is considered significant based on the specified threshold.</summary>
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
        if (previous is null || current is null)
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

        if (!IsNumericType(typeof(T)))
        {
            return true;
        }

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

    /// <summary>Calculates the absolute difference between two numeric values of the specified type.</summary>
    /// <remarks>This method only operates on numeric types that can be converted to <see cref="double"/>. If
    /// the type parameter <typeparamref name="T"/> is not numeric or conversion fails, the method returns 0.</remarks>
    /// <typeparam name="T">The numeric type to compare. Must be a type supported by <see cref="Convert.ToDouble(object)"/>.</typeparam>
    /// <param name="previous">The previous value to compare. If <paramref name="previous"/> is <see langword="null"/>, the method returns 0.</param>
    /// <param name="current">The current value to compare. If <paramref name="current"/> is <see langword="null"/>, the method returns 0.</param>
    /// <returns>The absolute difference between <paramref name="current"/> and <paramref name="previous"/> as a <see
    /// cref="double"/>. Returns 0 if either value is <see langword="null"/>, not numeric, or cannot be converted.</returns>
    private static double CalculateChangeAmount<T>(T? previous, T? current)
    {
        if (previous is null || current is null || !IsNumericType(typeof(T)))
        {
            return 0;
        }

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

    /// <summary>Determines whether the specified type represents a numeric value, including nullable numeric types.</summary>
    /// <remarks>This method considers the following types as numeric: byte, sbyte, short, ushort, int, uint,
    /// long, ulong, float, double, and decimal, as well as their nullable forms.</remarks>
    /// <param name="type">The type to evaluate. This can be a non-nullable or nullable numeric type.</param>
    /// <returns>true if the type is a numeric type (such as int, double, decimal, or their nullable equivalents); otherwise,
    /// false.</returns>
    private static bool IsNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        var typeCode = Type.GetTypeCode(underlyingType);

        return typeCode is >= TypeCode.SByte and <= TypeCode.Decimal;
    }

    /// <summary>Updates aggregate cache statistics for a single cache entry.</summary>
    /// <param name="value">The cached tag value.</param>
    /// <param name="totalEntries">The total entry count after adding the current value.</param>
    /// <param name="totalHits">The aggregate hit count.</param>
    /// <param name="oldestEntry">The oldest entry timestamp.</param>
    /// <param name="newestEntry">The newest entry timestamp.</param>
    private static void UpdateCacheStatistics(
        CachedTagValue value,
        int totalEntries,
        ref long totalHits,
        ref DateTime oldestEntry,
        ref DateTime newestEntry)
    {
        totalHits += value.HitCount;
        oldestEntry = totalEntries == 1 || value.Timestamp < oldestEntry ? value.Timestamp : oldestEntry;
        newestEntry = totalEntries == 1 || value.Timestamp > newestEntry ? value.Timestamp : newestEntry;
    }
}
