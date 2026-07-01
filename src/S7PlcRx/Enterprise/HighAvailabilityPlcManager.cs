// Copyright (c) 2022-2026 Chris Pulman. All rights reserved.
// Chris Pulman licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive.Enterprise;
#else
namespace S7PlcRx.Enterprise;
#endif

/// <summary>
/// Provides high-availability management for a set of PLC (Programmable Logic Controller) connections, automatically
/// handling failover to backup PLCs in case of connection loss.
/// </summary>
/// <remarks>The HighAvailabilityPlcManager monitors the health of the primary PLC and automatically switches to a
/// backup PLC if the primary becomes unavailable. It exposes an observable stream of failover events for monitoring and
/// allows manual triggering of failover. This class is thread-safe for typical usage scenarios. Dispose the manager
/// when it is no longer needed to release resources.</remarks>
public class HighAvailabilityPlcManager : IDisposable
{
    /// <summary>Stores the b ac ku pp l c s used by this instance.</summary>
    private readonly IList<IRxS7> _backupPlcs;

    /// <summary>Stores the h ea lt hc he ck ti m e r used by this instance.</summary>
    private readonly Timer _healthCheckTimer;

    /// <summary>Stores the f ai lo ve re ve n t s used by this instance.</summary>
    private readonly Signal<PlcFailoverEvent> _failoverEvents = new();

    /// <summary>Stores the d is po s e d used by this instance.</summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HighAvailabilityPlcManager"/> class with a primary PLC, a list of backup PLCs,.
    /// and an optional health check interval.
    /// </summary>
    /// <remarks>The primary PLC is always treated as the first PLC in the managed list, regardless of its
    /// position in the provided backupPlcs collection. Health checks are performed at the specified interval to monitor
    /// PLC availability and facilitate failover if necessary.</remarks>
    /// <param name="primaryPlc">The primary PLC to be managed. Cannot be null.</param>
    /// <param name="backupPlcs">A list of backup PLCs to use for failover. The primary PLC will be inserted as the first element in this list.</param>
    /// <param name="healthCheckInterval">The interval at which health checks are performed on the PLCs. If null, a default interval of 30 seconds is
    /// used.</param>
    /// <exception cref="ArgumentNullException">Thrown if primaryPlc is null.</exception>
    public HighAvailabilityPlcManager(
        IRxS7 primaryPlc,
        IList<IRxS7> backupPlcs,
        TimeSpan? healthCheckInterval = null)
    {
        if (primaryPlc is null)
        {
            throw new ArgumentNullException(nameof(primaryPlc), "Primary PLC cannot be null.");
        }

        _backupPlcs = backupPlcs;
        _backupPlcs.Insert(0, primaryPlc); // Ensure primary is first in the list
        ActivePLC = primaryPlc;

        var interval = healthCheckInterval ?? TimeSpan.FromSeconds(30);
        _healthCheckTimer = new(PerformHealthCheck, null, interval, interval);
    }

    /// <summary>Gets the currently active PLC connection.</summary>
    public IRxS7 ActivePLC { get; private set; }

    /// <summary>Gets observable stream of failover events.</summary>
    public IObservable<PlcFailoverEvent> FailoverEvents => _failoverEvents;

    /// <summary>Manually triggers a failover to the next available backup.</summary>
    /// <returns>A value indicating whether failover was successful.</returns>
    public Task<bool> TriggerFailover() => PerformFailover("Manual failover triggered");

    /// <summary>Disposes the high-availability manager.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the unmanaged resources used by the HighAvailabilityPlcManager and optionally releases the managed resources.</summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _healthCheckTimer.Dispose();
            _failoverEvents.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Performs a health check on the active PLC connection and initiates failover if the connection is lost or a
    /// health check error occurs.
    /// </summary>
    /// <remarks>This method is intended to be used as a callback for timer-based health monitoring. If the
    /// object has been disposed, the method returns immediately without performing any checks.</remarks>
    /// <param name="state">An optional state object containing information to be used by the health check operation. This parameter is not
    /// used.</param>
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

    /// <summary>Attempts to switch the active PLC connection to a backup PLC in response to a failure condition.</summary>
    /// <remarks>If a backup PLC is available and connected, this method updates the active PLC and notifies
    /// subscribers of the failover event. If no backup PLC is connected, the active PLC remains unchanged and no event
    /// is raised.</remarks>
    /// <param name="reason">A string describing the reason for initiating the failover. This information is included in the failover event
    /// notification.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if failover to a
    /// backup PLC was successful; otherwise, <see langword="false"/>.</returns>
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
