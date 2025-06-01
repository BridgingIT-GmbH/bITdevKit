// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class InMemoryNotificationStorageProvider(
    ILogger<InMemoryNotificationStorageProvider> logger = null) : INotificationStorageProvider
{
    private readonly ConcurrentDictionary<Guid, EmailMessage> messages = [];
    private readonly ILogger<InMemoryNotificationStorageProvider> logger = logger ?? NullLogger<InMemoryNotificationStorageProvider>.Instance;

    public async Task<Result> SaveAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class, INotificationMessage
    {
        if (message is EmailMessage emailMessage)
        {
            try
            {
                this.logger.LogDebug("{LogKey} storage - save email message (id={MessageId})", Constants.LogKey, emailMessage.Id);
                this.messages[emailMessage.Id] = emailMessage;
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to save message (id={MessageId})", Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to save message: {ex.Message}")));
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
                if (!this.messages.ContainsKey(emailMessage.Id))
                {
                    return await Task.FromResult(Result.Failure()
                        .WithError(new Error($"EmailNotificationMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogDebug("{LogKey} storage - update email message (id={MessageId})", Constants.LogKey, emailMessage.Id);
                this.messages[emailMessage.Id] = emailMessage;
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to update message (id={MessageId})", Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure()
                    .WithError(new Error($"Failed to update message: {ex.Message}")));
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
                    return await Task.FromResult(Result.Failure()
                        .WithError(new Error($"EmailNotificationMessage with ID {emailMessage.Id} not found")));
                }

                this.logger.LogDebug("{LogKey} storage - delete email message (id={MessageId})", Constants.LogKey, emailMessage.Id);
                return await Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to delete message (id={MessageId})", Constants.LogKey, message.Id);
                return await Task.FromResult(Result.Failure().WithError(new Error($"Failed to delete message: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result.Failure()
            .WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
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
                this.logger.LogDebug("{LogKey} storage - retrieve up to {BatchSize} pending email messages", Constants.LogKey, batchSize);
                var pendingMessages = this.messages.Values
                    .Where(m => m.Status == EmailMessageStatus.Pending /*&& m.RetryCount < maxRetries*/)
                    .OrderBy(m => m.CreatedAt)
                    .Take(batchSize).ToList();
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Success(pendingMessages.Cast<TMessage>()));
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} storage - failed to retrieve pending messages for type {MessageType}", Constants.LogKey, typeof(TMessage).Name);
                return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure()
                    .WithError(new Error($"Failed to retrieve pending messages: {ex.Message}")));
            }
        }
        return await Task.FromResult(Result<IEnumerable<TMessage>>.Failure()
            .WithError(new Error($"Unsupported message type: {typeof(TMessage).Name}")));
    }
}