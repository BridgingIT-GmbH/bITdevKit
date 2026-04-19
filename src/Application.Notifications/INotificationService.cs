namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;

/// <summary>
/// Sends or queues notification messages of the specified type.
/// </summary>
/// <typeparam name="TMessage">The notification message type.</typeparam>
/// <example>
/// <code>
/// var result = await notificationService.SendAsync(
///     new EmailMessage
///     {
///         Id = Guid.NewGuid(),
///         To = ["alice@example.com"],
///         Subject = "Todo created",
///         Body = "Your todo item is ready."
///     },
///     new NotificationSendOptions { SendImmediately = true },
///     cancellationToken);
/// </code>
/// </example>
public interface INotificationService<TMessage>
    where TMessage : class, INotificationMessage
{
    /// <summary>
    /// Sends the message immediately or persists it to the outbox, depending on the supplied options and configuration.
    /// </summary>
    /// <param name="message">The notification message to process.</param>
    /// <param name="options">The send options that control immediate delivery behavior.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> SendAsync(TMessage message, NotificationSendOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the message to the configured notification outbox without forcing an immediate send.
    /// </summary>
    /// <param name="message">The notification message to queue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<Result> QueueAsync(TMessage message, CancellationToken cancellationToken);
}
