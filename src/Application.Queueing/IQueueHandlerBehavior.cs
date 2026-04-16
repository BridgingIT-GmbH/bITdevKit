namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Defines a behavior that wraps queue handler execution.
/// </summary>
public interface IQueueHandlerBehavior
{
    /// <summary>
    /// Executes behavior logic around queue message handling.
    /// </summary>
    /// <param name="message">The queued message being processed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="handler">The current handler instance.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    Task Handle(IQueueMessage message, CancellationToken cancellationToken, object handler, QueueHandlerDelegate next);
}
