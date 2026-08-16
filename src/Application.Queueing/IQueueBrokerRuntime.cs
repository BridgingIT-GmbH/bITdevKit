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

    /// <summary>
    /// Executes the subscribe operation.
    /// </summary>
    /// <param name="messageType">The message type used by the operation.</param>
    /// <param name="handlerType">The handler type used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Subscribe(Type messageType, Type handlerType);

    /// <summary>
    /// Executes the unsubscribe operation.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Unsubscribe<TMessage, THandler>()
        where TMessage : IQueueMessage
        where THandler : IQueueMessageHandler<TMessage>;

    /// <summary>
    /// Executes the unsubscribe operation.
    /// </summary>
    /// <param name="messageType">The message type used by the operation.</param>
    /// <param name="handlerType">The handler type used by the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Unsubscribe(Type messageType, Type handlerType);

    /// <summary>
    /// Executes the unsubscribe operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Unsubscribe();

    /// <summary>
    /// Processes a queue message request.
    /// </summary>
    /// <param name="messageRequest">The processing request.</param>
    Task Process(QueueMessageRequest messageRequest);
}
