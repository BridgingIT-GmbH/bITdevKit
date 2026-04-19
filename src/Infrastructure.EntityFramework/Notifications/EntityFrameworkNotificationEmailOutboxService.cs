// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System.Text.Json;
using BridgingIT.DevKit.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides an Entity Framework backed implementation of <see cref="INotificationEmailOutboxService" />.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="INotificationEmailContext" />.</typeparam>
public class EntityFrameworkNotificationEmailOutboxService<TContext>(IServiceProvider serviceProvider) : INotificationEmailOutboxService
    where TContext : DbContext, INotificationEmailContext
{
    /// <inheritdoc />
    public async Task<IEnumerable<NotificationEmailInfo>> GetMessagesAsync(
        EmailMessageStatus? status = null,
        string subject = null,
        string lockedBy = null,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.NotificationsEmails
            .Include(message => message.Attachments)
            .AsQueryable();

        query = query.WhereExpressionIf(message => message.Status == status, status.HasValue);
        query = query.WhereExpressionIf(message => message.Subject.Contains(subject), !subject.IsNullOrEmpty());
        query = query.WhereExpressionIf(message => message.LockedBy == lockedBy, !lockedBy.IsNullOrEmpty());
        query = query.WhereExpressionIf(message => message.CreatedAt >= createdAfter, createdAfter.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedAt <= createdBefore, createdBefore.HasValue);

        var messages = await query
            .OrderByDescending(message => message.CreatedAt)
            .TakeIf(take)
            .ToListAsync(cancellationToken)
            .AnyContext();

        return messages.Select(MapInfo).ToList();
    }

    /// <inheritdoc />
    public async Task<NotificationEmailInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.NotificationsEmails
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .AnyContext();

        return message is null ? null : MapInfo(message);
    }

    /// <inheritdoc />
    public async Task<NotificationEmailContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.NotificationsEmails
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .AnyContext();

        return message is null
            ? null
            : new NotificationEmailContentInfo
            {
                Id = message.Id,
                Subject = message.Subject,
                Body = message.Body,
                IsHtml = message.IsHtml
            };
    }

    /// <inheritdoc />
    public async Task<NotificationEmailStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.NotificationsEmails.AsQueryable();
        query = query.WhereExpressionIf(message => message.CreatedAt >= startDate || message.SentAt >= startDate, startDate.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedAt <= endDate || message.SentAt <= endDate, endDate.HasValue);

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        var now = DateTimeOffset.UtcNow;

        return new NotificationEmailStats
        {
            Total = messages.Count,
            Pending = messages.Count(message => message.Status == EmailMessageStatus.Pending),
            Locked = messages.Count(message => message.Status == EmailMessageStatus.Locked),
            Sent = messages.Count(message => message.Status == EmailMessageStatus.Sent),
            Failed = messages.Count(message => message.Status == EmailMessageStatus.Failed),
            Leased = messages.Count(message => !message.LockedBy.IsNullOrEmpty() && message.LockedUntil.HasValue && message.LockedUntil > now)
        };
    }

    /// <inheritdoc />
    public async Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.NotificationsEmails
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .AnyContext();
        if (message is null || message.Status != EmailMessageStatus.Failed)
        {
            return;
        }

        var properties = DeserializeProperties(message.PropertiesJson);
        properties.Remove("ProcessMessage");

        message.Status = EmailMessageStatus.Pending;
        message.RetryCount = 0;
        message.SentAt = null;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.PropertiesJson = SerializeProperties(properties);
        message.AdvanceConcurrencyVersion();

        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task DeleteMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.NotificationsEmails
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .AnyContext();
        if (message is null)
        {
            return;
        }

        context.NotificationsEmails.Remove(message);
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<EmailMessageStatus> statuses = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.NotificationsEmails
            .Include(item => item.Attachments)
            .AsQueryable();

        var statusArray = statuses?.ToArray();
        query = query.WhereExpressionIf(message => statusArray.Contains(message.Status), statusArray.SafeAny());
        query = query.WhereExpressionIf(message => (message.SentAt ?? message.CreatedAt) < olderThan, olderThan.HasValue);

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        context.NotificationsEmails.RemoveRange(messages);
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    private static NotificationEmailInfo MapInfo(EmailMessageEntity message)
    {
        var properties = DeserializeProperties(message.PropertiesJson);

        return new NotificationEmailInfo
        {
            Id = message.Id,
            To = DeserializeStringCollection(message.To),
            Cc = DeserializeStringCollection(message.CC),
            Bcc = DeserializeStringCollection(message.BCC),
            FromName = DeserializeAddress(message.From)?.Name,
            FromAddress = DeserializeAddress(message.From)?.Address,
            ReplyToName = DeserializeAddress(message.ReplyTo)?.Name,
            ReplyToAddress = DeserializeAddress(message.ReplyTo)?.Address,
            Subject = message.Subject,
            IsHtml = message.IsHtml,
            Priority = message.Priority,
            Status = message.Status,
            RetryCount = message.RetryCount,
            CreatedAt = message.CreatedAt,
            SentAt = message.SentAt,
            LockedBy = message.LockedBy,
            LockedUntil = message.LockedUntil,
            ProcessMessage = TryGetStringProperty(properties, "ProcessMessage"),
            AttachmentCount = message.Attachments?.Count ?? 0,
            Attachments = message.Attachments?.Select(attachment => new NotificationEmailAttachmentInfo
            {
                Id = attachment.Id,
                EmailMessageId = attachment.EmailMessageId,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                ContentLength = attachment.Content?.Length ?? 0,
                ContentId = attachment.ContentId,
                IsEmbedded = attachment.IsEmbedded
            }).ToList() ?? [],
            Properties = properties
        };
    }

    private static ICollection<string> DeserializeStringCollection(string value)
    {
        return value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<List<string>>(value, DefaultJsonSerializerOptions.Create()) ?? [];
    }

    private static EmailAddress DeserializeAddress(string value)
    {
        return value.IsNullOrEmpty()
            ? null
            : JsonSerializer.Deserialize<EmailAddress>(value, DefaultJsonSerializerOptions.Create());
    }

    private static Dictionary<string, object> DeserializeProperties(string value)
    {
        return value.IsNullOrEmpty()
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, object>>(value, DefaultJsonSerializerOptions.Create()) ?? [];
    }

    private static string SerializeProperties(IDictionary<string, object> properties)
    {
        return properties.IsNullOrEmpty()
            ? null
            : JsonSerializer.Serialize(properties, DefaultJsonSerializerOptions.Create());
    }

    private static string TryGetStringProperty(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (properties == null || !properties.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => value.ToString()
        };
    }
}
