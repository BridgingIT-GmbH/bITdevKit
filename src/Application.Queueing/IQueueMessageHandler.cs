namespace BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Handles a specific queued message type.
/// </summary>
/// <typeparam name="TMessage">The queued message type.</typeparam>
/// <example>
/// <code>
/// public class GenerateInvoiceQueueHandler : IQueueMessageHandler&lt;GenerateInvoiceQueueMessage&gt;
/// {
///     public Task Handle(GenerateInvoiceQueueMessage message, CancellationToken cancellationToken)
///     {
///         return Task.CompletedTask;
///     }
/// }
/// </code>
/// </example>
public interface IQueueMessageHandler<TMessage>
    where TMessage : IQueueMessage
{
    /// <summary>
    /// Handles the queued message.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task Handle(TMessage message, CancellationToken cancellationToken);
}
