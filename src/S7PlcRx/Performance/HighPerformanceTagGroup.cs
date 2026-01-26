// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Advanced;

namespace S7PlcRx.Performance;

/// <summary>
/// Provides high-performance batch operations for reading, writing, and observing a group of PLC tags as a single unit.
/// </summary>
/// <remarks>This class is designed to optimize communication with a PLC by grouping related tags and minimizing
/// individual polling. It supports efficient batch reads and writes, and exposes an observable stream for monitoring
/// group state changes. Instances of this class are not thread-safe; external synchronization may be required if
/// accessed concurrently.</remarks>
/// <typeparam name="T">The type of value associated with each PLC tag in the group.</typeparam>
public class HighPerformanceTagGroup<T> : IDisposable
{
    private readonly IRxS7 _plc;
    private readonly string[] _tagNames;
    private readonly ConcurrentDictionary<string, T?> _currentValues = new();
    private readonly List<string> _tagsAdded = [];
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
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException("Group name cannot be null or whitespace.", nameof(groupName));
        }

        if (tagNames == null || tagNames.Length == 0)
        {
            throw new ArgumentException("Tag names cannot be null or empty.", nameof(tagNames));
        }

        _plc = plc;
        GroupName = groupName;
        _tagNames = tagNames;
        foreach (var tagName in tagNames)
        {
            if (!plc.TagList.ContainsKey(tagName) && tagName.StartsWith("DB"))
            {
                plc.AddUpdateTagItem<T>(tagName, tagName)
                .SetTagPollIng(false); // Disable individual polling for performance
                _tagsAdded.Add(tagName);
            }
        }
    }

    /// <summary>
    /// Gets the name of the group associated with this instance.
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// Gets a read-only dictionary containing the current values associated with each key.
    /// </summary>
    public IReadOnlyDictionary<string, T?> CurrentValues => _currentValues;

    /// <summary>
    /// Observes changes to the group of tags and provides a stream of their current values.
    /// </summary>
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
                _plc.GetTag(tagName).SetTagPollIng(true);
            }
        }

        return _plc.ObserveAll
            .Where(t => t != null && _tagNames.Contains(t.Name) && t.Value is T)
            .Select(t => new { t!.Name, t.Value })
            .Scan(
                _currentValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
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
    public async Task<Dictionary<string, T?>> ReadAll() => await _plc.ValueBatch<T>(_tagNames);

    /// <summary>
    /// Writes all specified values to the PLC in a single batch operation.
    /// </summary>
    /// <remarks>Entries in the dictionary with tag names not recognized by the PLC are ignored. This method
    /// performs the write operation asynchronously and does not block the calling thread.</remarks>
    /// <param name="values">A dictionary containing tag names as keys and their corresponding values to be written. Only entries with tag
    /// names recognized by the PLC will be processed.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async Task WriteAll(Dictionary<string, T> values)
    {
        var filteredValues = values.Where(kvp => _tagNames.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        await _plc.ValueBatch(filteredValues);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var tagName in _tagNames)
                {
                    _plc.GetTag(tagName)
                        .SetTagPollIng(false); // Disable individual polling for performance
                }

                // Remove tags added by this group
                foreach (var tagName in _tagsAdded)
                {
                    if (_plc.TagList.ContainsKey(tagName))
                    {
                        _plc.RemoveTagItem(tagName);
                    }
                }

                _tagsAdded.Clear();

                _disposed = true;
            }

            _disposed = true;
        }
    }
}
