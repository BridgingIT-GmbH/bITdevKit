namespace BridgingIT.DevKit.Infrastructure.Notifications;

using Microsoft.EntityFrameworkCore;

public interface INotificationEmailContext
{
    DbSet<EmailMessageEntity> OutboxNotificationEmails { get; set; }

    DbSet<EmailAttachmentEntity> OutboxNotificationEmailAttachments { get; set; }
}