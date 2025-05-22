namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class InMemoryNotificationStorageProvider : INotificationStorageProvider
{
    private readonly ConcurrentDictionary<Guid, EmailMessage> messages;
    private readonly ILogger<InMemoryNotificationStorageProvider> logger;

    public InMemoryNotificationStorageProvider(
        ILogger<InMemoryNotificationStorageProvider> logger = null)
    {
        this.messages = new ConcurrentDictionary<Guid, EmailMessage>();
        this.logger = logger ?? NullLogger<InMemoryNotificationStorageProvider>.Instance;
    }

    public async Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                this.logger.LogInformation("Saving EmailNotificationMessage with ID {MessageId}", emailMessage.Id);
                this.messages[emailMessage.Id] = emailMessage;
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
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
                if (!this.messages.ContainsKey(emailMessage.Id))
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error($"EmailNotificationMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogInformation("Updating EmailNotificationMessage with ID {MessageId}", emailMessage.Id);
                this.messages[emailMessage.Id] = emailMessage;
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
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
                if (!this.messages.TryRemove(emailMessage.Id, out _))
                {
                    return await Task.FromResult(Result.Failure().WithError(new Error($"EmailNotificationMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogInformation("Deleting EmailNotificationMessage with ID {MessageId}", emailMessage.Id);
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
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
                this.logger.LogInformation("Retrieving up to {BatchSize} pending EmailNotificationMessages with max retries {MaxRetries}", batchSize, maxRetries);
                var pendingMessages = this.messages.Values
                    .Where(m => m.Status == EmailStatus.Pending && m.RetryCount < maxRetries)
                    .OrderBy(m => m.CreatedAt)
                    .Take(batchSize)
                    .ToList();
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Success(pendingMessages.Cast<TMessage>()));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to retrieve pending messages for type {MessageType}", typeof(TMessage).Name);
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure().WithError(new Error($"Failed to retrieve pending messages: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure().WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }
}