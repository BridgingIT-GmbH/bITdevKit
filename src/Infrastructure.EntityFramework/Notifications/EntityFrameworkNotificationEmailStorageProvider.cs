// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Notifications;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class EntityFrameworkNotificationEmailStorageProvider<TContext>(
    IServiceProvider serviceProvider,
    ILogger<EntityFrameworkNotificationEmailStorageProvider<TContext>> logger) : INotificationStorageProvider
    where TContext : DbContext, INotificationEmailContext
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<EntityFrameworkNotificationEmailStorageProvider<TContext>> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                var entity = this.MapToEntity(emailMessage);
                this.logger.LogDebug("{LogKey} storage - save email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                //this.context.ChangeTracker.Clear(); // Clear the change tracker to avoid entity already being tracked
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

    public async Task<Result> UpdateAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                var entity = await context.NotificationsEmails.AsNoTracking()
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == emailMessage.Id, cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure()
                        .WithError(new Error($"EmailMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogDebug("{LogKey} storage - update email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                this.MapToEntity(emailMessage, entity);
                //this.context.ChangeTracker.Clear(); // Clear the change tracker to avoid entity already being tracked
                context.NotificationsEmails.Update(entity);

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

    public async Task<Result> DeleteAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                using var scope = this.serviceProvider.CreateScope();
                var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                var entity = await context.NotificationsEmails
                    .FirstOrDefaultAsync(e => e.Id == emailMessage.Id, cancellationToken);
                if (entity == null)
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error($"EmailMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogDebug("{LogKey} storage - delete email message (id={MessageId})", Application.Notifications.Constants.LogKey, entity.Id);
                //this.context.ChangeTracker.Clear(); // Clear the change tracker to avoid entity already being tracked
                context.NotificationsEmails.Remove(entity);

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
                var storageProvider = scope.ServiceProvider.GetRequiredService<INotificationStorageProvider>();
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

                this.logger.LogDebug("{LogKey} storage - retrieve up to {BatchSize} pending email messages", Application.Notifications.Constants.LogKey, batchSize);
                var entities = await context.NotificationsEmails.AsNoTracking()
                    .Where(m => m.Status == EmailMessageStatus.Pending/* && m.RetryCount < maxRetries*/)
                    .Include(m => m.Attachments)
                    .OrderBy(m => m.CreatedAt)
                    .Take(batchSize).ToListAsync(cancellationToken);

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
                To = message.To == null ? null : JsonSerializer.Serialize(message.To, DefaultJsonSerializerOptions.Create()),
                CC = message.CC == null ? null : JsonSerializer.Serialize(message.CC, DefaultJsonSerializerOptions.Create()),
                BCC = message.BCC == null ? null : JsonSerializer.Serialize(message.BCC, DefaultJsonSerializerOptions.Create()),
                From = message.From == null ? null : JsonSerializer.Serialize(message.From, DefaultJsonSerializerOptions.Create()),
                ReplyTo = message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultJsonSerializerOptions.Create()),
                Headers = message.Headers == null ? null : JsonSerializer.Serialize(message.Headers, DefaultJsonSerializerOptions.Create()),
                PropertiesJson = message.Properties == null ? null : JsonSerializer.Serialize(message.Properties, DefaultJsonSerializerOptions.Create()),
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
        entity.To = message.To.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.To, DefaultJsonSerializerOptions.Create());
        entity.CC = message.CC.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.CC, DefaultJsonSerializerOptions.Create());
        entity.BCC = message.BCC.IsNullOrEmpty() ? null : JsonSerializer.Serialize(message.BCC, DefaultJsonSerializerOptions.Create());
        entity.From = message.From == null ? null : JsonSerializer.Serialize(message.From, DefaultJsonSerializerOptions.Create());
        entity.ReplyTo = message.ReplyTo == null ? null : message.ReplyTo == null ? null : JsonSerializer.Serialize(message.ReplyTo, DefaultJsonSerializerOptions.Create());
        entity.Headers = message.Headers == null ? null : JsonSerializer.Serialize(message.Headers, DefaultJsonSerializerOptions.Create());
        entity.PropertiesJson = message.Properties == null ? null : JsonSerializer.Serialize(message.Properties, DefaultJsonSerializerOptions.Create());
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
        return entity == null
            ? throw new ArgumentNullException(nameof(entity))
            : new EmailMessage
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
    }
}
