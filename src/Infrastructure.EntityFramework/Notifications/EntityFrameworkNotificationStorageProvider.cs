namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class EntityFrameworkNotificationStorageProvider<TContext> : INotificationStorageProvider
    where TContext : DbContext, INotificationEmailContext
{
    private readonly TContext context;
    private readonly ILogger<EntityFrameworkNotificationStorageProvider<TContext>> logger;

    public EntityFrameworkNotificationStorageProvider(
        TContext context,
        ILogger<EntityFrameworkNotificationStorageProvider<TContext>> logger)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                var entity = this.MapToEntity(emailMessage);
                this.logger.LogInformation("Saving EmailMessage with ID {MessageId}", entity.Id);
                this.context.OutboxNotificationEmails.Add(entity);
                await this.context.SaveChangesAsync(cancellationToken);
                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "Failed to save message with ID {MessageId}", message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to save message: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    public async Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                var entity = await this.context.OutboxNotificationEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == emailMessage.Id, cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error($"EmailMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogInformation("Updating EmailMessage with ID {MessageId}", entity.Id);
                this.MapToEntity(emailMessage, entity);
                this.context.OutboxNotificationEmails.Update(entity);
                await this.context.SaveChangesAsync(cancellationToken);
                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateConcurrencyException ex)
            {
                this.logger.LogWarning(ex, "Concurrency conflict updating message with ID {MessageId}", message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Concurrency conflict: {ex.Message}")));
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "Failed to update message with ID {MessageId}", message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to update message: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    public async Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                var entity = await this.context.OutboxNotificationEmails
                    .FirstOrDefaultAsync(e => e.Id == emailMessage.Id, cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error($"EmailMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogInformation("Deleting EmailMessage with ID {MessageId}", entity.Id);
                this.context.OutboxNotificationEmails.Remove(entity);
                await this.context.SaveChangesAsync(cancellationToken);
                return await Task.FromResult(Result.Success());
            }
            catch (DbUpdateException ex)
            {
                this.logger.LogError(ex, "Failed to delete message with ID {MessageId}", message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to delete message: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    public async Task<Result<IEnumerable<TMessage>>> GetPendingAsync<TMessage>(
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (typeof(TMessage) == typeof(EmailMessage))
        {
            try
            {
                this.logger.LogInformation("Retrieving up to {BatchSize} pending EmailMessages with max retries {MaxRetries}", batchSize, maxRetries);
                var entities = await this.context.OutboxNotificationEmails
                    .Where(m => m.Status == EmailStatus.Pending && m.RetryCount < maxRetries)
                    .Include(m => m.Attachments)
                    .OrderBy(m => m.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
                var messages = entities.Select(this.MapToMessage).Cast<TMessage>();
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Success(messages));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to retrieve pending messages for type {MessageType}", typeof(TMessage).Name);
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure().WithError(new Error($"Failed to retrieve pending messages: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }

    private EmailMessageEntity MapToEntity(EmailMessage message)
    {
        var entity = new EmailMessageEntity
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
            To = JsonSerializer.Serialize(message.To, DefaultSystemTextJsonSerializerOptions.Create()),
            CC = JsonSerializer.Serialize(message.CC, DefaultSystemTextJsonSerializerOptions.Create()),
            BCC = JsonSerializer.Serialize(message.BCC, DefaultSystemTextJsonSerializerOptions.Create()),
            From = JsonSerializer.Serialize(message.From, DefaultSystemTextJsonSerializerOptions.Create()),
            ReplyTo = message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultSystemTextJsonSerializerOptions.Create()),
            Headers = JsonSerializer.Serialize(message.Headers, DefaultSystemTextJsonSerializerOptions.Create()),
            PropertiesJson = JsonSerializer.Serialize(message.Properties, DefaultSystemTextJsonSerializerOptions.Create()),
            Attachments = message.Attachments.Select(a => new EmailAttachment
            {
                Id = a.Id,
                EmailMessageId = message.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Content = a.Content,
                ContentId = a.ContentId,
                IsEmbedded = a.IsEmbedded
            }).ToList()
        };
        return entity;
    }

    private void MapToEntity(EmailMessage message, EmailMessageEntity entity)
    {
        entity.Subject = message.Subject;
        entity.Body = message.Body;
        entity.IsHtml = message.IsHtml;
        entity.Priority = message.Priority;
        entity.Status = message.Status;
        entity.RetryCount = message.RetryCount;
        entity.CreatedAt = message.CreatedAt;
        entity.SentAt = message.SentAt;
        entity.To = JsonSerializer.Serialize(message.To, DefaultSystemTextJsonSerializerOptions.Create());
        entity.CC = JsonSerializer.Serialize(message.CC, DefaultSystemTextJsonSerializerOptions.Create());
        entity.BCC = JsonSerializer.Serialize(message.BCC, DefaultSystemTextJsonSerializerOptions.Create());
        entity.From = JsonSerializer.Serialize(message.From, DefaultSystemTextJsonSerializerOptions.Create());
        entity.ReplyTo = message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultSystemTextJsonSerializerOptions.Create());
        entity.Headers = JsonSerializer.Serialize(message.Headers, DefaultSystemTextJsonSerializerOptions.Create());
        entity.PropertiesJson = JsonSerializer.Serialize(message.Properties, DefaultSystemTextJsonSerializerOptions.Create());

        entity.Attachments.Clear();
        entity.Attachments.AddRange(message.Attachments.Select(a => new EmailAttachment
        {
            Id = a.Id,
            EmailMessageId = message.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            Content = a.Content,
            ContentId = a.ContentId,
            IsEmbedded = a.IsEmbedded
        }));
    }

    private EmailMessage MapToMessage(EmailMessageEntity entity)
    {
        return new EmailMessage
        {
            Id = entity.Id,
            To = JsonSerializer.Deserialize<List<string>>(entity.To, DefaultSystemTextJsonSerializerOptions.Create()),
            CC = JsonSerializer.Deserialize<List<string>>(entity.CC, DefaultSystemTextJsonSerializerOptions.Create()),
            BCC = JsonSerializer.Deserialize<List<string>>(entity.BCC, DefaultSystemTextJsonSerializerOptions.Create()),
            From = JsonSerializer.Deserialize<Application.Notifications.EmailAddress>(entity.From, DefaultSystemTextJsonSerializerOptions.Create()),
            ReplyTo = entity.ReplyTo == null ? null : JsonSerializer.Deserialize<Application.Notifications.EmailAddress>(entity.ReplyTo, DefaultSystemTextJsonSerializerOptions.Create()),
            Subject = entity.Subject,
            Body = entity.Body,
            IsHtml = entity.IsHtml,
            Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.Headers, DefaultSystemTextJsonSerializerOptions.Create()),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.PropertiesJson, DefaultSystemTextJsonSerializerOptions.Create()),
            Priority = entity.Priority,
            Status = entity.Status,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt,
            SentAt = entity.SentAt,
            Attachments = entity.Attachments.Select(a => new Application.Notifications.EmailAttachment
            {
                Id = a.Id,
                EmailMessageId = a.EmailMessageId,
                FileName = a.FileName,
                ContentType = a.ContentType,
                Content = a.Content,
                ContentId = a.ContentId,
                IsEmbedded = a.IsEmbedded
            }).ToList()
        };
    }
}