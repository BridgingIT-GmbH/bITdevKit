namespace BridgingIT.DevKit.Application.Notifications;

/// <summary>
/// Controls how a notification message should be processed when it is submitted to a notification service.
/// </summary>
/// <example>
/// <code>
/// await notificationService.SendAsync(
///     message,
///     new NotificationSendOptions { SendImmediately = true },
///     cancellationToken);
/// </code>
/// </example>
public class NotificationSendOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the message should be sent immediately after it is saved to the outbox.
    /// </summary>
    public bool SendImmediately { get; set; }
}
