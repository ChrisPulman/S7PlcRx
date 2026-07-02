// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
#if REACTIVE_SHIM
using S7PlcRx.Reactive.Advanced;
#else
using S7PlcRx.Advanced;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Performance;
#else
namespace S7PlcRx.Performance;
#endif

/// <summary>Provides high-performance batch operations for reading, writing, and observing a group of PLC tags as a single unit.</summary>
/// <remarks>This class is designed to optimize communication with a PLC by grouping related tags and minimizing
/// individual polling. It supports efficient batch reads and writes, and exposes an observable stream for monitoring
/// group state changes. Instances of this class are not thread-safe; external synchronization may be required if
/// accessed concurrently.</remarks>
/// <typeparam name="T">The type of value associated with each PLC tag in the group.</typeparam>
public class HighPerformanceTagGroup<T> : IDisposable
{
    /// <summary>Stores the p l c used by this instance.</summary>
    private readonly IRxS7 _plc;

    /// <summary>Stores the t ag na m e s used by this instance.</summary>
    private readonly string[] _tagNames;

    /// <summary>Stores the c ur re nt va lu e s used by this instance.</summary>
    private readonly ConcurrentDictionary<string, T?> _currentValues = new();

    /// <summary>Stores the t ag sa dd e d used by this instance.</summary>
    private readonly List<string> _tagsAdded = [];

    /// <summary>Stores the d is po s e d used by this instance.</summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HighPerformanceTagGroup{T}"/> class, associating a set of tag names with a specified.
    /// PLC for optimized group operations.
    /// </summary>
    /// <remarks>Tag names starting with "DB" that are not already present in the PLC's tag list will be added
    /// with individual polling disabled to improve performance when accessing the group.</remarks>
    /// <param name="plc">The PLC connection used to manage and access the specified tags.</param>
    /// <param name="groupName">The name assigned to this tag group. Cannot be null or whitespace.</param>
    /// <param name="tagNames">An array of tag names to include in the group. Cannot be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="plc"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="groupName"/> is null, whitespace, or if <paramref name="tagNames"/> is null or empty.</exception>
    public HighPerformanceTagGroup(IRxS7 plc, string groupName, string[] tagNames)
    {
        if (plc is null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
#else
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("Group name cannot be null or whitespace.", nameof(groupName));
        }
#endif

        if (tagNames is null || tagNames.Length == 0)
        {
            throw new ArgumentException("Tag names cannot be null or empty.", nameof(tagNames));
        }

        _plc = plc;
        GroupName = groupName;
        _tagNames = tagNames;
        foreach (var tagName in tagNames)
        {
            if (!plc.TagList.ContainsKey(tagName) && tagName.StartsWith("DB", StringComparison.Ordinal))
            {
                _ = plc.AddUpdateTagItem<T>(tagName, tagName)
                .SetTagPollIng(false); // Disable individual polling for performance
                _tagsAdded.Add(tagName);
            }
        }
    }

    /// <summary>Gets the name of the group associated with this instance.</summary>
    public string GroupName { get; }

    /// <summary>Gets a read-only dictionary containing the current values associated with each key.</summary>
    public IReadOnlyDictionary<string, T?> CurrentValues => _currentValues;

    /// <summary>Observes changes to the group of tags and provides a stream of their current values.</summary>
    /// <remarks>Individual polling is enabled for each tag in the group to ensure timely updates. The
    /// returned observable emits only when the values of the tags change, and subscribers receive the most recent state
    /// of all tags in the group. The sequence is shared among all subscribers and remains active as long as there is at
    /// least one subscription.</remarks>
    /// <returns>An observable sequence that emits a dictionary containing the latest values for each tag in the group. Each
    /// dictionary maps tag names to their corresponding values of type T. The sequence emits a new dictionary whenever
    /// any tag value changes.</returns>
    public IObservable<Dictionary<string, T?>> ObserveGroup()
    {
        foreach (var tagName in _tagNames)
        {
            if (_plc.TagList.ContainsKey(tagName))
            {
                // Enable individual polling for each tag to ensure updates
                _ = _plc.GetTag(tagName).SetTagPollIng(true);
            }
        }

        return _plc.ObserveAll
            .Where(t => t is { Value: T } && ContainsTagName(t.Name))
            .Select(t => (t!.Name, t.Value))
            .Scan(
                CreateCurrentValuesSnapshot(),
                (acc, change) =>
                {
                    acc[change.Name!] = (T?)change.Value;
                    return new Dictionary<string, T?>(acc);
                })
            .DistinctUntilChanged()
            .Publish()
            .RefCount();
    }

