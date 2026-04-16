namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the processing outcome for a queue message attempt.
/// </summary>
public enum QueueProcessingResult
{
    Succeeded = 0,
    WaitingForHandler = 1,
    Failed = 2,
    Expired = 3
}
