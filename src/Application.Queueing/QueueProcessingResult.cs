namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the processing outcome for a queue message attempt.
/// </summary>
public enum QueueProcessingResult
{
    /// <summary>
    /// Represents the succeeded value.
    /// </summary>
    Succeeded = 0,
    /// <summary>
    /// Represents the waiting for handler value.
    /// </summary>
    WaitingForHandler = 1,
    /// <summary>
    /// Represents the failed value.
    /// </summary>
    Failed = 2,
    /// <summary>
    /// Represents the expired value.
    /// </summary>
    Expired = 3
}
