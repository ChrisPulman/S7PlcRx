// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace S7PlcRx.Enterprise;

/// <summary>
/// High-availability PLC manager with automatic failover.
/// </summary>
public class HighAvailabilityPlcManager : IDisposable
{
    private readonly IList<IRxS7> _backupPlcs;
    private readonly Timer _healthCheckTimer;
    private readonly Subject<PlcFailoverEvent> _failoverEvents = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HighAvailabilityPlcManager"/> class.
    /// </summary>
    /// <param name="primaryPlc">The primary PLC.</param>
    /// <param name="backupPlcs">The backup PLCs.</param>
    /// <param name="healthCheckInterval">The health check interval.</param>
    public HighAvailabilityPlcManager(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan? healthCheckInterval = null)
    {
        _backupPlcs = backupPlcs;
        ActivePLC = primaryPlc;

        var interval = healthCheckInterval ?? TimeSpan.FromSeconds(30);
        _healthCheckTimer = new Timer(PerformHealthCheck, null, interval, interval);
    }

    /// <summary>
    /// Gets the currently active PLC connection.
    /// </summary>
    public IRxS7 ActivePLC { get; private set; }

    /// <summary>
    /// Gets observable stream of failover events.
    /// </summary>
    public IObservable<PlcFailoverEvent> FailoverEvents => _failoverEvents.AsObservable();

    /// <summary>
    /// Manually triggers a failover to the next available backup.
    /// </summary>
    /// <returns>A value indicating whether failover was successful.</returns>
    public async Task<bool> TriggerFailover() => await PerformFailover("Manual failover triggered");

    /// <summary>
    /// Disposes the high-availability manager.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the HighAvailabilityPlcManager and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _healthCheckTimer?.Dispose();
            _failoverEvents?.Dispose();
            _disposed = true;
        }
    }

    private async void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!ActivePLC.IsConnectedValue)
            {
                await PerformFailover("Primary PLC connection lost");
            }
        }
        catch (Exception ex)
        {
            await PerformFailover($"Health check failed: {ex.Message}");
        }
    }

    private async Task<bool> PerformFailover(string reason)
    {
        foreach (var backupPlc in _backupPlcs)
        {
            if (backupPlc.IsConnectedValue)
            {
                var oldPlc = ActivePLC;
                ActivePLC = backupPlc;

                _failoverEvents.OnNext(new PlcFailoverEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Reason = reason,
                    OldPlc = $"{oldPlc.IP}:{oldPlc.PLCType}",
                    NewPlc = $"{backupPlc.IP}:{backupPlc.PLCType}"
                });

                return await Task.FromResult(true);
            }
        }

        return false;
    }
}
