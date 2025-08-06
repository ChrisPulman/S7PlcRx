// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using S7PlcRx.Enterprise;

namespace S7PlcRx.Core;

/// <summary>
/// Production connection pool for high-throughput scenarios.
/// </summary>
public class ConnectionPool : IDisposable
{
    private readonly List<IRxS7> _connections = [];
    private readonly ConnectionPoolConfig _config;
    private readonly object _lock = new();
    private int _currentIndex;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionPool"/> class.
    /// </summary>
    /// <param name="config">The pool configuration.</param>
    public ConnectionPool(ConnectionPoolConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionPool"/> class.
    /// </summary>
    /// <param name="connectionConfigs">The connection configurations.</param>
    /// <param name="poolConfig">The pool configuration.</param>
    public ConnectionPool(
        IEnumerable<PlcConnectionConfig> connectionConfigs,
        ConnectionPoolConfig poolConfig)
    {
        _config = poolConfig ?? throw new ArgumentNullException(nameof(poolConfig));

        foreach (var config in connectionConfigs.Take(poolConfig.MaxConnections))
        {
            var plc = new RxS7(config.PLCType, config.IPAddress, config.Rack, config.Slot);
            _connections.Add(plc);
        }
    }

    /// <summary>
    /// Gets the maximum number of connections in the pool.
    /// </summary>
    public int MaxConnections => _config.MaxConnections;

    /// <summary>
    /// Gets the number of active connections.
    /// </summary>
    public int ActiveConnections => _connections.Count(c => c.IsConnectedValue);

    /// <summary>
    /// Gets a connection from the pool using load balancing.
    /// </summary>
    /// <returns>An available PLC connection.</returns>
    public IRxS7 GetConnection
    {
        get
        {
            lock (_lock)
            {
                if (_config.EnableConnectionReuse)
                {
                    // Round-robin load balancing
                    var connection = _connections[_currentIndex];
                    _currentIndex = (_currentIndex + 1) % _connections.Count;
                    return connection;
                }

                // Return first available connection
                return _connections.FirstOrDefault(c => c.IsConnectedValue) ?? _connections[0];
            }
        }
    }

    /// <summary>
    /// Gets all connections in the pool.
    /// </summary>
    /// <returns>All connections.</returns>
    public IEnumerable<IRxS7> GetAllConnections() => [.. _connections];

    /// <summary>
    /// Disposes all connections in the pool.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the ConnectionPool and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var connection in _connections)
            {
                connection?.Dispose();
            }

            _disposed = true;
        }
    }
}
