// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx.Advanced;

/// <summary>
/// Provides an equality comparer for dictionaries that determines equality based on their key-value pairs.
/// </summary>
/// <remarks>This comparer considers two dictionaries equal if they contain the same number of key-value pairs and
/// each key in one dictionary exists in the other with an equal value, as determined by the default equality comparer
/// for the value type. The order of key-value pairs does not affect equality. This comparer can be used to compare
/// dictionaries in collections such as hash sets or as keys in other dictionaries.</remarks>
/// <typeparam name="TKey">The type of keys in the dictionaries. Must be non-nullable.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionaries.</typeparam>
public class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    /// <summary>
    /// Determines whether two dictionaries are equal by comparing their keys and values.
    /// </summary>
    /// <remarks>Dictionaries are considered equal if they have the same number of elements and each key in
    /// one dictionary exists in the other with an equal value, as determined by the default equality comparer for the
    /// value type. The order of elements is not considered.</remarks>
    /// <param name="x">The first dictionary to compare, or null.</param>
    /// <param name="y">The second dictionary to compare, or null.</param>
    /// <returns>true if both dictionaries contain the same keys and associated values; otherwise, false. Returns true if both
    /// are null.</returns>
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
    /// Returns a hash code for the specified dictionary instance.
    /// </summary>
    /// <remarks>The hash code is computed by combining the hash codes of the dictionary's keys and values.
    /// The order of elements in the dictionary does not affect the resulting hash code.</remarks>
    /// <param name="obj">The dictionary for which to compute the hash code. Can be null.</param>
    /// <returns>A hash code for the specified dictionary. Returns 0 if <paramref name="obj"/> is null.</returns>
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
