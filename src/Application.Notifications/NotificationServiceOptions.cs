namespace BridgingIT.DevKit.Application.Notifications;

using System;

public class NotificationServiceOptions
{
    public SmtpSettings SmtpSettings { get; set; } = new SmtpSettings();

    public OutboxNotificationEmailOptions OutboxOptions { get; set; } = new OutboxNotificationEmailOptions();

    public bool IsOutboxConfigured { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
