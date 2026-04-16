namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Represents the next delegate in the queue enqueue pipeline.
/// </summary>
public delegate Task QueueEnqueuerDelegate();

/// <summary>
/// Represents the next delegate in the queue handler pipeline.
/// </summary>
public delegate Task QueueHandlerDelegate();
