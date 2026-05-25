namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Provides queue-specific subscription, enqueue, and processing operations on top of the shared outbound queue broker contract.
/// </summary>
/// <example>
/// <code>
/// await queueBroker.Enqueue(new GenerateInvoiceQueueMessage { InvoiceId = invoiceId }, cancellationToken);
/// </code>
/// </example>
public interface IQueueBrokerRuntime : IQueueBroker
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
    /// Processes a queue message request.
    /// </summary>
    /// <param name="messageRequest">The processing request.</param>
    Task Process(QueueMessageRequest messageRequest);
}
