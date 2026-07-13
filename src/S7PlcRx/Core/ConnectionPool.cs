// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
using S7PlcRx.Reactive.Enterprise;
#else
using S7PlcRx.Enterprise;
#endif

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>
/// Manages a pool of PLC connections, providing load-balanced access and connection reuse according to the specified
/// configuration.
/// </summary>
/// <remarks>The ConnectionPool enables efficient management of multiple PLC connections by reusing and balancing
/// requests across available connections. It supports configurable pool size and connection reuse strategies. This
/// class is thread-safe for concurrent access. Call Dispose to release all connections when the pool is no longer
/// needed.</remarks>
public class ConnectionPool : IDisposable
{
    /// <summary>Stores the c on ne ct io n s used by this instance.</summary>
    private readonly List<IRxS7> _connections = [];

    /// <summary>Stores the c on f i g used by this instance.</summary>
    private readonly ConnectionPoolConfig _config;

    /// <summary>Stores the lock used to protect connection selection and disposal.</summary>
    private readonly Lock _lock = new();

    /// <summary>Stores the d is po s e d used by this instance.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ConnectionPool"/> class.</summary>
    /// <param name="config">The pool configuration.</param>
    public ConnectionPool(ConnectionPoolConfig config) => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>Initializes a new instance of the <see cref="ConnectionPool"/> class.</summary>
    /// <param name="connectionConfigs">The connection configurations.</param>
    /// <param name="poolConfig">The pool configuration.</param>
    public ConnectionPool(
        IEnumerable<PlcConnectionConfig> connectionConfigs,
        ConnectionPoolConfig poolConfig)
    {
        _config = poolConfig ?? throw new ArgumentNullException(nameof(poolConfig));

        if (connectionConfigs is null)
        {
            throw new ArgumentNullException(nameof(connectionConfigs));
        }

        var addedConnections = 0;
        foreach (var config in connectionConfigs)
        {
            if (addedConnections >= poolConfig.MaxConnections)
            {
                break;
            }

            var plc = new RxS7(new(new(config.PLCType, config.IPAddress, config.Rack, config.Slot)));
            _connections.Add(plc);
            addedConnections++;
        }
    }

    /// <summary>Gets the maximum number of connections in the pool.</summary>
    public int MaxConnections => _config.MaxConnections;

    /// <summary>Gets the number of active connections.</summary>
    public int ActiveConnections
    {
        get
        {
            var activeConnections = 0;
            foreach (var connection in _connections)
            {
                if (connection.IsConnectedValue)
                {
                    activeConnections++;
                }
            }

            return activeConnections;
        }
    }

    /// <summary>Gets a connection from the pool using load balancing.</summary>
    /// <returns>An available PLC connection.</returns>
    public IRxS7 Connection => GetConnectionCore();

    /// <summary>Gets all connections in the pool.</summary>
    public IEnumerable<IRxS7> AllConnections => [.. _connections];

    /// <summary>Disposes all connections in the pool.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the unmanaged resources used by the ConnectionPool and optionally releases the managed resources.</summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var connection in _connections)
            {
                connection?.Dispose();
            }
        }

        _disposed = true;
    }

    /// <summary>Gets a connection using the configured selection strategy.</summary>
    /// <returns>The selected connection.</returns>
    private IRxS7 GetConnectionCore()
    {
        lock (_lock)
        {
            return _config.EnableConnectionReuse ? RotateConnection() : GetFirstAvailableConnection();
        }
    }

    /// <summary>Gets the first available connection, or the first connection when none are connected.</summary>
    /// <returns>The selected connection.</returns>
    private IRxS7 GetFirstAvailableConnection()
    {
        foreach (var connection in _connections)
        {
            if (connection.IsConnectedValue)
            {
                return connection;
            }
        }

        return _connections[0];
    }

    /// <summary>Rotates and returns the next round-robin connection.</summary>
    /// <returns>The selected connection.</returns>
    private IRxS7 RotateConnection()
    {
        var connection = _connections[0];
        _connections.RemoveAt(0);
        _connections.Add(connection);
        return connection;
    }
}
