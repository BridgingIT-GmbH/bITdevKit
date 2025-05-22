namespace BridgingIT.DevKit.Infrastructure.Notifications;

using Microsoft.EntityFrameworkCore;

public interface INotificationEmailContext
{
    DbSet<EmailMessage> OutboxNotificationEmails { get; set; }

    DbSet<EmailAttachment> OutboxNotificationEmailAttachments { get; set; }
}