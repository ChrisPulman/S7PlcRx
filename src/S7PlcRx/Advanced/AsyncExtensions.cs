// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Advanced;
#else
namespace S7PlcRx.Advanced;
#endif

/// <summary>
/// Provides additional async-first helpers for reading, writing, and observing PLC values without changing the base
/// <see cref="IRxS7"/> API surface.
/// </summary>
/// <remarks>These helpers layer <see cref="ValueTask"/> and async-observable patterns over the existing PLC API.
/// Where possible, they complete synchronously from cached tag values or the existing multi-variable read/write paths
/// to reduce avoidable allocations.</remarks>
public static class AsyncExtensions
{
    /// <summary>Provides async-first helpers for PLC instances.</summary>
    /// <param name="plc">The PLC instance.</param>
    extension(IRxS7 plc)
    {
        /// <summary>Reads a PLC value using a <see cref="ValueTask{TResult}"/>.</summary>
        /// <typeparam name="T">The expected PLC value type.</typeparam>
        /// <param name="variable">The tag name to read.</param>
        /// <param name="cancellationToken">The cancellation token for the read operation.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to the current PLC value.</returns>
        public ValueTask<T?> ReadValueAsync<T>(string? variable, CancellationToken cancellationToken = default)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            var variableName = ValidateVariableName(variable);

            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask<T?>(Task.FromCanceled<T?>(cancellationToken));
            }

            if (TryGetCurrentValue(plc, variableName, out T? currentValue))
            {
                return new ValueTask<T?>(currentValue);
            }

            return cancellationToken.CanBeCanceled
                ? new ValueTask<T?>(plc.ValueAsync<T>(variableName, cancellationToken))
                : new ValueTask<T?>(plc.Value<T>(variableName));
        }

        /// <summary>Reads multiple PLC values using a <see cref="ValueTask{TResult}"/>.</summary>
        /// <typeparam name="T">The expected PLC value type.</typeparam>
        /// <param name="variables">The tag names to read.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to a dictionary of tag values keyed by tag name.</returns>
        public ValueTask<Dictionary<string, T?>> ReadValuesAsync<T>(params string[] variables)
            => plc.ReadValuesAsync<T>((IReadOnlyList<string>)variables, CancellationToken.None);

        /// <summary>Reads multiple PLC values using a <see cref="ValueTask{TResult}"/>, honoring cancellation for deferred reads.</summary>
        /// <typeparam name="T">The expected PLC value type.</typeparam>
        /// <param name="variables">The tag names to read.</param>
        /// <param name="cancellationToken">The cancellation token for deferred reads.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that resolves to a dictionary of tag values keyed by tag name.</returns>
        public ValueTask<Dictionary<string, T?>> ReadValuesAsync<T>(IReadOnlyList<string> variables, CancellationToken cancellationToken = default)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            if (variables is null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            if (variables.Count == 0)
            {
                return new ValueTask<Dictionary<string, T?>>(new Dictionary<string, T?>());
            }

            ValidateVariableNames(variables);

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

            var pendingReads = CreatePendingReads<T>(plc, variables, cancellationToken);

            return new ValueTask<Dictionary<string, T?>>(ReadValuesAsyncCore(variables, pendingReads));
        }

        /// <summary>Writes multiple PLC values using a <see cref="ValueTask"/>.</summary>
        /// <typeparam name="T">The PLC value type.</typeparam>
        /// <param name="values">The tag values to write.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed <see cref="ValueTask"/> when the writes have been issued.</returns>
        public ValueTask WriteValuesAsync<T>(IReadOnlyDictionary<string, T> values, CancellationToken cancellationToken = default)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            if (values is null)
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
        /// <summary>Exposes a PLC variable as an async observable sequence.</summary>
        /// <typeparam name="T">The expected PLC value type.</typeparam>
        /// <param name="variable">The tag name to observe.</param>
        /// <returns>An async observable that emits tag value updates asynchronously.</returns>
        public ReactiveUI.Primitives.Async.IObservableAsync<T?> ObserveValue<T>(string? variable)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            var variableName = ValidateVariableName(variable);

            return ToAsyncObservable(plc.Observe<T>(variableName));
        }

        /// <summary>Exposes a batch PLC observation as an async observable sequence.</summary>
        /// <typeparam name="T">The expected PLC value type.</typeparam>
        /// <param name="variables">The tag names to observe.</param>
        /// <returns>An async observable that emits dictionaries of tag values asynchronously.</returns>
        public ReactiveUI.Primitives.Async.IObservableAsync<Dictionary<string, T?>> ObserveValues<T>(params string[] variables)
        {
            if (plc is null)
            {
                throw new ArgumentNullException(nameof(plc));
            }

            if (variables is null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            if (variables.Length == 0)
            {
                throw new ArgumentException("At least one variable is required.", nameof(variables));
            }

            foreach (var variable in variables)
            {
                _ = ValidateVariableName(variable);
            }

            return ToAsyncObservable(plc.ObserveBatch<T>(variables));
        }
#endif
    }

