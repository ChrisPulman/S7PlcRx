// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Advanced;

/// <summary>
/// Dictionary equality comparer for change detection in observables.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    /// <summary>
    /// Determines whether the specified dictionaries are equal.
    /// </summary>
    /// <param name="x">The first dictionary.</param>
    /// <param name="y">The second dictionary.</param>
    /// <returns>True if the dictionaries are equal, false otherwise.</returns>
    public bool Equals(Dictionary<TKey, TValue>? x, Dictionary<TKey, TValue>? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x == null || y == null)
        {
            return false;
        }

        if (x.Count != y.Count)
        {
            return false;
        }

        foreach (var kvp in x)
        {
            if (!y.TryGetValue(kvp.Key, out var value) || !EqualityComparer<TValue>.Default.Equals(kvp.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for the specified dictionary.
    /// </summary>
    /// <param name="obj">The dictionary.</param>
    /// <returns>A hash code for the dictionary.</returns>
    public int GetHashCode(Dictionary<TKey, TValue> obj)
    {
        if (obj == null)
        {
            return 0;
        }

        var hash = 0;
        foreach (var kvp in obj)
        {
            hash ^= kvp.Key.GetHashCode();
            if (kvp.Value != null)
            {
                hash ^= kvp.Value.GetHashCode();
            }
        }

        return hash;
    }
}
