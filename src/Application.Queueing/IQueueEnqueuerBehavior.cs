namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Defines a behavior that wraps queue enqueue operations.
/// </summary>
public interface IQueueEnqueuerBehavior
{
    /// <summary>
    /// Executes behavior logic around enqueue.
    /// </summary>
    /// <param name="message">The message being enqueued.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    Task Enqueue(IQueueMessage message, CancellationToken cancellationToken, QueueEnqueuerDelegate next);
}
