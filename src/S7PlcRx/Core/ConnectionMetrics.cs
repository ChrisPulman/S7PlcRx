// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using TimeSpan = System.TimeSpan;

namespace S7PlcRx.Core;

/// <summary>
/// Provides metrics and statistics for a network connection, including data transfer counts, error rates, and average
/// operation times.
/// </summary>
/// <remarks>This class is intended for internal use to monitor and analyze connection performance. It is not
/// thread-safe for mutation; callers should ensure appropriate synchronization if accessed concurrently. Metrics are
/// updated as operations are recorded and can be retrieved for monitoring or diagnostic purposes.</remarks>
internal class ConnectionMetrics
{
    private readonly Queue<TimeSpan> _sendTimes = new();
    private readonly Queue<TimeSpan> _receiveTimes = new();
    private readonly object _lock = new();
    private long _bytesSent;
    private long _bytesReceived;
    private long _errorCount;
    private long _operationCount;

    /// <summary>
    /// Gets the total number of bytes sent over the connection.
    /// </summary>
    public long BytesSent => _bytesSent;

    /// <summary>
    /// Gets the total number of bytes received.
    /// </summary>
    public long BytesReceived => _bytesReceived;

    /// <summary>
    /// Gets the total number of errors that have occurred.
    /// </summary>
    public long ErrorCount => _errorCount;

    /// <summary>
    /// Gets the total number of operations that have been performed.
    /// </summary>
    public long OperationCount => _operationCount;

    /// <summary>
    /// Gets the average time taken to send messages.
    /// </summary>
    /// <remarks>Returns a value of TimeSpan.Zero if no send operations have been recorded.</remarks>
    public TimeSpan AverageSendTime
    {
        get
        {
            lock (_lock)
            {
                return _sendTimes.Count > 0
                    ? TimeSpan.FromTicks((long)_sendTimes.Average(t => t.Ticks))
                    : TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Gets the average time taken to receive messages.
    /// </summary>
    /// <remarks>If no messages have been received, the value is <see cref="TimeSpan.Zero"/>.</remarks>
    public TimeSpan AverageReceiveTime
    {
        get
        {
            lock (_lock)
            {
                return _receiveTimes.Count > 0
                    ? TimeSpan.FromTicks((long)_receiveTimes.Average(t => t.Ticks))
                    : TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Gets the ratio of failed operations to total operations performed.
    /// </summary>
    /// <remarks>The error rate is calculated as the number of errors divided by the total number of
    /// operations. If no operations have been performed, the error rate is 0.</remarks>
    public double ErrorRate => OperationCount > 0 ? (double)ErrorCount / OperationCount : 0;

    /// <summary>
    /// Records the duration and number of bytes for a completed send operation.
    /// </summary>
    /// <remarks>This method updates internal statistics for monitoring send performance. It is thread-safe
    /// and can be called concurrently from multiple threads.</remarks>
    /// <param name="duration">The time taken to complete the send operation.</param>
    /// <param name="bytes">The number of bytes sent during the operation.</param>
    public void RecordSend(TimeSpan duration, long bytes)
    {
        Interlocked.Add(ref _bytesSent, bytes);
        Interlocked.Increment(ref _operationCount);

        lock (_lock)
        {
            _sendTimes.Enqueue(duration);
            if (_sendTimes.Count > 100)
            {
                _sendTimes.Dequeue();
            }
        }
    }

    /// <summary>
    /// Records the duration and number of bytes for a single receive operation.
    /// </summary>
    /// <remarks>This method updates internal statistics for receive operations, including total bytes
    /// received and recent receive durations. It is thread-safe and can be called concurrently from multiple
    /// threads.</remarks>
    /// <param name="duration">The time taken to complete the receive operation.</param>
    /// <param name="bytes">The number of bytes received during the operation. Must be zero or greater.</param>
    public void RecordReceive(TimeSpan duration, long bytes)
    {
        Interlocked.Add(ref _bytesReceived, bytes);
        Interlocked.Increment(ref _operationCount);

        lock (_lock)
        {
            _receiveTimes.Enqueue(duration);
            if (_receiveTimes.Count > 100)
            {
                _receiveTimes.Dequeue();
            }
        }
    }

    /// <summary>
    /// Records the occurrence of an error and increments the error count for monitoring or diagnostic purposes.
    /// </summary>
    public void RecordError()
    {
        Interlocked.Increment(ref _errorCount);
        Interlocked.Increment(ref _operationCount);
    }

    /// <summary>
    /// Creates and returns a snapshot of the current connection metrics.
    /// </summary>
    /// <remarks>Use this method to obtain a consistent, point-in-time view of the connection's metrics for
    /// reporting or analysis purposes. The snapshot is not updated if the underlying metrics change after the
    /// call.</remarks>
    /// <returns>A new <see cref="ConnectionMetrics"/> instance containing the current values of all tracked metrics. The
    /// returned object is independent of future changes to the original metrics.</returns>
    public ConnectionMetrics GetSnapshot()
    {
        var snapshot = new ConnectionMetrics
        {
            _bytesSent = BytesSent,
            _bytesReceived = BytesReceived,
            _errorCount = ErrorCount,
            _operationCount = OperationCount
        };

        lock (_lock)
        {
            foreach (var time in _sendTimes)
            {
                snapshot._sendTimes.Enqueue(time);
            }

            foreach (var time in _receiveTimes)
            {
                snapshot._receiveTimes.Enqueue(time);
            }
        }

        return snapshot;
    }
}
