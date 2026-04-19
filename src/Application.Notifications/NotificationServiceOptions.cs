namespace BridgingIT.DevKit.Application.Notifications;

using System;

/// <summary>
/// Holds notification service infrastructure settings such as SMTP connectivity and outbox behavior.
/// </summary>
/// <example>
/// <code>
/// services.AddNotificationService&lt;EmailMessage&gt;(configuration, builder =>
/// {
///     builder.WithSmtpSettings(settings => settings.Sender("DoFiesta", "noreply@example.com"));
/// });
/// </code>
/// </example>
public class NotificationServiceOptions
{
    /// <summary>
    /// Gets or sets the SMTP connection settings used for email delivery.
    /// </summary>
    public SmtpSettings SmtpSettings { get; set; } = new SmtpSettings();

    /// <summary>
    /// Gets or sets the outbox processing options.
    /// </summary>
    public OutboxNotificationEmailOptions OutboxOptions { get; set; } = new OutboxNotificationEmailOptions();

    /// <summary>
    /// Gets or sets a value indicating whether an outbox has been configured for the notification service.
    /// </summary>
    public bool IsOutboxConfigured { get; set; }

    /// <summary>
    /// Gets or sets the timeout applied to delivery operations.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
