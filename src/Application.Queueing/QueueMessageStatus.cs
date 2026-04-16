namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the operational state of a queued message.
/// </summary>
public enum QueueMessageStatus
{
    Pending = 0,
    WaitingForHandler = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    DeadLettered = 5,
    Expired = 6
}
