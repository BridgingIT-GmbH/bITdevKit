namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

public class LogPurgeQueue
{
    private readonly ConcurrentQueue<(DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval)> _queue = new();

    public void Enqueue(DateTimeOffset olderThan, bool archive, int batchSize, TimeSpan delayInterval)
    {
        _queue.Enqueue((olderThan, archive, batchSize, delayInterval));
    }

    public bool TryDequeue(out (DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval) request)
    {
        return _queue.TryDequeue(out request);
    }
}