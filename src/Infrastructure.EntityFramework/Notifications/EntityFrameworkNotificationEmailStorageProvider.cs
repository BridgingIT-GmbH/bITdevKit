// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Persists notification emails in an Entity Framework backed outbox store.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="INotificationEmailContext"/>.</typeparam>
public class EntityFrameworkNotificationEmailStorageProvider<TContext>(
    IServiceProvider serviceProvider,
    ILogger<EntityFrameworkNotificationEmailStorageProvider<TContext>> logger,
    OutboxNotificationEmailOptions outboxOptions = null) : INotificationStorageProvider
    where TContext : DbContext, INotificationEmailContext
{
    private const string LeaseOwnerPropertyName = "__NotificationOutboxLeaseOwner";
    private const int ClaimCandidateMultiplier = 4;
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<EntityFrameworkNotificationEmailStorageProvider<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OutboxNotificationEmailOptions outboxOptions = outboxOptions ?? new OutboxNotificationEmailOptions();
    private readonly string leaseOwner = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    /// <inheritdoc />
    public async Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                var entity = this.MapToEntity(emailMessage);
                this.logger.LogDebug("{LogKey} storage - save email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                context.NotificationsEmails.Add(entity);

                await context.SaveChangesAsync(cancellationToken);

                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to save message with ID {MessageId}", Application.Notifications.Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure()
                    .WithError(new Error($"Failed to save message: {ex.Message}")));
            }
        }

        return await Task.FromResult(Result.Failure()
            .WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var leaseOwner = this.GetLeaseOwner(emailMessage);
                var now = DateTimeOffset.UtcNow;

                var entityQuery = context.NotificationsEmails
                    .Include(e => e.Attachments)
                    .Where(e => e.Id == emailMessage.Id);

                if (!string.IsNullOrWhiteSpace(leaseOwner))
                {
                    entityQuery = entityQuery.Where(e =>
                        e.LockedBy == leaseOwner &&
                        e.LockedUntil != null &&
                        e.LockedUntil >= now);
                }

                var entity = await entityQuery.FirstOrDefaultAsync(cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure()
                        .WithError(new Error(string.IsNullOrWhiteSpace(leaseOwner)
                            ? $"EmailMessage with ID {emailMessage.Id} not found"
                            : $"EmailMessage with ID {emailMessage.Id} is not currently leased by this worker")));
                }

                this.logger.LogDebug("{LogKey} storage - update email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                this.MapToEntity(emailMessage, entity);
                entity.LockedBy = null;
                entity.LockedUntil = null;
                entity.AdvanceConcurrencyVersion();

                await context.SaveChangesAsync(cancellationToken);

                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateConcurrencyException ex)
            {
                this.logger.LogWarning(ex, "{LogKey} storage - concurrency conflict updating message with ID {MessageId}", Application.Notifications.Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure()
                    .WithError(new Error($"Concurrency conflict: {ex.Message}")));
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to update message with ID {MessageId}", Application.Notifications.Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure()
                    .WithError(new Error($"Failed to update message: {ex.Message}")));
            }
        }

        return await Task.FromResult(Result.Failure()
            .WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var leaseOwner = this.GetLeaseOwner(emailMessage);
                var now = DateTimeOffset.UtcNow;

                var entityQuery = context.NotificationsEmails
                    .Where(e => e.Id == emailMessage.Id);

                if (!string.IsNullOrWhiteSpace(leaseOwner))
                {
                    entityQuery = entityQuery.Where(e =>
                        e.LockedBy == leaseOwner &&
                        e.LockedUntil != null &&
                        e.LockedUntil >= now);
                }

                var entity = await entityQuery.FirstOrDefaultAsync(cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error(string.IsNullOrWhiteSpace(leaseOwner)
                        ? $"EmailMessage with ID {emailMessage.Id} not found"
                        : $"EmailMessage with ID {emailMessage.Id} is not currently leased by this worker")));
                }

                this.logger.LogDebug("{LogKey} storage - archive email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                entity.IsArchived = true;
                entity.ArchivedDate ??= DateTimeOffset.UtcNow;
                entity.LockedBy = null;
                entity.LockedUntil = null;
                entity.AdvanceConcurrencyVersion();

                await context.SaveChangesAsync(cancellationToken);

                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to delete message with ID {MessageId}", Application.Notifications.Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure()
                    .WithError(new Error($"Failed to delete message: {ex.Message}")));
            }
        }

        return await Task.FromResult(Result.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<TMessage>>> GetPendingAsync<TMessage>(
        int batchSize,
        //int maxRetries,
        CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (typeof(TMessage) == typeof(EmailMessage))
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();
                var claimedIds = await this.ClaimPendingMessageIdsAsync(context, batchSize, cancellationToken);

                if (claimedIds.Count == 0)
                {
                    return await Task.FromResult(Result<IEnumerable<TMessage>>.Success([]));
                }

                this.logger.LogDebug("{LogKey} storage - retrieve up to {BatchSize} pending email messages", Application.Notifications.Constants.LogKey, batchSize);
                var entities = await context.NotificationsEmails.AsNoTracking()
                    .Where(m => !m.IsArchived && claimedIds.Contains(m.Id))
                    .Include(m => m.Attachments)
                    .OrderBy(m => m.CreatedAt)
                    .ToListAsync(cancellationToken);

                var messages = entities.Select(this.MapToMessage).Cast<TMessage>();
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Success(messages));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to retrieve pending messages for type {MessageType}", Application.Notifications.Constants.LogKey, typeof(TMessage).Name);
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure()
                    .WithError(new Error($"Failed to retrieve pending messages: {ex.Message}")));
            }
        }

        return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    private EmailMessageEntity MapToEntity(EmailMessage message)
    {
        return message == null
            ? throw new ArgumentNullException(nameof(message))
            : new EmailMessageEntity
            {
                Id = message.Id,
                Subject = message.Subject,
                Body = message.Body,
                IsHtml = message.IsHtml,
                Priority = message.Priority,
                Status = message.Status,
                RetryCount = message.RetryCount,
                CreatedAt = message.CreatedAt,
                SentAt = message.SentAt,
                LockedBy = this.GetLeaseOwner(message),
                To = message.To == null ? null : JsonSerializer.Serialize(message.To, DefaultJsonSerializerOptions.Create()),
                CC = message.CC == null ? null : JsonSerializer.Serialize(message.CC, DefaultJsonSerializerOptions.Create()),
                BCC = message.BCC == null ? null : JsonSerializer.Serialize(message.BCC, DefaultJsonSerializerOptions.Create()),
                From = message.From == null ? null : JsonSerializer.Serialize(message.From, DefaultJsonSerializerOptions.Create()),
                ReplyTo = message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultJsonSerializerOptions.Create()),
                Headers = message.Headers == null ? null : JsonSerializer.Serialize(message.Headers, DefaultJsonSerializerOptions.Create()),
                PropertiesJson = this.SerializeProperties(message.Properties),
                Attachments = message.Attachments?.ConvertAll(e =>
                    new EmailMessageAttachmentEntity
                    {
                        Id = e.Id,
                        EmailMessageId = message.Id,
                        FileName = e.FileName,
                        ContentType = e.ContentType,
                        Content = e.Content,
                        ContentId = e.ContentId,
                        IsEmbedded = e.IsEmbedded
                    })
            };
    }

    private void MapToEntity(EmailMessage message, EmailMessageEntity entity)
    {
        if (message == null || entity == null)
        {
            throw new ArgumentNullException(message == null ? nameof(message) : nameof(entity));
        }

        entity.Subject = message.Subject;
        entity.Body = message.Body;
        entity.IsHtml = message.IsHtml;
        entity.Priority = message.Priority;
        entity.Status = message.Status;
        entity.RetryCount = message.RetryCount;
        entity.CreatedAt = message.CreatedAt;
        entity.SentAt = message.SentAt;
        entity.LockedBy = this.GetLeaseOwner(message);
        entity.To = message.To.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.To, DefaultJsonSerializerOptions.Create());
        entity.CC = message.CC.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.CC, DefaultJsonSerializerOptions.Create());
        entity.BCC = message.BCC.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.BCC, DefaultJsonSerializerOptions.Create());
        entity.From = message.From == null ? null : JsonSerializer.Serialize(message.From, DefaultJsonSerializerOptions.Create());
        entity.ReplyTo = message.ReplyTo == null ? null : message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultJsonSerializerOptions.Create());
        entity.Headers = message.Headers == null ? null : JsonSerializer.Serialize(message.Headers, DefaultJsonSerializerOptions.Create());
        entity.PropertiesJson = this.SerializeProperties(message.Properties);
        entity.Attachments.Clear();
        entity.Attachments.AddRange(message.Attachments?.Select(e =>
            new EmailMessageAttachmentEntity
            {
                Id = e.Id,
                EmailMessageId = message.Id,
                FileName = e.FileName,
                ContentType = e.ContentType,
                Content = e.Content,
                ContentId = e.ContentId,
                IsEmbedded = e.IsEmbedded
            }));
    }

    private EmailMessage MapToMessage(EmailMessageEntity entity)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        var message = new EmailMessage
        {
            Id = entity.Id,
            To = entity.To.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<List<string>>(entity.To, DefaultJsonSerializerOptions.Create()),
            CC = entity.CC.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<List<string>>(entity.CC, DefaultJsonSerializerOptions.Create()),
            BCC = entity.BCC.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<List<string>>(entity.BCC, DefaultJsonSerializerOptions.Create()),
            From = entity.From.IsNullOrEmpty() ? null : JsonSerializer.Deserialize<EmailAddress>(entity.From, DefaultJsonSerializerOptions.Create()),
            ReplyTo = entity.ReplyTo.IsNullOrEmpty() ? null : JsonSerializer.Deserialize<EmailAddress>(entity.ReplyTo, DefaultJsonSerializerOptions.Create()),
            Subject = entity.Subject,
            Body = entity.Body,
            IsHtml = entity.IsHtml,
            Headers = entity.Headers.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Headers, DefaultJsonSerializerOptions.Create()),
            Properties = entity.PropertiesJson.IsNullOrEmpty() ? [] : JsonSerializer.Deserialize<Dictionary<string, object>>(entity.PropertiesJson, DefaultJsonSerializerOptions.Create()),
            Priority = entity.Priority,
            Status = entity.Status,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt,
            SentAt = entity.SentAt,
            Attachments = entity.Attachments?.ConvertAll(e =>
                new EmailAttachment
                {
                    Id = e.Id,
                    EmailMessageId = e.EmailMessageId,
                    FileName = e.FileName,
                    ContentType = e.ContentType,
                    Content = e.Content,
                    ContentId = e.ContentId,
                    IsEmbedded = e.IsEmbedded
                })
        };

        if (!string.IsNullOrWhiteSpace(entity.LockedBy))
        {
            message.Properties[LeaseOwnerPropertyName] = entity.LockedBy;
        }

        return message;
    }

    private async Task<List<Guid>> ClaimPendingMessageIdsAsync(TContext context, int batchSize, CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var candidateIds = await context.NotificationsEmails.AsNoTracking()
            .Where(message => !message.IsArchived)
            .Where(message =>
                message.Status == EmailMessageStatus.Pending ||
                (message.Status == EmailMessageStatus.Failed && message.RetryCount < this.outboxOptions.RetryCount) ||
                (message.Status == EmailMessageStatus.Locked && message.LockedUntil < now))
            .Where(message => message.LockedUntil == null || message.LockedUntil < now)
            .OrderBy(message => message.CreatedAt)
            .Take(Math.Max(batchSize, 1) * ClaimCandidateMultiplier)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        var claimedIds = new List<Guid>(Math.Min(batchSize, candidateIds.Count));
        foreach (var candidateId in candidateIds)
        {
            if (claimedIds.Count >= batchSize)
            {
                break;
            }

            if (await this.TryClaimMessageAsync(context, candidateId, cancellationToken))
            {
                claimedIds.Add(candidateId);
            }
        }

        return claimedIds;
    }

    private async Task<bool> TryClaimMessageAsync(TContext context, Guid messageId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lockedUntil = now.Add(this.GetLeaseDuration());
        var concurrencyVersion = Guid.NewGuid();

        if (SupportsExecuteUpdate(context))
        {
            var claimed = await context.NotificationsEmails
                .Where(message =>
                    message.Id == messageId &&
                    !message.IsArchived &&
                    (message.Status == EmailMessageStatus.Pending ||
                     (message.Status == EmailMessageStatus.Failed && message.RetryCount < this.outboxOptions.RetryCount) ||
                     (message.Status == EmailMessageStatus.Locked && message.LockedUntil < now)) &&
                    (message.LockedUntil == null || message.LockedUntil < now))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(message => message.Status, EmailMessageStatus.Locked)
                        .SetProperty(message => message.LockedBy, this.leaseOwner)
                        .SetProperty(message => message.LockedUntil, lockedUntil)
                        .SetProperty(message => message.ConcurrencyVersion, concurrencyVersion),
                    cancellationToken);

            return claimed > 0;
        }

        var entity = await context.NotificationsEmails
            .FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
        if (!this.CanClaimMessage(entity, now))
        {
            return false;
        }

        entity.Status = EmailMessageStatus.Locked;
        entity.LockedBy = this.leaseOwner;
        entity.LockedUntil = lockedUntil;
        entity.ConcurrencyVersion = concurrencyVersion;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private bool CanClaimMessage(EmailMessageEntity entity, DateTimeOffset now)
    {
        if (entity == null)
        {
            return false;
        }

        if (entity.IsArchived)
        {
            return false;
        }

        if (entity.LockedUntil != null && entity.LockedUntil >= now)
        {
            return false;
        }

        return entity.Status == EmailMessageStatus.Pending ||
            (entity.Status == EmailMessageStatus.Failed && entity.RetryCount < this.outboxOptions.RetryCount) ||
            (entity.Status == EmailMessageStatus.Locked && entity.LockedUntil < now);
    }

    private string GetLeaseOwner(EmailMessage message)
    {
        if (message?.Properties == null ||
            !message.Properties.TryGetValue(LeaseOwnerPropertyName, out var value) ||
            value == null)
        {
            return null;
        }

        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            _ => value.ToString()
        };
    }

    private TimeSpan GetLeaseDuration()
    {
        return this.outboxOptions.LeaseDuration > TimeSpan.Zero
            ? this.outboxOptions.LeaseDuration
            : TimeSpan.FromMinutes(5);
    }

    private string SerializeProperties(IDictionary<string, object> properties)
    {
        if (properties.IsNullOrEmpty())
        {
            return null;
        }

        var persistedProperties = properties
            .Where(pair => !string.Equals(pair.Key, LeaseOwnerPropertyName, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return persistedProperties.Count == 0
            ? null
            : JsonSerializer.Serialize(persistedProperties, DefaultJsonSerializerOptions.Create());
    }

    private static bool SupportsExecuteUpdate(TContext context)
    {
        return context.Database.IsRelational();
    }
}
