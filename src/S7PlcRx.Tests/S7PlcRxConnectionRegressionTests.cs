// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias TUnitAssertions;

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using MockS7Plc;
using S7PlcRx.Enums;
using TUnitAssertions::TUnit.Assertions.Extensions;
using TUnitAssert = TUnitAssertions::TUnit.Assertions.Assert;

namespace S7PlcRx.Tests;

/// <summary>
/// Regression tests for connection readiness and watchdog behavior.
/// </summary>
[NonParallelizable]
public sealed class S7PlcRxConnectionRegressionTests
{
    private const string LivePlcIp = "172.16.13.1";

    /// <summary>
    /// Ensures connection readiness is based on the completed ISO/S7 setup handshake, not optional SZL diagnostics.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task IsConnected_WhenSetupHandshakeSucceedsWithoutSzlReadiness_ShouldBecomeTrue()
    {
        using var server = new HandshakeOnlyS7Server();
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, interval: 50);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
        await TUnitAssert.That(server.HandshakeCount).IsGreaterThanOrEqualTo(1);
        await TUnitAssert.That(server.UnsupportedRequestCount).IsEqualTo(0);
    }

    /// <summary>
    /// Ensures handshake frames can arrive fragmented across multiple TCP reads.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task IsConnected_WhenHandshakeResponsesAreFragmented_ShouldBecomeTrue()
    {
        using var server = new HandshakeOnlyS7Server(fragmentHandshakeResponses: true);
        using var plc = S71500.Create(MockServer.Localhost, 0, 1, null, interval: 50);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(5))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
        await TUnitAssert.That(server.HandshakeCount).IsGreaterThanOrEqualTo(1);
        await TUnitAssert.That(server.UnsupportedRequestCount).IsEqualTo(0);
    }

    /// <summary>
    /// Ensures a live S7-1500 can reach connected state without any program-specific DB reads.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Explicit]
    [Category("LivePLC")]
    public async Task IsConnected_ToLiveS71500_ShouldBecomeTrue()
    {
        using var plc = S71500.Create(LivePlcIp, interval: 50);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(15))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();
    }

    /// <summary>
    /// Ensures live CPU diagnostics complete without any program-specific DB reads.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    [Explicit]
    [Category("LivePLC")]
    public async Task GetCpuInfo_ToLiveS71500_ShouldCompleteAndReturnIdentityFields()
    {
        using var plc = S71500.Create(LivePlcIp, interval: 50);

        var cpuInfo = await plc.GetCpuInfo()
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(15))
            .FirstAsync();

        await TUnitAssert.That(cpuInfo).IsNotNull();
        await TUnitAssert.That(cpuInfo.Length).IsGreaterThanOrEqualTo(9);
        await TUnitAssert.That(cpuInfo.Any(static value => !string.IsNullOrWhiteSpace(value))).IsTrue();
        await TUnitAssert.That(cpuInfo[5]).IsNotNull();
        await TUnitAssert.That(cpuInfo[5].Trim()).IsNotEmpty();
    }

    /// <summary>
    /// Ensures watchdog writes start once a normal MockS7Plc connection is established.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task Watchdog_WhenConnectedToMockPlc_ShouldWriteConfiguredWord()
    {
        const ushort watchdogValue = 1234;

        using var server = new MockServer();
        server.DefaultDb1Size = 16;
        await TUnitAssert.That(server.Start()).IsEqualTo(0);

        using var plc = new RxS7(CpuType.S71500, MockServer.Localhost, 0, 1, "DB1.DBW0", interval: 50, watchdogValue, watchDogInterval: 1);

        var connected = await plc.IsConnected
            .Where(static x => x)
            .Take(1)
            .Timeout(TimeSpan.FromSeconds(10))
            .FirstAsync();

        await TUnitAssert.That(connected).IsTrue();

        var watchdogWritten = await WaitUntilAsync(
            () => BinaryPrimitives.ReadUInt16BigEndian(server.DefaultDb1!.AsSpan(0, 2)) == watchdogValue,
            TimeSpan.FromSeconds(5));

        await TUnitAssert.That(watchdogWritten).IsTrue();
        await TUnitAssert.That(BinaryPrimitives.ReadUInt16BigEndian(server.DefaultDb1!.AsSpan(0, 2))).IsEqualTo(watchdogValue);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        return predicate();
    }

    private sealed class HandshakeOnlyS7Server : IDisposable
    {
        private static readonly byte[] ConnectionConfirm =
        [
            0x03, 0x00, 0x00, 0x16, 0x11, 0xD0, 0x00, 0x01, 0x00, 0x2E, 0x00,
            0xC0, 0x01, 0x09, 0xC1, 0x02, 0x03, 0x01, 0xC2, 0x02, 0x01, 0x00
        ];

        private static readonly byte[] SetupConfirm =
        [
            0x03, 0x00, 0x00, 0x1B, 0x02, 0xF0, 0x80, 0x32, 0x03, 0x00, 0x00,
            0x04, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xF0, 0x00, 0x00, 0x03, 0xC0
        ];

        private static readonly byte[] UnsupportedDiagnosticResponse =
        [
            0x03, 0x00, 0x00, 0x10, 0x02, 0xF0, 0x80, 0x32,
            0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        ];

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TcpListener _listener;
        private readonly Task _acceptLoop;
        private readonly bool _fragmentHandshakeResponses;
        private bool _disposed;
        private int _handshakeCount;
        private int _unsupportedRequestCount;

        public HandshakeOnlyS7Server(bool fragmentHandshakeResponses = false)
        {
            _fragmentHandshakeResponses = fragmentHandshakeResponses;
            _listener = new TcpListener(IPAddress.Loopback, 102);
            _listener.Start();
            _acceptLoop = AcceptLoopAsync();
        }

        public int HandshakeCount => Volatile.Read(ref _handshakeCount);

        public int UnsupportedRequestCount => Volatile.Read(ref _unsupportedRequestCount);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _listener.Stop();
            ((IDisposable)_listener).Dispose();

            try
            {
                _acceptLoop.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
            }

            _cancellationTokenSource.Dispose();
        }

        private static async Task<int> ReadTpktAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            if (!await ReadExactAsync(stream, buffer, 0, 4, cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            var length = (buffer[2] << 8) | buffer[3];
            if (length < 4 || length > buffer.Length)
            {
                return 0;
            }

            return await ReadExactAsync(stream, buffer, 4, length - 4, cancellationToken).ConfigureAwait(false)
                ? length
                : 0;
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var total = 0;
            while (total < count)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                var read = await stream.ReadAsync(buffer.AsMemory(offset + total, count - total), cancellationToken).ConfigureAwait(false);
#else
                var read = await stream.ReadAsync(buffer, offset + total, count - total, cancellationToken).ConfigureAwait(false);
#endif
                if (read <= 0)
                {
                    return false;
                }

                total += read;
            }

            return true;
        }

        private static async Task WriteFrameAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            await stream.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
#else
            await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
#endif
        }

        private async Task WriteHandshakeFrameAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            if (!_fragmentHandshakeResponses)
            {
                await WriteFrameAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
                return;
            }

            for (var i = 0; i < buffer.Length; i++)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                await stream.WriteAsync(buffer.AsMemory(i, 1), cancellationToken).ConfigureAwait(false);
#else
                await stream.WriteAsync(buffer, i, 1, cancellationToken).ConfigureAwait(false);
#endif
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(2, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                _ = HandleClientAsync(client, _cancellationTokenSource.Token);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                var buffer = new byte[256];

                if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                {
                    return;
                }

                await WriteHandshakeFrameAsync(stream, ConnectionConfirm, cancellationToken).ConfigureAwait(false);

                if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                {
                    return;
                }

                await WriteHandshakeFrameAsync(stream, SetupConfirm, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _handshakeCount);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (await ReadTpktAsync(stream, buffer, cancellationToken).ConfigureAwait(false) <= 0)
                    {
                        return;
                    }

                    Interlocked.Increment(ref _unsupportedRequestCount);
                    await WriteFrameAsync(stream, UnsupportedDiagnosticResponse, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
