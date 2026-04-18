// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ReactiveUI.Extensions;
#if NET8_0_OR_GREATER
using ReactiveUI.Extensions.Async;
#endif

namespace S7PlcRx.Advanced;

/// <summary>
/// Provides additional async-first helpers for reading, writing, and observing PLC values without changing the base
/// <see cref="IRxS7"/> API surface.
/// </summary>
/// <remarks>These helpers layer <see cref="ValueTask"/> and async-observable patterns over the existing PLC API.
/// Where possible, they complete synchronously from cached tag values or the existing multi-variable read/write paths
/// to reduce avoidable allocations.</remarks>
public static class AsyncExtensions
{
    /// <summary>
    /// Reads a PLC value using a <see cref="ValueTask{TResult}"/>, returning synchronously when a compatible cached tag value is already available.
    /// </summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variable">The tag name to read.</param>
    /// <param name="cancellationToken">The cancellation token for the read operation.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to the current PLC value.</returns>
    public static ValueTask<T?> ReadValueAsync<T>(this IRxS7 plc, string? variable, CancellationToken cancellationToken = default)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            throw new ArgumentNullException(nameof(variable));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<T?>(Task.FromCanceled<T?>(cancellationToken));
        }

        if (TryGetCurrentValue(plc, variable, out T? currentValue))
        {
            return new ValueTask<T?>(currentValue);
        }

        return cancellationToken.CanBeCanceled
            ? new ValueTask<T?>(plc.ValueAsync<T>(variable, cancellationToken))
            : new ValueTask<T?>(plc.Value<T>(variable));
    }

    /// <summary>
    /// Reads multiple PLC values using a <see cref="ValueTask{TResult}"/>, preferring cached values and the optimized multi-variable read path where available.
    /// </summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The tag names to read.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to a dictionary of tag values keyed by tag name.</returns>
    public static ValueTask<Dictionary<string, T?>> ReadValuesAsync<T>(this IRxS7 plc, params string[] variables)
        => ReadValuesAsync<T>(plc, (IReadOnlyList<string>)variables, CancellationToken.None);

    /// <summary>
    /// Reads multiple PLC values using a <see cref="ValueTask{TResult}"/>, honoring cancellation for deferred reads.
    /// </summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The tag names to read.</param>
    /// <param name="cancellationToken">The cancellation token for deferred reads.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to a dictionary of tag values keyed by tag name.</returns>
    public static ValueTask<Dictionary<string, T?>> ReadValuesAsync<T>(this IRxS7 plc, IReadOnlyList<string> variables, CancellationToken cancellationToken = default)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (variables == null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        if (variables.Count == 0)
        {
            return new ValueTask<Dictionary<string, T?>>(new Dictionary<string, T?>());
        }

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentNullException(nameof(variables));
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<Dictionary<string, T?>>(Task.FromCanceled<Dictionary<string, T?>>(cancellationToken));
        }

        if (TryGetCurrentValues<T>(plc, variables, out var cachedValues))
        {
            return new ValueTask<Dictionary<string, T?>>(cachedValues);
        }

        if (plc is RxS7 rx && TryReadMultiVar<T>(rx, variables, out var multiValues))
        {
            return new ValueTask<Dictionary<string, T?>>(multiValues);
        }

        var pendingReads = new Task<T?>[variables.Count];
        for (var i = 0; i < variables.Count; i++)
        {
            pendingReads[i] = cancellationToken.CanBeCanceled
                ? plc.ValueAsync<T>(variables[i], cancellationToken)
                : plc.Value<T>(variables[i]);
        }

        if (pendingReads.All(static read => read.Status == TaskStatus.RanToCompletion))
        {
            var values = new Dictionary<string, T?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
            for (var i = 0; i < variables.Count; i++)
            {
                values[variables[i]] = pendingReads[i].Result;
            }

            return new ValueTask<Dictionary<string, T?>>(values);
        }

        return new ValueTask<Dictionary<string, T?>>(ReadValuesAsyncCore(variables, pendingReads));
    }

    /// <summary>
    /// Writes multiple PLC values using a <see cref="ValueTask"/>, preferring the optimized multi-variable write path where available.
    /// </summary>
    /// <typeparam name="T">The PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="values">The tag values to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed <see cref="ValueTask"/> when the writes have been issued.</returns>
    public static ValueTask WriteValuesAsync<T>(this IRxS7 plc, IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (plc is RxS7 rx && TryWriteMultiVar(rx, values))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var kvp in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            plc.Value(kvp.Key, kvp.Value);
        }

        return ValueTask.CompletedTask;
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Exposes a PLC variable as an async observable sequence.
    /// </summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variable">The tag name to observe.</param>
    /// <returns>An <see cref="IObservableAsync{T}"/> that emits tag value updates asynchronously.</returns>
    public static IObservableAsync<T?> ObserveValueAsync<T>(this IRxS7 plc, string? variable)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            throw new ArgumentNullException(nameof(variable));
        }

        return plc.Observe<T>(variable).ToObservableAsync();
    }

    /// <summary>
    /// Exposes a batch PLC observation as an async observable sequence.
    /// </summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The tag names to observe.</param>
    /// <returns>An <see cref="IObservableAsync{T}"/> that emits dictionaries of tag values asynchronously.</returns>
    public static IObservableAsync<Dictionary<string, T?>> ObserveValuesAsync<T>(this IRxS7 plc, params string[] variables)
    {
        if (plc == null)
        {
            throw new ArgumentNullException(nameof(plc));
        }

        if (variables == null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        if (variables.Length == 0)
        {
            throw new ArgumentException("At least one variable is required.", nameof(variables));
        }

        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentNullException(nameof(variables));
            }
        }

        return plc.ObserveBatch<T>(variables).ToObservableAsync();
    }
