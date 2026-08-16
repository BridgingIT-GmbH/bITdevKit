namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the operational state of a queued message.
/// </summary>
public enum QueueMessageStatus
{
    /// <summary>
    /// Represents the pending value.
    /// </summary>
    Pending = 0,
    /// <summary>
    /// Represents the waiting for handler value.
    /// </summary>
    WaitingForHandler = 1,
    /// <summary>
    /// Represents the processing value.
    /// </summary>
    Processing = 2,
    /// <summary>
    /// Represents the succeeded value.
    /// </summary>
    Succeeded = 3,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed = 4,
    /// <summary>
    /// Represents the dead lettered value.
    /// </summary>
    DeadLettered = 5,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired = 6
}
