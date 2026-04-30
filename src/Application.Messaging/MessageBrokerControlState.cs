namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Stores runtime control state shared between message brokers and operational services.
/// </summary>
public class MessageBrokerControlState
{
    private readonly object syncRoot = new();
    private readonly HashSet<string> pausedTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the currently paused message types.
    /// </summary>
    public IReadOnlyCollection<string> GetPausedTypes()
    {
        lock (this.syncRoot)
        {
            return this.pausedTypes.OrderBy(item => item).ToArray();
        }
    }

    /// <summary>
    /// Indicates whether the specified message type is paused.
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
    /// Marks the specified message type as paused.
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
    /// Removes the paused marker from the specified message type.
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