#endif

    private static async Task<Dictionary<string, T?>> ReadValuesAsyncCore<T>(IReadOnlyList<string> variables, Task<T?>[] pendingReads)
    {
        var values = new Dictionary<string, T?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < variables.Count; i++)
        {
            values[variables[i]] = await pendingReads[i].ConfigureAwait(false);
        }

        return values;
    }

    private static bool TryGetCurrentValue<T>(IRxS7 plc, string variable, out T? value)
    {
        var tag = plc.TagList[variable];
        if (tag != null && (typeof(T) == typeof(object) || (tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T))))
        {
            value = (T?)tag.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetCurrentValues<T>(IRxS7 plc, IReadOnlyList<string> variables, out Dictionary<string, T?> values)
    {
        values = new Dictionary<string, T?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
        foreach (var variable in variables)
        {
            if (!TryGetCurrentValue(plc, variable, out T? value))
            {
                values = null!;
                return false;
            }

            values[variable] = value;
        }

        return true;
    }

    private static bool TryReadMultiVar<T>(RxS7 rx, IReadOnlyList<string> variables, out Dictionary<string, T?> values)
    {
        var tags = new List<Tag>(variables.Count);
        foreach (var variable in variables)
        {
            var tag = rx.TagList[variable];
            if (tag == null)
            {
                values = null!;
                return false;
            }

            tags.Add(tag);
        }

        var multi = rx.ReadMultiVar(tags);
        if (multi == null)
        {
            values = null!;
            return false;
        }

        values = new Dictionary<string, T?>(multi.Count, StringComparer.InvariantCultureIgnoreCase);
        foreach (var kvp in multi)
        {
            if (kvp.Value is null)
            {
                values[kvp.Key] = default;
                continue;
            }

            if (kvp.Value is T typed)
            {
                values[kvp.Key] = typed;
                continue;
            }

            values = null!;
            return false;
        }

        return true;
    }

    private static bool TryWriteMultiVar<T>(RxS7 rx, IReadOnlyDictionary<string, T> values)
    {
        var tags = new List<Tag>(values.Count);
        foreach (var kvp in values)
        {
            var tag = rx.TagList[kvp.Key];
            if (tag == null)
            {
                return false;
            }

            tag.NewValue = kvp.Value;
            tags.Add(tag);
        }

        return rx.WriteMultiVar(tags);
    }
}
