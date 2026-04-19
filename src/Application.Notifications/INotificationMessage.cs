namespace BridgingIT.DevKit.Application.Notifications;

using System;

/// <summary>
/// Represents a notification message that can be sent immediately or persisted to an outbox.
/// </summary>
/// <example>
/// <code>
/// var message = new EmailMessage
/// {
///     Id = Guid.NewGuid(),
///     To = ["alice@example.com"],
///     Subject = "Todo created",
///     Body = "Your todo item is ready."
/// };
/// </code>
/// </example>
public interface INotificationMessage
{
    /// <summary>
    /// Gets the stable message identifier used by storage providers and outbox workers.
    /// </summary>
    Guid Id { get; }
}
