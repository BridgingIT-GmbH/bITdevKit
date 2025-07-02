namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Concurrent;

public class LogEntryMaintenanceQueue
{
    private readonly ConcurrentQueue<(DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval)> queue = [];

    public void Enqueue(DateTimeOffset olderThan, bool archive, int batchSize, TimeSpan delayInterval)
    {
        this.queue.Enqueue((olderThan, archive, batchSize, delayInterval));
    }

    public bool TryDequeue(out (DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval) request)
    {
        return this.queue.TryDequeue(out request);
    }
}