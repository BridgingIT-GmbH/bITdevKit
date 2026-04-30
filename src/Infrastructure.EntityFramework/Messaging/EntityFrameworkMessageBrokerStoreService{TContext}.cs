// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Provides an Entity Framework backed implementation of <see cref="IMessageBrokerService"/>.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IMessagingContext"/>.</typeparam>
public class EntityFrameworkMessageBrokerStoreService<TContext>(IServiceProvider serviceProvider, MessageBrokerControlState controlState) : IMessageBrokerService
    where TContext : DbContext, IMessagingContext
{
    /// <inheritdoc />
    public async Task<IEnumerable<BrokerMessageInfo>> GetMessagesAsync(
        BrokerMessageStatus? status = null,
        string type = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        bool includeHandlers = false,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.BrokerMessages.AsQueryable();

        query = query.WhereExpressionIf(message => message.Status == status, status.HasValue);
        query = query.WhereExpressionIf(message => message.Type == type, !type.IsNullOrEmpty());
        query = query.WhereExpressionIf(message => message.MessageId == messageId, !messageId.IsNullOrEmpty());
        query = query.WhereExpressionIf(message => message.LockedBy == lockedBy, !lockedBy.IsNullOrEmpty());
        query = query.WhereExpressionIf(message => message.IsArchived == isArchived, isArchived.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedDate >= createdAfter, createdAfter.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedDate <= createdBefore, createdBefore.HasValue);

        var messages = await query
            .OrderByDescending(message => message.CreatedDate)
            .TakeIf(take)
            .ToListAsync(cancellationToken)
            .AnyContext();

        return messages.Select(message => MapInfo(message, includeHandlers)).ToList();
    }

    /// <inheritdoc />
    public async Task<BrokerMessageInfo> GetMessageAsync(Guid id, bool includeHandlers = true, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();

        return message is null ? null : MapInfo(message, includeHandlers);
    }

    /// <inheritdoc />
    public async Task<BrokerMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();

        return message is null
            ? null
            : new BrokerMessageContentInfo
            {
                Id = message.Id,
                MessageId = message.MessageId,
                Type = message.Type,
                Content = message.Content,
                ContentHash = message.ContentHash,
                CreatedDate = message.CreatedDate,
                IsArchived = message.IsArchived
            };
    }

    /// <inheritdoc />
    public async Task<BrokerMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.BrokerMessages.AsQueryable();
        query = query.WhereExpressionIf(message => message.IsArchived == isArchived, isArchived.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedDate >= startDate || message.ProcessedDate >= startDate, startDate.HasValue);
        query = query.WhereExpressionIf(message => message.CreatedDate <= endDate || message.ProcessedDate <= endDate, endDate.HasValue);

        var messages = await query.ToListAsync(cancellationToken).AnyContext();

        return new BrokerMessageStats
        {
            Total = messages.Count,
            Pending = messages.Count(message => message.Status == BrokerMessageStatus.Pending),
            Processing = messages.Count(message => message.Status == BrokerMessageStatus.Processing),
            Succeeded = messages.Count(message => message.Status == BrokerMessageStatus.Succeeded),
            Failed = messages.Count(message => message.Status == BrokerMessageStatus.Failed),
            DeadLettered = messages.Count(message => message.Status == BrokerMessageStatus.DeadLettered),
            Expired = messages.Count(message => message.Status == BrokerMessageStatus.Expired),
            Archived = messages.Count(message => message.IsArchived),
            Leased = messages.Count(message => !message.LockedBy.IsNullOrEmpty() && message.LockedUntil.HasValue && message.LockedUntil > DateTimeOffset.UtcNow)
        };
    }

    /// <inheritdoc />
    public async Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        var hasExpiredWork = message.Status == BrokerMessageStatus.Expired ||
            message.HandlerStates.Any(handler => handler.Status == BrokerMessageHandlerStatus.Expired);

        foreach (var handler in message.HandlerStates.Where(handler => handler.Status is BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.DeadLettered or BrokerMessageHandlerStatus.Expired))
        {
            ResetHandler(handler);
        }

        if (hasExpiredWork)
        {
            RefreshExpirationForRetry(message);
        }

        message.Status = BrokerMessageStatus.Pending;
        message.LastError = null;
        message.ProcessedDate = null;
        message.IsArchived = false;
        message.ArchivedDate = null;
        message.AdvanceConcurrencyVersion();
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task RetryMessageHandlerAsync(Guid id, string handlerType, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null || handlerType.IsNullOrEmpty())
        {
            return;
        }

        var handler = message.HandlerStates.FirstOrDefault(item => string.Equals(item.HandlerType, handlerType, StringComparison.Ordinal));
        if (handler is null)
        {
            return;
        }

        if (handler.Status == BrokerMessageHandlerStatus.Expired)
        {
            RefreshExpirationForRetry(message);
        }

        ResetHandler(handler);
        message.Status = BrokerMessageStatus.Pending;
        message.LastError = null;
        message.ProcessedDate = null;
        message.IsArchived = false;
        message.ArchivedDate = null;
        message.AdvanceConcurrencyVersion();
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        message.LockedBy = null;
        message.LockedUntil = null;
        if (message.Status == BrokerMessageStatus.Processing)
        {
            message.Status = BrokerMessageStatus.Pending;
        }

        message.AdvanceConcurrencyVersion();
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var message = await context.BrokerMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        if (message.Status is not (BrokerMessageStatus.Succeeded or BrokerMessageStatus.DeadLettered or BrokerMessageStatus.Expired))
        {
            return;
        }

        message.IsArchived = true;
        message.ArchivedDate = DateTimeOffset.UtcNow;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.AdvanceConcurrencyVersion();
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<BrokerMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var query = context.BrokerMessages.AsQueryable();
        query = query.WhereExpressionIf(message => message.IsArchived == isArchived, isArchived.HasValue);

        var statusArray = statuses?.ToArray();
        query = query.WhereExpressionIf(message => statusArray.Contains(message.Status), statusArray.SafeAny());
        query = query.WhereExpressionIf(message => (message.ProcessedDate ?? message.CreatedDate) < olderThan, olderThan.HasValue);

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        context.BrokerMessages.RemoveRange(messages);
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        controlState.PauseMessageType(type);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        controlState.ResumeMessageType(type);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<BrokerMessageBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var messages = await context.BrokerMessages.Where(m => !m.IsArchived).ToListAsync(cancellationToken).AnyContext();

        return new BrokerMessageBrokerSummary
        {
            Total = messages.Count,
            Pending = messages.Count(m => m.Status == BrokerMessageStatus.Pending),
            Processing = messages.Count(m => m.Status == BrokerMessageStatus.Processing),
            Succeeded = messages.Count(m => m.Status == BrokerMessageStatus.Succeeded),
            Failed = messages.Count(m => m.Status == BrokerMessageStatus.Failed),
            DeadLettered = messages.Count(m => m.Status == BrokerMessageStatus.DeadLettered),
            Expired = messages.Count(m => m.Status == BrokerMessageStatus.Expired),
            PausedTypes = controlState.GetPausedTypes(),
            Capabilities = new BrokerMessageBrokerCapabilities
            {
                SupportsDurableStorage = true,
                SupportsRetry = true,
                SupportsArchive = true,
                SupportsLeaseManagement = true,
                SupportsPauseResume = true,
                SupportsWaitingMessageInspection = true
            }
        };
    }

    /// <inheritdoc />
    public Task<IEnumerable<BrokerMessageSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = ServiceCollectionMessagingExtensions.Subscriptions
            .Select(s => new BrokerMessageSubscriptionInfo
            {
                MessageType = s.message.PrettyName(false),
                HandlerType = s.handler.FullName,
                IsMessageTypePaused = controlState.IsMessageTypePaused(s.message.AssemblyQualifiedNameShort())
                    || controlState.IsMessageTypePaused(s.message.PrettyName(false))
            })
            .ToList();

        return Task.FromResult<IEnumerable<BrokerMessageSubscriptionInfo>>(subscriptions);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BrokerMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var messages = await context.BrokerMessages
            .Where(m => !m.IsArchived)
            .ToListAsync(cancellationToken).AnyContext();

        IEnumerable<BrokerMessage> waiting = messages
            .Where(m => m.HandlerStates.Count == 0)
            .OrderBy(m => m.CreatedDate);

        if (take.HasValue)
        {
            waiting = waiting.Take(take.Value);
        }

        return waiting.Select(m => MapInfo(m, false)).ToList();
    }

    private static BrokerMessageInfo MapInfo(BrokerMessage message, bool includeHandlers)
    {
        return new BrokerMessageInfo
        {
            Id = message.Id,
            MessageId = message.MessageId,
            Type = message.Type,
            Status = message.Status,
            IsArchived = message.IsArchived,
            ArchivedDate = message.ArchivedDate,
            CreatedDate = message.CreatedDate,
            ExpiresOn = message.ExpiresOn,
            LockedBy = message.LockedBy,
            LockedUntil = message.LockedUntil,
            ProcessedDate = message.ProcessedDate,
            AttemptCountSummary = message.HandlerStates.SafeNull().Sum(handler => handler.AttemptCount),
            LastError = message.LastError,
            Properties = message.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? [],
            Handlers = includeHandlers
                ? message.HandlerStates.SafeNull().Select(handler => new BrokerMessageHandlerInfo
                {
                    SubscriptionKey = handler.SubscriptionKey,
                    HandlerType = handler.HandlerType,
                    Status = handler.Status,
                    AttemptCount = handler.AttemptCount,
                    LastError = handler.LastError,
                    ProcessedDate = handler.ProcessedDate
                }).ToList()
                : []
        };
    }

    private static void ResetHandler(BrokerMessageHandlerState handler)
    {
        handler.Status = BrokerMessageHandlerStatus.Pending;
        handler.LastError = null;
        handler.ProcessedDate = null;
        handler.AttemptCount = 0;
    }

    private static void RefreshExpirationForRetry(BrokerMessage message)
    {
        if (!message.ExpiresOn.HasValue)
        {
            return;
        }

        var lifetime = message.ExpiresOn.Value - message.CreatedDate;
        message.ExpiresOn = lifetime > TimeSpan.Zero
            ? DateTimeOffset.UtcNow.Add(lifetime)
            : null;
    }
}