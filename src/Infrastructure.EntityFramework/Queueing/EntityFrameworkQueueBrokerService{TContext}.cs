namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides operational access to the Entity Framework queue broker.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IQueueingContext"/>.</typeparam>
public class EntityFrameworkQueueBrokerService<TContext> : IQueueBrokerService
    where TContext : DbContext, IQueueingContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly EntityFrameworkQueueBrokerOptions options;
    private readonly QueueingRegistrationStore registrationStore;
    private readonly QueueBrokerControlState controlState;

    /// <summary>
    /// Initializes a new operational service instance that resolves database contexts from the root service provider.
    /// </summary>
    public EntityFrameworkQueueBrokerService(
        IServiceProvider serviceProvider,
        EntityFrameworkQueueBrokerOptions options,
        QueueingRegistrationStore registrationStore,
        QueueBrokerControlState controlState)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        this.controlState = controlState ?? throw new ArgumentNullException(nameof(controlState));
    }

    /// <summary>
    /// Initializes a new operational service instance that uses the provided database context.
    /// </summary>
    public EntityFrameworkQueueBrokerService(
        TContext context,
        EntityFrameworkQueueBrokerOptions options,
        QueueingRegistrationStore registrationStore,
        QueueBrokerControlState controlState)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.registrationStore = registrationStore ?? throw new ArgumentNullException(nameof(registrationStore));
        this.controlState = controlState ?? throw new ArgumentNullException(nameof(controlState));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QueueMessageInfo>> GetMessagesAsync(
        QueueMessageStatus? status = null,
        string type = null,
        string queueName = null,
        string messageId = null,
        string lockedBy = null,
        bool? isArchived = false,
        DateTimeOffset? createdAfter = null,
        DateTimeOffset? createdBefore = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);

        IQueryable<QueueMessage> query = context.QueueMessages.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(message => message.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(message => message.Type.Contains(type));
        }

        if (!string.IsNullOrWhiteSpace(queueName))
        {
            query = query.Where(message => message.QueueName.Contains(queueName));
        }

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            query = query.Where(message => message.MessageId.Contains(messageId));
        }

        if (!string.IsNullOrWhiteSpace(lockedBy))
        {
            query = query.Where(message => message.LockedBy == lockedBy);
        }

        if (isArchived.HasValue)
        {
            query = query.Where(message => message.IsArchived == isArchived.Value);
        }

        if (createdAfter.HasValue)
        {
            query = query.Where(message => message.CreatedDate >= createdAfter.Value);
        }

        if (createdBefore.HasValue)
        {
            query = query.Where(message => message.CreatedDate <= createdBefore.Value);
        }

        query = query.OrderByDescending(message => message.CreatedDate);

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        return messages.Select(MapInfo).ToArray();
    }

    /// <inheritdoc />
    public async Task<QueueMessageInfo> GetMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);
        var message = await context.QueueMessages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        return message is null ? null : MapInfo(message);
    }

    /// <inheritdoc />
    public async Task<QueueMessageContentInfo> GetMessageContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);
        var message = await context.QueueMessages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        return message is null
            ? null
            : new QueueMessageContentInfo
            {
                Id = message.Id,
                MessageId = message.MessageId,
                QueueName = message.QueueName,
                Type = message.Type,
                Content = message.Content,
                ContentHash = message.ContentHash,
                CreatedDate = message.CreatedDate,
                IsArchived = message.IsArchived,
                ArchivedDate = message.ArchivedDate
            };
    }

    /// <inheritdoc />
    public async Task<QueueMessageStats> GetMessageStatsAsync(
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        bool? isArchived = false,
        CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);

        IQueryable<QueueMessage> query = context.QueueMessages.AsNoTracking();

        if (isArchived.HasValue)
        {
            query = query.Where(message => message.IsArchived == isArchived.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(message => (message.ProcessedDate ?? message.CreatedDate) >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(message => (message.ProcessedDate ?? message.CreatedDate) <= endDate.Value);
        }

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        var now = DateTimeOffset.UtcNow;

        return new QueueMessageStats
        {
            Total = messages.Count,
            Pending = messages.Count(message => message.Status == QueueMessageStatus.Pending),
            WaitingForHandler = messages.Count(message => message.Status == QueueMessageStatus.WaitingForHandler),
            Processing = messages.Count(message => message.Status == QueueMessageStatus.Processing),
            Succeeded = messages.Count(message => message.Status == QueueMessageStatus.Succeeded),
            Failed = messages.Count(message => message.Status == QueueMessageStatus.Failed),
            DeadLettered = messages.Count(message => message.Status == QueueMessageStatus.DeadLettered),
            Expired = messages.Count(message => message.Status == QueueMessageStatus.Expired),
            Archived = messages.Count(message => message.IsArchived),
            Leased = messages.Count(message => !string.IsNullOrWhiteSpace(message.LockedBy) && message.LockedUntil > now),
            PausedQueues = this.controlState.GetPausedQueues(),
            PausedTypes = this.controlState.GetPausedTypes(),
            OpenCircuits = []
        };
    }

    /// <inheritdoc />
    public async Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var stats = await this.GetMessageStatsAsync(cancellationToken: cancellationToken);

        return new QueueBrokerSummary
        {
            Total = stats.Total,
            Pending = stats.Pending,
            WaitingForHandler = stats.WaitingForHandler,
            Processing = stats.Processing,
            Succeeded = stats.Succeeded,
            Failed = stats.Failed,
            DeadLettered = stats.DeadLettered,
            Expired = stats.Expired,
            PausedQueues = stats.PausedQueues,
            PausedTypes = stats.PausedTypes,
            Capabilities = new QueueBrokerCapabilities
            {
                SupportsDurableStorage = true,
                SupportsRetry = true,
                SupportsArchive = true,
                SupportsLeaseManagement = true,
                SupportsCircuitManagement = false,
                SupportsPauseResume = true,
                SupportsWaitingMessageInspection = true
            }
        };
    }

    /// <inheritdoc />
    public Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<QueueSubscriptionInfo> result = this.registrationStore.Subscriptions
            .Select(item => new QueueSubscriptionInfo
            {
                QueueName = this.GetQueueName(item.MessageType),
                MessageType = item.MessageType.PrettyName(false),
                HandlerType = item.HandlerType.FullName,
                IsQueuePaused = this.controlState.IsQueuePaused(this.GetQueueName(item.MessageType)),
                IsMessageTypePaused = this.controlState.IsMessageTypePaused(item.MessageType.PrettyName(false))
            })
            .OrderBy(item => item.MessageType)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);

        IQueryable<QueueMessage> query = context.QueueMessages
            .AsNoTracking()
            .Where(message => !message.IsArchived && message.Status == QueueMessageStatus.WaitingForHandler)
            .OrderBy(message => message.CreatedDate);

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        return messages.Select(MapInfo).ToArray();
    }

    /// <inheritdoc />
    public async Task RetryMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);
        var message = await context.QueueMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        message.Status = QueueMessageStatus.Pending;
        message.AttemptCount = 0;
        message.RegisteredHandlerType = null;
        message.LastError = null;
        message.ProcessedDate = null;
        message.ProcessingStartedDate = null;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.IsArchived = false;
        message.ArchivedDate = null;
        message.AdvanceConcurrencyVersion();

        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task ReleaseLeaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);
        var message = await context.QueueMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        message.LockedBy = null;
        message.LockedUntil = null;
        if (message.Status == QueueMessageStatus.Processing)
        {
            message.Status = QueueMessageStatus.Pending;
            message.ProcessingStartedDate = null;
        }

        message.AdvanceConcurrencyVersion();
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.PauseMessageType(type);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.ResumeMessageType(type);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResetMessageTypeCircuitAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ArchiveMessageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);
        var message = await context.QueueMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).AnyContext();
        if (message is null)
        {
            return;
        }

        message.IsArchived = true;
        message.ArchivedDate ??= DateTimeOffset.UtcNow;
        message.AdvanceConcurrencyVersion();

        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public async Task PurgeMessagesAsync(
        DateTimeOffset? olderThan = null,
        IEnumerable<QueueMessageStatus> statuses = null,
        bool? isArchived = null,
        CancellationToken cancellationToken = default)
    {
        using var lease = this.CreateContextLease(out var context);

        IQueryable<QueueMessage> query = context.QueueMessages;

        if (olderThan.HasValue)
        {
            query = query.Where(message => message.CreatedDate <= olderThan.Value);
        }

        var statusList = statuses?.ToArray();
        if (statusList?.Length > 0)
        {
            query = query.Where(message => statusList.Contains(message.Status));
        }

        if (isArchived.HasValue)
        {
            query = query.Where(message => message.IsArchived == isArchived.Value);
        }

        var messages = await query.ToListAsync(cancellationToken).AnyContext();
        context.QueueMessages.RemoveRange(messages);
        await context.SaveChangesAsync(cancellationToken).AnyContext();
    }

    /// <inheritdoc />
    public Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.PauseQueue(queueName);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.controlState.ResumeQueue(queueName);

        return Task.CompletedTask;
    }

    private string GetQueueName(Type messageType)
    {
        var typeName = messageType.PrettyName(false);
        return string.Concat(this.options.QueueNamePrefix, typeName, this.options.QueueNameSuffix);
    }

    private IDisposable CreateContextLease(out TContext dbContext)
    {
        if (this.serviceProvider is not null)
        {
            var scope = this.serviceProvider.CreateScope();
            dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            return scope;
        }

        dbContext = this.context;
        return NoopDisposable.Instance;
    }

    private static QueueMessageInfo MapInfo(QueueMessage message)
    {
        return new QueueMessageInfo
        {
            Id = message.Id,
            MessageId = message.MessageId,
            QueueName = message.QueueName,
            Type = message.Type,
            RegisteredHandlerType = message.RegisteredHandlerType,
            IsArchived = message.IsArchived,
            ArchivedDate = message.ArchivedDate,
            Status = message.Status,
            AttemptCount = message.AttemptCount,
            CreatedDate = message.CreatedDate,
            ExpiresOn = message.ExpiresOn,
            LockedBy = message.LockedBy,
            LockedUntil = message.LockedUntil,
            ProcessingStartedDate = message.ProcessingStartedDate,
            ProcessedDate = message.ProcessedDate,
            LastError = message.LastError,
            Properties = message.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? []
        };
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}