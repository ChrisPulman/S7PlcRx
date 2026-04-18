// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using MockS7Plc;
using ReactiveUI.Extensions.Async;
using S7PlcRx.Advanced;
using S7PlcRx.Enums;

namespace S7PlcRx.Tests;

/// <summary>
/// Tests the async extension surface added on top of <see cref="IRxS7"/>.
/// </summary>
public class S7PlcRxAsyncExtensionsTests
{
    /// <summary>
    /// Verifies cached reads complete synchronously.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCachedValueExists_CompletesSynchronously()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("Cached", "DB1.DBW0", typeof(ushort)) { Value = (ushort)42 });

        var valueTask = plc.ReadValueAsync<ushort>("Cached");

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(await valueTask.AsTask(), Is.EqualTo((ushort)42));
        Assert.That(plc.SyncReadCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies canceled reads surface an operation canceled exception.
    /// </summary>
    [Test]
    public void ReadValueAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        var plc = new TestPlc();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await plc.ReadValueAsync<ushort>("Canceled", cts.Token).AsTask(),
            Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>
    /// Verifies uncached reads use the underlying task-based read path.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCacheMissing_UsesTaskReadPath()
    {
        var plc = new TestPlc();
        plc.SetSyncValue("Live", (ushort)7);

        var value = await plc.ReadValueAsync<ushort>("Live").AsTask();

        Assert.That(value, Is.EqualTo((ushort)7));
        Assert.That(plc.SyncReadCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies empty batch reads return an empty dictionary.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenVariablesEmpty_ReturnsEmptyDictionary()
    {
        var plc = new TestPlc();

        var values = await plc.ReadValuesAsync<ushort>(Array.Empty<string>()).AsTask();

        Assert.That(values, Is.Empty);
    }

    /// <summary>
    /// Verifies cached batch reads complete synchronously.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenCachedValuesExist_CompletesSynchronously()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", "DB1.DBW0", typeof(ushort)) { Value = (ushort)1 });
        plc.TagList.Add(new Tag("B", "DB1.DBW2", typeof(ushort)) { Value = (ushort)2 });

        var valueTask = plc.ReadValuesAsync<ushort>("A", "B");
        var values = await valueTask.AsTask();

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(values["A"], Is.EqualTo((ushort)1));
        Assert.That(values["B"], Is.EqualTo((ushort)2));
        Assert.That(plc.SyncReadCount, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies cancellable batch reads use the async read path.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenCancellationTokenCanBeUsed_UsesAsyncReadPath()
    {
        var plc = new TestPlc();
        plc.SetAsyncValue("A", (ushort)11);
        plc.SetAsyncValue("B", (ushort)22);
        using var cts = new CancellationTokenSource();

        var values = await plc.ReadValuesAsync<ushort>(new[] { "A", "B" }, cts.Token).AsTask();

        Assert.That(values["A"], Is.EqualTo((ushort)11));
        Assert.That(values["B"], Is.EqualTo((ushort)22));
        Assert.That(plc.AsyncReadCount, Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies deferred async batch reads are awaited to completion.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenAsyncReadsAreDeferred_AwaitsCompletion()
    {
        var plc = new TestPlc();
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        plc.SetAsyncFactory(
            "A",
            async cancellationToken =>
            {
                var completedTask = await Task.WhenAny(completion.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                if (completedTask != completion.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                return await completion.Task;
            });

        using var cts = new CancellationTokenSource();
        var valueTask = plc.ReadValuesAsync<ushort>(new[] { "A" }, cts.Token);

        Assert.That(valueTask.IsCompleted, Is.False);

        completion.SetResult((ushort)5);
        var values = await valueTask.AsTask();

        Assert.That(values["A"], Is.EqualTo((ushort)5));
    }

    /// <summary>
    /// Verifies the optimized RxS7 multi-variable read path is used when available.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValuesAsync_WhenRxMultiVarCanBeUsed_ReturnsExpectedValues()
    {
        using var server = new MockServer();
        server.DefaultDb1Size = 16;
        Assert.That(server.Start(), Is.EqualTo(0));

        BinaryPrimitives.WriteUInt16BigEndian(server.DefaultDb1!.AsSpan(0, 2), 100);
        BinaryPrimitives.WriteUInt16BigEndian(server.DefaultDb1.AsSpan(2, 2), 200);

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 100);
        plc.AddUpdateTagItem<ushort>("A", "DB1.DBW0").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("B", "DB1.DBW2").SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x).Timeout(TimeSpan.FromSeconds(10));
        var values = await plc.ReadValuesAsync<ushort>("A", "B").AsTask();

        Assert.That(values["A"], Is.EqualTo((ushort)100));
        Assert.That(values["B"], Is.EqualTo((ushort)200));
    }

    /// <summary>
    /// Verifies cached values are bypassed when the runtime value type no longer matches the tag type.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ReadValueAsync_WhenCachedRuntimeTypeMismatches_FallsBackToRead()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", "DB1.DBW0", typeof(ushort)) { Value = 99 });
        plc.SetSyncValue("A", (ushort)77);

        var value = await plc.ReadValueAsync<ushort>("A").AsTask();

        Assert.That(value, Is.EqualTo((ushort)77));
        Assert.That(plc.SyncReadCount, Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies blank variable names are rejected.
    /// </summary>
    [Test]
    public void ReadValuesAsync_WhenVariableNameIsBlank_ThrowsArgumentNullException()
    {
        var plc = new TestPlc();

        Assert.ThrowsAsync<ArgumentNullException>(async () => await plc.ReadValuesAsync<ushort>(new[] { string.Empty }, CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Verifies fallback batch writes update each requested tag.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task WriteValuesAsync_WhenCalled_WritesExpectedValues()
    {
        var plc = new TestPlc();
        var values = new Dictionary<string, ushort>
        {
            ["A"] = 10,
            ["B"] = 20,
        };

        var valueTask = plc.WriteValuesAsync(values);
        await valueTask.AsTask();

        Assert.That(valueTask.IsCompleted, Is.True);
        Assert.That(plc.WrittenValues["A"], Is.EqualTo((ushort)10));
        Assert.That(plc.WrittenValues["B"], Is.EqualTo((ushort)20));
    }

    /// <summary>
    /// Verifies canceled writes stop before dispatch.
    /// </summary>
    [Test]
    public void WriteValuesAsync_WhenCanceled_ThrowsOperationCanceledException()
    {
        var plc = new TestPlc();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await plc.WriteValuesAsync(new Dictionary<string, ushort> { ["A"] = 10 }, cts.Token).AsTask());
    }

    /// <summary>
    /// Verifies the optimized RxS7 multi-variable write path updates DB values.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task WriteValuesAsync_WhenRxMultiVarCanBeUsed_WritesExpectedValues()
    {
        using var server = new MockServer();
        server.DefaultDb1Size = 16;
        Assert.That(server.Start(), Is.EqualTo(0));

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, null, interval: 100);
        plc.AddUpdateTagItem<ushort>("A", "DB1.DBW0").SetTagPollIng(false);
        plc.AddUpdateTagItem<ushort>("B", "DB1.DBW2").SetTagPollIng(false);

        await plc.IsConnected.FirstAsync(x => x).Timeout(TimeSpan.FromSeconds(10));
        await plc.WriteValuesAsync(new Dictionary<string, ushort> { ["A"] = 321, ["B"] = 654 }).AsTask();
        await Task.Delay(100);

        Assert.That(BinaryPrimitives.ReadUInt16BigEndian(server.DefaultDb1!.AsSpan(0, 2)), Is.EqualTo((ushort)321));
        Assert.That(BinaryPrimitives.ReadUInt16BigEndian(server.DefaultDb1.AsSpan(2, 2)), Is.EqualTo((ushort)654));
    }

    /// <summary>
    /// Verifies the existing batch read helper routes through the new async read extensions.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ValueBatch_WhenCalled_UsesAsyncReadExtensions()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", "DB1.DBW0", typeof(ushort)) { Value = (ushort)9 });

        var values = await plc.ValueBatch<ushort>("A");

        Assert.That(values["A"], Is.EqualTo((ushort)9));
    }

    /// <summary>
    /// Verifies the existing batch write helper routes through the new async write extensions.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ValueBatchWrite_WhenCalled_UsesAsyncWriteExtensions()
    {
        var plc = new TestPlc();

        await plc.ValueBatch(new Dictionary<string, ushort> { ["A"] = 12 });

        Assert.That(plc.WrittenValues["A"], Is.EqualTo((ushort)12));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Verifies async observable value projections emit tag updates.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ObserveValueAsync_WhenTagChanges_EmitsUpdatedValue()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", "DB1.DBW0", typeof(ushort)));

        var completion = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = await plc.ObserveValueAsync<ushort>("A")
            .SubscribeAsync(
                async (value, cancellationToken) =>
                {
                    if (value == 123)
                    {
                        completion.TrySetResult(value);
                    }

                    await Task.CompletedTask;
                },
                CancellationToken.None);

        plc.PublishObservedValue("A", (ushort)123, typeof(ushort));

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(result, Is.EqualTo((ushort)123));
    }

    /// <summary>
    /// Verifies async observable single-value wrappers reject blank variable names.
    /// </summary>
    [Test]
    public void ObserveValueAsync_WhenVariableBlank_ThrowsArgumentNullException()
    {
        var plc = new TestPlc();

        Assert.Throws<ArgumentNullException>(() => plc.ObserveValueAsync<ushort>(string.Empty));
    }

    /// <summary>
    /// Verifies async observable batch projections emit updated dictionaries.
    /// </summary>
    /// <returns>A task representing the test operation.</returns>
    [Test]
    public async Task ObserveValuesAsync_WhenTagsChange_EmitsUpdatedDictionary()
    {
        var plc = new TestPlc();
        plc.TagList.Add(new Tag("A", "DB1.DBW0", typeof(ushort)));

        var completion = new TaskCompletionSource<Dictionary<string, ushort>>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = await plc.ObserveValuesAsync<ushort>("A")
            .SubscribeAsync(
                async (values, cancellationToken) =>
                {
                    if (values.TryGetValue("A", out var a) && a == 10)
                    {
                        completion.TrySetResult(values);
                    }

                    await Task.CompletedTask;
                },
                CancellationToken.None);

        plc.PublishObservedValue("A", (ushort)10, typeof(ushort));

        var values = await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.That(values["A"], Is.EqualTo((ushort)10));
    }

    /// <summary>
    /// Verifies async observable batch wrappers reject empty variable lists.
    /// </summary>
    [Test]
    public void ObserveValuesAsync_WhenVariablesEmpty_ThrowsArgumentException()
    {
        var plc = new TestPlc();

        Assert.Throws<ArgumentException>(() => plc.ObserveValuesAsync<ushort>());
    }

    /// <summary>
    /// Verifies async observable batch wrappers reject null variable arrays.
    /// </summary>
    [Test]
    public void ObserveValuesAsync_WhenVariablesNull_ThrowsArgumentNullException()
    {
        var plc = new TestPlc();

        Assert.Throws<ArgumentNullException>(() => plc.ObserveValuesAsync<ushort>(null!));
    }
#endif

    private sealed class TestPlc : IRxS7
    {
        private readonly Dictionary<string, object?> _asyncValues = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Func<CancellationToken, Task<object?>>> _asyncValueFactories = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly Subject<Tag?> _observeAllSubject = new();
        private readonly Dictionary<string, object?> _syncValues = new(StringComparer.InvariantCultureIgnoreCase);

        public string IP => MockServer.Localhost;

        public IObservable<bool> IsConnected => Observable.Return(true);

        public bool IsConnectedValue => true;

        public IObservable<string> LastError => Observable.Empty<string>();

        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        public IObservable<Tag?> ObserveAll => _observeAllSubject.AsObservable();

        public CpuType PLCType => CpuType.S71500;

        public short Rack => 0;

        public short Slot => 1;

        public IObservable<bool> IsPaused => Observable.Return(false);

        public IObservable<string> Status => Observable.Empty<string>();

        public Tags TagList { get; } = [];

        public bool ShowWatchDogWriting { get; set; }

        public string? WatchDogAddress => null;

        public ushort WatchDogValueToWrite { get; set; }

        public int WatchDogWritingTime => 0;

        public IObservable<long> ReadTime => Observable.Empty<long>();

        public int AsyncReadCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public int SyncReadCount { get; private set; }

        public Dictionary<string, object?> WrittenValues { get; } = new(StringComparer.InvariantCultureIgnoreCase);

        public void Dispose()
        {
            IsDisposed = true;
            _observeAllSubject.Dispose();
        }

        public IObservable<T?> Observe<T>(string? variable) => _observeAllSubject
            .Where(tag => string.Equals(tag?.Name, variable, StringComparison.InvariantCultureIgnoreCase))
            .Where(tag => tag?.Value is T)
            .Select(tag => (T?)tag!.Value);

        public Task<T?> Value<T>(string? variable)
        {
            SyncReadCount++;
            return Task.FromResult(ReadValue<T>(variable, _syncValues));
        }

        public Task<T?> ValueAsync<T>(string? variable, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncReadCount++;
            if (_asyncValueFactories.TryGetValue(variable!, out var factory))
            {
                return ReadFromFactoryAsync<T>(factory, cancellationToken);
            }

            return Task.FromResult(ReadValue<T>(variable, _asyncValues));
        }

        public void Value<T>(string? variable, T? value)
        {
            WrittenValues[variable!] = value;
        }

        public IObservable<string[]> GetCpuInfo() => Observable.Return(Array.Empty<string>());

        public void SetAsyncFactory(string variable, Func<CancellationToken, Task<object?>> factory)
            => _asyncValueFactories[variable] = factory;

        public void SetAsyncValue(string variable, object value)
            => _asyncValues[variable] = value;

        public void SetSyncValue(string variable, object value)
            => _syncValues[variable] = value;

        public void PublishObservedValue(string variable, object? value, Type type)
        {
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentNullException(nameof(variable));
            }

            var tag = TagList[variable] ?? new Tag(variable, variable, type);
            tag.Value = value;

            if (TagList[variable] == null)
            {
                TagList.Add(tag);
            }

            _observeAllSubject.OnNext(tag);
        }

        private static async Task<T?> ReadFromFactoryAsync<T>(Func<CancellationToken, Task<object?>> factory, CancellationToken cancellationToken)
        {
            var value = await factory(cancellationToken);
            return value is T typed ? typed : default;
        }

        private static T? ReadValue<T>(string? variable, IReadOnlyDictionary<string, object?> values)
        {
            if (string.IsNullOrWhiteSpace(variable))
            {
                throw new ArgumentNullException(nameof(variable));
            }

            return values.TryGetValue(variable, out var value) && value is T typed ? typed : default;
        }
    }
}