#if NET8_0_OR_GREATER
    /// <summary>Stores the t oo bs er va bl ea sy n c value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="source">The s ou r c e value.</param>
    /// <returns>The resulting value.</returns>
    private static ReactiveUI.Primitives.Async.IObservableAsync<T> ToAsyncObservable<T>(IObservable<T> source) =>
        ReactiveUI.Primitives.Async.SignalAsync.Create<T>((observer, cancellationToken) =>
        {
            var subscription = source.Subscribe(
                value => QueueAsyncNotification(() => observer.OnNextAsync(value, cancellationToken)),
                error => QueueAsyncNotification(() => observer.OnErrorResumeAsync(error, cancellationToken)),
                () => QueueAsyncNotification(() => observer.OnCompletedAsync(ReactiveUI.Primitives.Result.Success)));

            return ValueTask.FromResult(ReactiveUI.Primitives.Async.DisposableAsyncExtensions.ToDisposableAsync(subscription));
        });

    /// <summary>Queues an asynchronous observer notification.</summary>
    /// <param name="notification">The notification to run.</param>
    private static void QueueAsyncNotification(Func<ValueTask> notification)
    {
        _ = Task.Run(async () => await notification().ConfigureAwait(false));
    }

#endif

    /// <summary>Stores the v al id at ev ar ia bl en a m e value.</summary>
    /// <param name="variable">The v ar ia b l e value.</param>
    /// <returns>The resulting value.</returns>
    private static string ValidateVariableName(string? variable)
    {
        if (HasNoText(variable))
        {
            throw new ArgumentNullException(nameof(variable));
        }

        return variable!;
    }

    /// <summary>Returns whether the value is null, empty, or contains only whitespace.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>true when no non-whitespace text is present; otherwise, false.</returns>
    private static bool HasNoText(string? value)
    {
        if (value is null)
        {
            return true;
        }

        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Validates all variable names.</summary>
    /// <param name="variables">The variable names to validate.</param>
    private static void ValidateVariableNames(IReadOnlyList<string> variables)
    {
        foreach (var variable in variables)
        {
            _ = ValidateVariableName(variable);
        }
    }

    /// <summary>Creates pending read tasks for a group of variables.</summary>
    /// <typeparam name="T">The expected PLC value type.</typeparam>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="variables">The variable names to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pending read tasks.</returns>
    private static Task<T?>[] CreatePendingReads<T>(IRxS7 plc, IReadOnlyList<string> variables, CancellationToken cancellationToken)
    {
        var pendingReads = new Task<T?>[variables.Count];
        for (var i = 0; i < variables.Count; i++)
        {
            pendingReads[i] = cancellationToken.CanBeCanceled
                ? plc.ValueAsync<T>(variables[i], cancellationToken)
                : plc.Value<T>(variables[i]);
        }

        return pendingReads;
    }

    /// <summary>Stores the r ea dv al ue sa sy nc co r e value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="variables">The v ar ia bl e s value.</param>
    /// <param name="pendingReads">The p en di ng re a d s value.</param>
    /// <returns>The resulting value.</returns>
    private static async Task<Dictionary<string, T?>> ReadValuesAsyncCore<T>(IReadOnlyList<string> variables, Task<T?>[] pendingReads)
    {
        var values = new Dictionary<string, T?>(variables.Count, StringComparer.InvariantCultureIgnoreCase);
        for (var i = 0; i < variables.Count; i++)
        {
            values[variables[i]] = await pendingReads[i].ConfigureAwait(false);
        }

        return values;
    }

    /// <summary>Stores the t ry ge tc ur re nt va l u e value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="plc">The p l c value.</param>
    /// <param name="variable">The v ar ia b l e value.</param>
    /// <param name="value">The v al u e value.</param>
    /// <returns>The resulting value.</returns>
    private static bool TryGetCurrentValue<T>(IRxS7 plc, string variable, out T? value)
    {
        var tag = plc.TagList[variable];
        if (tag is not null && (typeof(T) == typeof(object) || (tag.Type == typeof(T) && tag.Value?.GetType() == typeof(T))))
        {
            value = (T?)tag.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Stores the t ry ge tc ur re nt va lu e s value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="plc">The p l c value.</param>
    /// <param name="variables">The v ar ia bl e s value.</param>
    /// <param name="values">The v al u e s value.</param>
    /// <returns>The resulting value.</returns>
    private static bool TryGetCurrentValues<T>(IRxS7 plc, IReadOnlyList<string> variables, out Dictionary<string, T?> values)
    {
        values = new(variables.Count, StringComparer.InvariantCultureIgnoreCase);
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

    /// <summary>Stores the t ry re ad mu lt iv a r value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="rx">The r x value.</param>
    /// <param name="variables">The v ar ia bl e s value.</param>
    /// <param name="values">The v al u e s value.</param>
    /// <returns>The resulting value.</returns>
    private static bool TryReadMultiVar<T>(RxS7 rx, IReadOnlyList<string> variables, out Dictionary<string, T?> values)
    {
        var tags = new List<Tag>(variables.Count);
        foreach (var variable in variables)
        {
            var tag = rx.TagList[variable];
            if (tag is null)
            {
                values = null!;
                return false;
            }

            tags.Add(tag);
        }

        var multi = rx.ReadMultiVar(tags);
        if (multi is null)
        {
            values = null!;
            return false;
        }

        values = new(multi.Count, StringComparer.InvariantCultureIgnoreCase);
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

    /// <summary>Stores the t ry wr it em ul ti v a r value.</summary>
    /// <typeparam name="T">The t value type.</typeparam>
    /// <param name="rx">The r x value.</param>
    /// <param name="values">The v al u e s value.</param>
    /// <returns>The resulting value.</returns>
    private static bool TryWriteMultiVar<T>(RxS7 rx, IReadOnlyDictionary<string, T> values)
    {
        var tags = new List<Tag>(values.Count);
        foreach (var kvp in values)
        {
            var tag = rx.TagList[kvp.Key];
            if (tag is null)
            {
                return false;
            }

            tag.NewValue = kvp.Value;
            tags.Add(tag);
        }

        return rx.WriteMultiVar(tags);
    }
}