    /// <summary>
    /// Asynchronously reads the values of all configured PLC tags and returns a dictionary mapping tag names to their
    /// corresponding values.
    /// </summary>
    /// <remarks>The returned dictionary includes an entry for each tag in the configured set, regardless of
    /// whether the value was successfully read. This method is thread-safe and can be awaited. The order of entries in
    /// the dictionary is not guaranteed.</remarks>
    /// <returns>A dictionary containing the tag names as keys and their associated values of type <typeparamref name="T"/> as
    /// values. If a tag value cannot be read, its value will be <see langword="null"/>.</returns>
    public Task<Dictionary<string, T?>> ReadAll() => _plc.ValueBatch<T>(_tagNames);

    /// <summary>Writes all specified values to the PLC in a single batch operation.</summary>
    /// <remarks>Entries in the dictionary with tag names not recognized by the PLC are ignored. This method
    /// performs the write operation asynchronously and does not block the calling thread.</remarks>
    /// <param name="values">A dictionary containing tag names as keys and their corresponding values to be written. Only entries with tag
    /// names recognized by the PLC will be processed.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteAll(Dictionary<string, T> values)
    {
        var filteredValues = FilterValues(values);
        await _plc.ValueBatch(filteredValues);
    }

    /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            DisablePolling();
            RemoveAddedTags();
        }

        _disposed = true;
    }

    /// <summary>Creates a current-value snapshot dictionary.</summary>
    /// <returns>The current-value snapshot.</returns>
    private Dictionary<string, T?> CreateCurrentValuesSnapshot()
    {
        var snapshot = new Dictionary<string, T?>(_currentValues.Count);
        foreach (var kvp in _currentValues)
        {
            snapshot[kvp.Key] = kvp.Value;
        }

        return snapshot;
    }

    /// <summary>Filters write values to tags that belong to this group.</summary>
    /// <param name="values">The candidate write values.</param>
    /// <returns>The filtered write values.</returns>
    private Dictionary<string, T> FilterValues(Dictionary<string, T> values)
    {
        var filteredValues = new Dictionary<string, T>();
        foreach (var kvp in values)
        {
            if (ContainsTagName(kvp.Key))
            {
                filteredValues[kvp.Key] = kvp.Value;
            }
        }

        return filteredValues;
    }

    /// <summary>Checks whether a tag name is part of this group.</summary>
    /// <param name="tagName">The tag name to check.</param>
    /// <returns>true when the tag name is part of this group; otherwise, false.</returns>
    private bool ContainsTagName(string? tagName)
    {
        if (tagName is null)
        {
            return false;
        }

        foreach (var currentTagName in _tagNames)
        {
            if (string.Equals(currentTagName, tagName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Disables polling for all tags in the group.</summary>
    private void DisablePolling()
    {
        foreach (var tagName in _tagNames)
        {
            _ = _plc.GetTag(tagName)
                .SetTagPollIng(false); // Disable individual polling for performance
        }
    }

    /// <summary>Removes tags that were added by this group.</summary>
    private void RemoveAddedTags()
    {
        foreach (var tagName in _tagsAdded)
        {
            if (_plc.TagList.ContainsKey(tagName))
            {
                _plc.RemoveTagItem(tagName);
            }
        }

        _tagsAdded.Clear();
    }
}
