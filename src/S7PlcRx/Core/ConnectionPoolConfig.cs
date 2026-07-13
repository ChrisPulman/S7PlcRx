// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Core;
#else
namespace S7PlcRx.Core;
#endif

/// <summary>Represents the configuration settings for a connection pool, including limits, timeouts, and behavior options.</summary>
/// <remarks>Use this class to specify parameters that control the size, performance, and health monitoring of a
/// connection pool. Adjusting these settings can help optimize resource usage and connection reliability for
/// applications that manage multiple concurrent connections.</remarks>
public sealed class ConnectionPoolConfig
{
    /// <summary>Defines the default connection timeout in seconds.</summary>
    private const int DefaultConnectionTimeoutSeconds = 30;

    /// <summary>Gets or sets the maximum pool size.</summary>
    public int MaxPoolSize { get; set; } = 10;

    /// <summary>Gets or sets the maximum number of connections in the pool.</summary>
    public int MaxConnections { get; set; } = 10;

    /// <summary>Gets or sets the connection timeout.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(DefaultConnectionTimeoutSeconds);

    /// <summary>Gets or sets a value indicating whether to enable load balancing.</summary>
    public bool EnableLoadBalancing { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to enable connection reuse.</summary>
    public bool EnableConnectionReuse { get; set; } = true;

    /// <summary>Gets or sets the health check interval.</summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}
