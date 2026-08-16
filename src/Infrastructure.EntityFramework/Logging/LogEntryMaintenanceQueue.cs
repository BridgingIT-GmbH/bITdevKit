namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Concurrent;

/// <summary>
/// Represents log entry maintenance queue.
/// </summary>
public class LogEntryMaintenanceQueue
{
    private readonly ConcurrentQueue<(DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval)> queue = [];

    /// <summary>
    /// Executes the enqueue operation.
    /// </summary>
    /// <param name="olderThan">The older than used by the operation.</param>
    /// <param name="archive">The archive used by the operation.</param>
    /// <param name="batchSize">The batch size used by the operation.</param>
    /// <param name="delayInterval">The delay interval used by the operation.</param>
    public void Enqueue(DateTimeOffset olderThan, bool archive, int batchSize, TimeSpan delayInterval)
    {
        this.queue.Enqueue((olderThan, archive, batchSize, delayInterval));
    }

    /// <summary>
    /// Executes the try dequeue operation.
    /// </summary>
    /// <param name="request">The request used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryDequeue(out (DateTimeOffset OlderThan, bool Archive, int BatchSize, TimeSpan DelayInterval) request)
    {
        return this.queue.TryDequeue(out request);
    }
}
