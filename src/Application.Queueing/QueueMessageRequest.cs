namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Carries a queue message through the broker processing pipeline.
/// </summary>
public class QueueMessageRequest
{
    /// <summary>
    /// Initializes a new processing request without a completion callback.
    /// </summary>
    public QueueMessageRequest(IQueueMessage message, CancellationToken cancellationToken)
        : this(message, _ => { }, cancellationToken)
    {
    }

    /// <summary>
    /// Initializes a new processing request.
    /// </summary>
    public QueueMessageRequest(IQueueMessage message, Action<QueueProcessingResult> onProcessComplete, CancellationToken cancellationToken)
    {
        this.Message = message ?? throw new ArgumentNullException(nameof(message));
        this.OnProcessComplete = onProcessComplete ?? (_ => { });
        this.CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the message being processed.
    /// </summary>
    public IQueueMessage Message { get; }

    /// <summary>
    /// Gets the callback invoked when processing completes.
    /// </summary>
    public Action<QueueProcessingResult> OnProcessComplete { get; }

    /// <summary>
    /// Gets the cancellation token for the processing operation.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets request-scoped properties used by broker implementations.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}
