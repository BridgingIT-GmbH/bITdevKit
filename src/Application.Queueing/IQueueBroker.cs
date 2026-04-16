namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Provides queue-specific subscription, enqueue, and processing operations.
/// </summary>
/// <example>
/// <code>
/// await queueBroker.Enqueue(new GenerateInvoiceQueueMessage { InvoiceId = invoiceId }, cancellationToken);
/// </code>
/// </example>
public interface IQueueBroker
{
    /// <summary>
    /// Registers a queue handler for the specified message type.
    /// </summary>
    Task Subscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    Task Subscribe(Type messageType, Type handlerType);

    Task Unsubscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    Task Unsubscribe(Type messageType, Type handlerType);

    Task Unsubscribe();

    /// <summary>
    /// Enqueues a queue message using the provider's default behavior.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Enqueue(IQueueMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a queue message and waits for provider-specific persistence confirmation.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a queue message request.
    /// </summary>
    /// <param name="messageRequest">The processing request.</param>
    Task Process(QueueMessageRequest messageRequest);
}
