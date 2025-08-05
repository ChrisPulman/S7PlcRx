// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Reactive.Linq;
using S7PlcRx.Advanced;

namespace S7PlcRx.Performance;

/// <summary>
/// High-performance tag group for optimized batch operations.
/// </summary>
public class HighPerformanceTagGroup : IDisposable
{
    private readonly IRxS7 _plc;
    private readonly string[] _tagNames;
    private readonly Timer _updateTimer;
    private readonly ConcurrentDictionary<string, object?> _currentValues = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HighPerformanceTagGroup"/> class.
    /// </summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="groupName">The group name.</param>
    /// <param name="tagNames">The tag names in the group.</param>
    public HighPerformanceTagGroup(IRxS7 plc, string groupName, string[] tagNames)
    {
        _plc = plc;
        GroupName = groupName;
        _tagNames = tagNames;

        // Create update timer for batch reading
        _updateTimer = new Timer(UpdateAllValues, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Gets the group name.
    /// </summary>
    public string GroupName { get; }

    /// <summary>
    /// Gets the current values for all tags in the group.
    /// </summary>
    public IReadOnlyDictionary<string, object?> CurrentValues => _currentValues;

    /// <summary>
    /// Observes all values in the group as a combined stream.
    /// </summary>
    /// <returns>An observable of the complete group state.</returns>
    public IObservable<Dictionary<string, object?>> ObserveGroup() =>
        _plc.ObserveAll
            .Where(t => t != null && _tagNames.Contains(t.Name))
            .Select(t => new { t!.Name, t.Value })
            .Scan(
                _currentValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                (acc, change) =>
                {
                    acc[change.Name!] = change.Value;
                    return new Dictionary<string, object?>(acc);
                })
            .DistinctUntilChanged()
            .Publish()
            .RefCount();

    /// <summary>
    /// Reads all values in the group efficiently.
    /// </summary>
    /// <typeparam name="T">The type of values to read.</typeparam>
    /// <returns>A dictionary of tag names and values.</returns>
    public async Task<Dictionary<string, T?>> ReadAll<T>() => await _plc.ValueBatch<T>(_tagNames);

    /// <summary>
    /// Writes multiple values to the group.
    /// </summary>
    /// <typeparam name="T">The type of values to write.</typeparam>
    /// <param name="values">The values to write.</param>
    /// <returns>A task representing the write operation.</returns>
    public async Task WriteAll<T>(Dictionary<string, T> values)
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
                _updateTimer?.Dispose();
                _disposed = true;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposed = true;
        }
    }

    private async void UpdateAllValues(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            foreach (var tagName in _tagNames)
            {
                var value = await _plc.Value<object>(tagName);
                _currentValues.AddOrUpdate(tagName, value, (_, _) => value);
            }
        }
        catch
        {
            // Ignore errors during background updates
        }
    }
}
