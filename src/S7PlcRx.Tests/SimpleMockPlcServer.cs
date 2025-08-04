// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace S7PlcRx.Tests;

/// <summary>
/// Simple mock S7 PLC server for testing purposes.
/// Provides basic TCP server functionality to simulate PLC responses.
/// </summary>
public class SimpleMockPlcServer : IDisposable
{
    private readonly ConcurrentDictionary<int, byte[]> _dataBlocks = new();
    private readonly int _port;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleMockPlcServer"/> class.
    /// </summary>
    /// <param name="port">The port to listen on.</param>
    public SimpleMockPlcServer(int port = 10102)
    {
        _port = port;
        InitializeDataBlocks();
    }

    /// <summary>
    /// Gets a value indicating whether the server is running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts the mock PLC server.
    /// </summary>
    /// <returns>A task representing the start operation.</returns>
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _tcpListener = new TcpListener(IPAddress.Any, _port);

        try
        {
            _tcpListener.Start();
            IsRunning = true;

            // Start accepting connections in background
            _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));

            // Give server time to start
            await Task.Delay(100);
        }
        catch (Exception)
        {
            IsRunning = false;
            throw;
        }
    }

    /// <summary>
    /// Stops the mock PLC server.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _cancellationTokenSource?.Cancel();
            _tcpListener?.Stop();
            IsRunning = false;
        }
        catch
        {
            // Ignore stop errors
        }
    }

    /// <summary>
    /// Sets data in a data block for testing.
    /// </summary>
    /// <param name="dbNumber">Data block number.</param>
    /// <param name="offset">Offset in the data block.</param>
    /// <param name="data">Data to set.</param>
    public void SetTestData(int dbNumber, int offset, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var db = _dataBlocks.GetOrAdd(dbNumber, _ => new byte[1024]);
        if (offset + data.Length <= db.Length)
        {
            Array.Copy(data, 0, db, offset, data.Length);
        }
    }

    /// <summary>
    /// Disposes the server resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the server resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _tcpListener?.Dispose();
            _disposed = true;
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[1024];

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Send a basic response
                    var response = CreateBasicResponse(buffer, bytesRead);
                    if (response.Length > 0)
                    {
                        await stream.WriteAsync(response.AsMemory(0, response.Length), cancellationToken);
                    }
                }
            }
        }
        catch
        {
            // Ignore client handling errors for testing
        }
    }

    private static byte[] CreateBasicResponse(byte[] request, int length)
    {
        // Very basic S7 protocol response simulation
        if (length < 4)
        {
            return [];
        }

        // Check for S7 protocol header
        if (request[0] == 0x03 && request[1] == 0x00)
        {
            // Connection establishment
            if (length >= 20 && request[4] == 0x11)
            {
                return
                [
                    0x03, 0x00, 0x00, 0x16,
                    0x11, 0xE0, 0x00, 0x00, 0x00, 0x01, 0x00, 0xC1, 0x02, 0x01, 0x00,
                    0xC2, 0x02, 0x01, 0x02, 0xC0, 0x01, 0x09
                ];
            }

            // Setup communication
            if (length >= 25 && request[4] == 0x02)
            {
                return
                [
                    0x03, 0x00, 0x00, 0x1B,
                    0x02, 0xF0, 0x80, 0x32, 0x03, 0x00, 0x00, 0x01, 0x00, 0x00,
                    0x08, 0x00, 0x00, 0xF0, 0x00, 0x00, 0x01, 0x00, 0x01, 0x01, 0xE0
                ];
            }

            // Data communication (simplified)
            if (length > 7 && request[4] == 0x02 && request[5] == 0xF0)
            {
                var response = new byte[50];
                response[0] = 0x03;
                response[1] = 0x00;
                response[2] = 0x00;
                response[3] = 0x32; // Length
                response[4] = 0x02;
                response[5] = 0xF0;
                response[6] = 0x80;
                response[7] = 0x32;

                // Mock successful response
                response[18] = 0xFF; // Success code
                response[21] = 0xFF; // Success code

                // Add some test data
                for (var i = 25; i < 45; i++)
                {
                    response[i] = (byte)((i - 25) * 10);
                }

                return response;
            }
        }

        return [];
    }

    private void InitializeDataBlocks()
    {
        // Initialize some test data
        var testData = new byte[1024];
        for (var i = 0; i < testData.Length; i++)
        {
            testData[i] = (byte)(i % 256);
        }

        _dataBlocks[1] = testData;
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var tcpClient = await _tcpListener.AcceptTcpClientAsync(CancellationToken.None);
                _ = Task.Run(() => SimpleMockPlcServer.HandleClientAsync(tcpClient, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Expected when server is stopped
                break;
            }
            catch
            {
                // Log errors in production, ignore for testing
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken); // Brief delay before retry
                }
            }
        }
    }
}
