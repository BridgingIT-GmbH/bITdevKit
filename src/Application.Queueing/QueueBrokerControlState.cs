namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Stores runtime control state shared between queue brokers and operational services.
/// </summary>
public class QueueBrokerControlState
{
    private readonly object syncRoot = new();
    private readonly HashSet<string> pausedQueues = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pausedTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the currently paused queue names.
    /// </summary>
    public IReadOnlyCollection<string> GetPausedQueues()
    {
        lock (this.syncRoot)
        {
            return this.pausedQueues.OrderBy(item => item).ToArray();
        }
    }

    /// <summary>
    /// Gets the currently paused queue message types.
    /// </summary>
    public IReadOnlyCollection<string> GetPausedTypes()
    {
        lock (this.syncRoot)
        {
            return this.pausedTypes.OrderBy(item => item).ToArray();
        }
    }

    /// <summary>
    /// Indicates whether the specified queue is paused.
    /// </summary>
    public bool IsQueuePaused(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        lock (this.syncRoot)
        {
            return this.pausedQueues.Contains(queueName);
        }
    }

    /// <summary>
    /// Indicates whether the specified queue message type is paused.
    /// </summary>
    public bool IsMessageTypePaused(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        lock (this.syncRoot)
        {
            return this.pausedTypes.Contains(type);
        }
    }

    /// <summary>
    /// Marks the specified queue as paused.
    /// </summary>
    public void PauseQueue(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        lock (this.syncRoot)
        {
            this.pausedQueues.Add(queueName);
        }
    }

    /// <summary>
    /// Removes the paused marker from the specified queue.
    /// </summary>
    public void ResumeQueue(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        lock (this.syncRoot)
        {
            this.pausedQueues.Remove(queueName);
        }
    }

    /// <summary>
    /// Marks the specified queue message type as paused.
    /// </summary>
    public void PauseMessageType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        lock (this.syncRoot)
        {
            this.pausedTypes.Add(type);
        }
    }

    /// <summary>
    /// Removes the paused marker from the specified queue message type.
    /// </summary>
    public void ResumeMessageType(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        lock (this.syncRoot)
        {
            this.pausedTypes.Remove(type);
        }
    }
}