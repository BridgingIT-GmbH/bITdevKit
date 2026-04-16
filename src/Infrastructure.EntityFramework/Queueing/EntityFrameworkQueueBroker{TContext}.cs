namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Implements a durable SQL-backed <see cref="IQueueBroker"/> transport using Entity Framework persistence.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IQueueingContext"/>.</typeparam>
public class EntityFrameworkQueueBroker<TContext> : QueueBrokerBase
    where TContext : DbContext, IQueueingContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly EntityFrameworkQueueBrokerOptions options;
    private readonly QueueBrokerControlState controlState;

    /// <summary>
    /// Initializes a new broker instance that resolves scoped database contexts from the root service provider.
    /// </summary>
    public EntityFrameworkQueueBroker(
        IServiceProvider serviceProvider,
        EntityFrameworkQueueBrokerOptions options,
        QueueBrokerControlState controlState = null)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.EnqueuerBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.HandlerFactory, nameof(options.HandlerFactory));

        this.serviceProvider = serviceProvider;
        this.options = options;
        this.controlState = controlState ?? new QueueBrokerControlState();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Initializes a new broker instance that writes directly to a provided context.
    /// </summary>
    public EntityFrameworkQueueBroker(
        TContext context,
        EntityFrameworkQueueBrokerOptions options,
        QueueBrokerControlState controlState = null)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.EnqueuerBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.HandlerFactory, nameof(options.HandlerFactory));

        this.context = context;
        this.options = options;
        this.controlState = controlState ?? new QueueBrokerControlState();
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    internal async Task<QueueBrokerSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope?.ServiceProvider?.GetRequiredService<TContext>();
        EnsureArg.IsNotNull(context, nameof(context));

        var query = context.QueueMessages.AsNoTracking();
        var messages = await query.ToListAsync(cancellationToken).AnyContext();

        return new QueueBrokerSummary
        {
            Total = messages.Count,
            Pending = messages.Count(message => message.Status == QueueMessageStatus.Pending),
            WaitingForHandler = messages.Count(message => message.Status == QueueMessageStatus.WaitingForHandler),
            Processing = messages.Count(message => message.Status == QueueMessageStatus.Processing),
            Succeeded = messages.Count(message => message.Status == QueueMessageStatus.Succeeded),
            Failed = messages.Count(message => message.Status == QueueMessageStatus.Failed),
            DeadLettered = messages.Count(message => message.Status == QueueMessageStatus.DeadLettered),
            Expired = messages.Count(message => message.Status == QueueMessageStatus.Expired),
            PausedQueues = this.controlState.GetPausedQueues(),
            PausedTypes = this.controlState.GetPausedTypes(),
            Capabilities = new QueueBrokerCapabilities
            {
                SupportsDurableStorage = true,
                SupportsRetry = false,
                SupportsArchive = false,
                SupportsPauseResume = true,
                SupportsWaitingMessageInspection = true
            }
        };
    }

    internal Task<IEnumerable<QueueSubscriptionInfo>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<QueueSubscriptionInfo> result;
        result = this.GetSubscriptions()
            .Select(item => new QueueSubscriptionInfo
            {
                QueueName = this.GetQueueName(item.Value.MessageType),
                MessageType = item.Value.MessageType.PrettyName(false),
                HandlerType = item.Value.HandlerType.FullName,
                IsQueuePaused = this.controlState.IsQueuePaused(this.GetQueueName(item.Value.MessageType)),
                IsMessageTypePaused = this.controlState.IsMessageTypePaused(item.Value.MessageType.PrettyName(false))
            })
            .OrderBy(item => item.MessageType)
            .ToArray();

        return Task.FromResult(result);
    }

    internal async Task<IEnumerable<QueueMessageInfo>> GetWaitingMessagesAsync(int? take = null, CancellationToken cancellationToken = default)
    {
        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope?.ServiceProvider?.GetRequiredService<TContext>();
        EnsureArg.IsNotNull(context, nameof(context));

        IQueryable<QueueMessage> query = context.QueueMessages
            .AsNoTracking()
            .Where(message => message.Status == QueueMessageStatus.WaitingForHandler)
            .OrderBy(message => message.CreatedDate);

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        var messages = await query.ToListAsync(cancellationToken).AnyContext();

        return messages.Select(MapInfo).ToArray();
    }

    internal Task PauseQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        this.controlState.PauseQueue(queueName);

        return Task.CompletedTask;
    }

    internal Task ResumeQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        this.controlState.ResumeQueue(queueName);

        return Task.CompletedTask;
    }

    internal Task PauseMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        this.controlState.PauseMessageType(type);

        return Task.CompletedTask;
    }

    internal Task ResumeMessageTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        this.controlState.ResumeMessageType(type);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes a persisted queue row and updates its state.
    /// </summary>
    public async Task ProcessStoredMessageAsync(QueueMessage storedMessage, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(storedMessage, nameof(storedMessage));

        if (this.IsExpired(storedMessage))
        {
            this.ExpireMessage(storedMessage);
            return;
        }

        if (this.IsPaused(storedMessage))
        {
            storedMessage.Status = QueueMessageStatus.Pending;
            storedMessage.ProcessedDate = null;
            return;
        }

        var messageType = ResolveMessageType(storedMessage.Type);
        if (messageType is null)
        {
            this.DeadLetterMessage(storedMessage, $"message type could not be resolved ({storedMessage.Type})");
            return;
        }

        storedMessage.AttemptCount++;
        var subscription = this.GetSubscription(messageType);
        storedMessage.RegisteredHandlerType = subscription?.HandlerType?.FullName;

        if (subscription is null)
        {
            storedMessage.Status = QueueMessageStatus.WaitingForHandler;
            storedMessage.LastError = null;
            storedMessage.ProcessedDate = null;
            return;
        }

        if (this.options.Serializer.Deserialize(storedMessage.Content, subscription.MessageType) is not IQueueMessage message)
        {
            this.DeadLetterMessage(storedMessage, "message content could not be deserialized");
            return;
        }

        message.Properties.AddOrUpdate(storedMessage.Properties);

        var request = new QueueMessageRequest(message, cancellationToken);
        var success = await this.ProcessSubscription(request, subscription, subscription.MessageType.PrettyName(false));

        if (success)
        {
            storedMessage.Status = QueueMessageStatus.Succeeded;
            storedMessage.LastError = null;
            storedMessage.ProcessedDate = DateTimeOffset.UtcNow;
        }
        else if (storedMessage.AttemptCount >= this.options.MaxDeliveryAttempts)
        {
            storedMessage.Status = QueueMessageStatus.DeadLettered;
            storedMessage.LastError ??= "max delivery attempts reached";
            storedMessage.ProcessedDate = DateTimeOffset.UtcNow;
        }
        else
        {
            storedMessage.Status = QueueMessageStatus.Failed;
            storedMessage.LastError ??= "handler processing failed";
            storedMessage.ProcessedDate = null;
        }
    }

    /// <inheritdoc />
    protected override async Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope?.ServiceProvider?.GetRequiredService<TContext>();
        EnsureArg.IsNotNull(context, nameof(context));

        context.QueueMessages.Add(this.CreateQueueMessage(message));
        if (this.options.AutoSave)
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
    }

    /// <inheritdoc />
    public override async Task EnqueueAndWait(IQueueMessage message, CancellationToken cancellationToken = default)
    {
        await this.Enqueue(message, cancellationToken);

        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope?.ServiceProvider?.GetRequiredService<TContext>();
        EnsureArg.IsNotNull(context, nameof(context));

        if (!this.options.AutoSave)
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
    }

    private QueueMessage CreateQueueMessage(IQueueMessage message)
    {
        var createdDate = message.Timestamp == default ? DateTimeOffset.UtcNow : message.Timestamp;

        return new QueueMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            QueueName = this.GetQueueName(message.GetType()),
            Type = message.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(message),
            ContentHash = HashHelper.Compute(message),
            CreatedDate = createdDate,
            ExpiresOn = this.options.MessageExpiration.HasValue ? createdDate.Add(this.options.MessageExpiration.Value) : null,
            Status = this.GetSubscription(message.GetType()) is null ? QueueMessageStatus.WaitingForHandler : QueueMessageStatus.Pending,
            Properties = message.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? []
        };
    }

    private string GetQueueName(Type messageType)
    {
        var typeName = messageType.PrettyName(false);
        return string.Concat(this.options.QueueNamePrefix, typeName, this.options.QueueNameSuffix);
    }

    private bool IsExpired(QueueMessage storedMessage)
    {
        return storedMessage.ExpiresOn.HasValue && storedMessage.ExpiresOn.Value <= DateTimeOffset.UtcNow;
    }

    private bool IsPaused(QueueMessage storedMessage)
    {
        return this.controlState.IsQueuePaused(storedMessage.QueueName) || this.controlState.IsMessageTypePaused(ResolveMessageType(storedMessage.Type)?.PrettyName(false) ?? storedMessage.Type);
    }

    private static Type ResolveMessageType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var resolvedType = Type.GetType(typeName, throwOnError: false);
        if (resolvedType is not null)
        {
            return resolvedType;
        }

        var fullTypeName = typeName.Split(',')[0].Trim();
        if (string.IsNullOrWhiteSpace(fullTypeName))
        {
            return null;
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetType(fullTypeName, throwOnError: false))
            .FirstOrDefault(candidate => candidate is not null);
    }

    private void ExpireMessage(QueueMessage storedMessage)
    {
        storedMessage.Status = QueueMessageStatus.Expired;
        storedMessage.LastError = "message expired before processing completed";
        storedMessage.ProcessedDate = DateTimeOffset.UtcNow;
    }

    private void DeadLetterMessage(QueueMessage storedMessage, string error)
    {
        storedMessage.AttemptCount = Math.Max(storedMessage.AttemptCount, this.options.MaxDeliveryAttempts);
        storedMessage.Status = QueueMessageStatus.DeadLettered;
        storedMessage.LastError = error;
        storedMessage.ProcessedDate = DateTimeOffset.UtcNow;
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
            Status = message.Status,
            AttemptCount = message.AttemptCount,
            CreatedDate = message.CreatedDate,
            ProcessedDate = message.ProcessedDate,
            LastError = message.LastError
        };
    }
}