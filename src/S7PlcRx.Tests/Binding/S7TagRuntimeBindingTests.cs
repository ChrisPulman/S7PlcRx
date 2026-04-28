// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using S7PlcRx.Binding;
using S7PlcRx.Enums;
using S7PlcRx.PlcTypes;

namespace S7PlcRx.Tests.Binding;

/// <summary>
/// Tests runtime grouped byte-array PLC binding operations.
/// </summary>
public sealed class S7TagRuntimeBindingTests
{
    /// <summary>
    /// Ensures multiple property writes in the same DB are coalesced into one byte-array write.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Write_WithSameDbTags_ShouldCoalesceToSingleByteArrayWrite()
    {
        var plc = new RecordingPlc();
        var definitions = new[]
        {
            new S7TagDefinition("Temperature", "DB1.DBD0", typeof(float), 0, S7TagDirection.WriteOnly),
            new S7TagDefinition("Pressure", "DB1.DBD4", typeof(float), 0, S7TagDirection.WriteOnly),
        };

        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (_, _) => { });
        binding.Write("Temperature", 12.5f);
        binding.Write("Pressure", 25.25f);

        await Task.Delay(150);

        Assert.Multiple(() =>
        {
            Assert.That(plc.Writes, Has.Count.EqualTo(1));
            Assert.That(plc.Writes[0].TagName, Is.EqualTo("__s7_binding_db1_0_8"));
            Assert.That(plc.Writes[0].Bytes, Has.Length.EqualTo(8));
        });
    }

    /// <summary>
    /// Ensures interval reads for same-DB tags are coalesced into one byte-array read.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ReadInterval_WithSameDbTags_ShouldCoalesceToSingleByteArrayRead()
    {
        var plc = new RecordingPlc();
        Real.ToSpan(1.25f, plc.ReadBuffer.AsSpan(0, 4));
        Real.ToSpan(2.5f, plc.ReadBuffer.AsSpan(4, 4));
        var applied = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
        var definitions = new[]
        {
            new S7TagDefinition("Temperature", "DB1.DBD0", typeof(float), 25, S7TagDirection.ReadOnly),
            new S7TagDefinition("Pressure", "DB1.DBD4", typeof(float), 25, S7TagDirection.ReadOnly),
        };

        using var binding = S7TagRuntimeBinding.Bind(plc, definitions, (name, value) => applied[name] = value);

        await Task.Delay(150);

        Assert.Multiple(() =>
        {
            Assert.That(plc.Reads, Does.Contain("__s7_binding_db1_0_8"));
            Assert.That(applied["Temperature"], Is.EqualTo(1.25f));
            Assert.That(applied["Pressure"], Is.EqualTo(2.5f));
        });
    }

    private sealed class RecordingPlc : IRxS7
    {
        public List<(string TagName, byte[] Bytes)> Writes { get; } = [];

        public List<string> Reads { get; } = [];

        public byte[] ReadBuffer { get; } = new byte[8];

        public string IP => "127.0.0.1";

        public IObservable<bool> IsConnected => Observable.Return(true);

        public bool IsConnectedValue => true;

        public IObservable<string> LastError => Observable.Empty<string>();

        public IObservable<ErrorCode> LastErrorCode => Observable.Empty<ErrorCode>();

        public IObservable<Tag?> ObserveAll => Observable.Empty<Tag?>();

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

        public bool IsDisposed { get; private set; }

        public IObservable<T?> Observe<T>(string? variable) => Observable.Empty<T?>();

        public Task<T?> Value<T>(string? variable)
        {
            if (typeof(T) == typeof(byte[]))
            {
                if (variable != null)
                {
                    Reads.Add(variable);
                }

                object bytes = ReadBuffer.ToArray();
                return Task.FromResult((T?)bytes);
            }

            return Task.FromResult(default(T));
        }

        public Task<T?> ValueAsync<T>(string? variable, CancellationToken cancellationToken) => Value<T>(variable);

        public void Value<T>(string? variable, T? value)
        {
            if (value is byte[] bytes && variable != null)
            {
                Writes.Add((variable, bytes));
            }
        }

        public IObservable<string[]> GetCpuInfo() => Observable.Empty<string[]>();

        public void Dispose() => IsDisposed = true;
    }
}
