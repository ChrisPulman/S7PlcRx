// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using TimeSpan = System.TimeSpan;

namespace S7PlcRx.Core;

/// <summary>
/// Connection metrics for performance monitoring and optimization.
/// </summary>
internal class ConnectionMetrics
{
    private readonly Queue<TimeSpan> _sendTimes = new();
    private readonly Queue<TimeSpan> _receiveTimes = new();
    private readonly object _lock = new();
    private long _bytesSent;
    private long _bytesReceived;
    private long _errorCount;
    private long _operationCount;

    public long BytesSent => _bytesSent;

    public long BytesReceived => _bytesReceived;

    public long ErrorCount => _errorCount;

    public long OperationCount => _operationCount;

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

    public double ErrorRate => OperationCount > 0 ? (double)ErrorCount / OperationCount : 0;

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

    public void RecordError()
    {
        Interlocked.Increment(ref _errorCount);
        Interlocked.Increment(ref _operationCount);
    }

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
