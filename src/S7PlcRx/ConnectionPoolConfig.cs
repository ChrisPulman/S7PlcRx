// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace S7PlcRx;

/// <summary>
/// Connection pool configuration.
/// </summary>
public sealed class ConnectionPoolConfig
{
    /// <summary>Gets or sets the maximum pool size.</summary>
    public int MaxPoolSize { get; set; } = 10;

    /// <summary>Gets or sets the connection timeout.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets a value indicating whether to enable load balancing.</summary>
    public bool EnableLoadBalancing { get; set; } = true;

    /// <summary>Gets or sets the health check interval.</summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}
