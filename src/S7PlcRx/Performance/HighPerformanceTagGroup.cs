// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Advanced;

namespace S7PlcRx.Performance;

/// <summary>
/// High-performance tag group for optimized batch operations.
/// </summary>
/// <typeparam name="T">The Type.</typeparam>
/// <seealso cref="System.IDisposable" />
public class HighPerformanceTagGroup<T> : IDisposable
{
    private readonly IRxS7 _plc;
    private readonly string[] _tagNames;
    private readonly ConcurrentDictionary<string, T?> _currentValues = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HighPerformanceTagGroup{T}"/> class.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="tagNames">The tag names in the group.</param>
    /// <exception cref="System.ArgumentNullException">plc.</exception>
    /// <exception cref="System.ArgumentException">
    /// Group name cannot be null or whitespace. - groupName
    /// or
    /// Tag names cannot be null or empty. - tagNames.
    /// </exception>
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
            plc.AddUpdateTagItem<T>(tagName, tagName)
                .SetTagPollIng(false); // Disable individual polling for performance
        }
    }

    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// Gets the current values for all tags in the group.
    /// </summary>
    public IReadOnlyDictionary<string, T?> CurrentValues => _currentValues;

    /// <summary>
    /// Observes all values in the group as a combined stream.
    /// </summary>
    /// <returns>An observable of the complete group state.</returns>
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
            .Where(t => t != null && _tagNames.Contains(t.Name))
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
    /// Reads all values in the group efficiently.
    /// </summary>
    /// <typeparam name="T">The type of values to read.</typeparam>
    /// <returns>A dictionary of tag names and values.</returns>
    public async Task<Dictionary<string, T?>> ReadAll() => await _plc.ValueBatch<T>(_tagNames);

    /// <summary>
    /// Writes multiple values to the group.
    /// </summary>
    /// <typeparam name="T">The type of values to write.</typeparam>
    /// <param name="values">The values to write.</param>
    /// <returns>A task representing the write operation.</returns>
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

                _disposed = true;
            }

            _disposed = true;
        }
    }
}
