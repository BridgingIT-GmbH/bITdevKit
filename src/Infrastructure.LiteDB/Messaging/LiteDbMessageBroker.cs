// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Implements durable native message publication, fan-out progress, claims, and operational queries.
/// </summary>
public sealed partial class LiteDbMessageBroker : MessageBrokerBase, IMessageBrokerService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> subscribedTypes = new();
    private readonly LiteDbFeatureDatabase database;
    private readonly LiteDbMessageBrokerOptions options;
    private readonly TimeProvider clock;
    private readonly LiteDbKeyedCollection<Guid, LiteDbBrokerMessage> messages;
    private readonly LiteDbKeyedCollection<string, Guid> identities;
    private readonly LiteDbKeyedCollection<string, bool> paused;

    /// <summary>Creates a native durable broker using existing publisher and handler behaviors.</summary>
    /// <param name="database">The messaging database and declared collections.</param>
    /// <param name="options">The frozen delivery settings and handler factory.</param>
    /// <param name="clock">The lease, expiry, and retry clock.</param>
    public LiteDbMessageBroker(LiteDbFeatureDatabase database, LiteDbMessageBrokerOptions options, TimeProvider clock = null)
        : base(options.LoggerFactory, options.HandlerFactory, options.Serializer, options.PublisherBehaviors, options.HandlerBehaviors)
    {
        this.database = database;
        this.options = options;
        this.clock = clock ?? TimeProvider.System;
        this.messages = database.Collection<Guid, LiteDbBrokerMessage>("messages");
        this.identities = database.Collection<string, Guid>("identities");
        this.paused = database.Collection<string, bool>("paused");
    }

    /// <inheritdoc />
    protected override Task OnPublish(IMessage message, CancellationToken cancellationToken) => this.database.ExecuteAsync(() => this.StoreMessage(message), cancellationToken);

    /// <summary>Captures a publication inside the caller's explicit entity transaction.</summary>
    /// <param name="message">The validated message to capture.</param>
    /// <param name="session">The caller's database session.</param>
    /// <param name="services">The caller's scoped services.</param>
    /// <param name="cancellationToken">Cancels capture.</param>
    /// <returns>The native capture task.</returns>
    public Task CaptureAsync(IMessage message, LiteDbDatabaseSession session, IServiceProvider services, CancellationToken cancellationToken = default) =>
        this.database.ExecuteInSessionAsync(session, services, () => { this.StoreMessage(message).GetAwaiter().GetResult(); return Task.FromResult(true); }, cancellationToken);

    /// <summary>Installs a fixed outbox forwarding subscription before the private broker accepts its first message.</summary>
    /// <typeparam name="TMessage">The private durable envelope type.</typeparam>
    /// <typeparam name="THandler">The forwarding handler type.</typeparam>
    public void RegisterOutboxHandler<TMessage, THandler>() where TMessage : IMessage where THandler : IMessageHandler<TMessage>
    {
        this.Subscriptions.Add<TMessage, THandler>();
        this.subscribedTypes[typeof(TMessage).AssemblyQualifiedNameShort()] = typeof(TMessage);
    }

    private Task StoreMessage(IMessage message)
    {
        if (this.identities.TryGetValue(message.MessageId, out var identity))
        {
            if (this.messages[identity].ContentHash != HashHelper.Compute(this.options.Serializer.SerializeToString(message)))
            {
                throw new LiteDbProviderException(new ConflictError("A message identity already has different persisted content."));
            }

            return Task.CompletedTask;
        }

        var row = this.CreateBrokerMessage(message);
        this.messages.Add(row.Id, row);
        this.identities.Add(message.MessageId, row.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task OnSubscribe<TMessage, THandler>() => this.ActivateWaitingAsync(typeof(TMessage));

    /// <inheritdoc />
    protected override Task OnSubscribe(Type messageType, Type handlerType) => this.ActivateWaitingAsync(messageType);

    private async Task ActivateWaitingAsync(Type type)
    {
        while (true)
        {
            var count = await this.database.ExecuteAsync(() =>
            {
        this.subscribedTypes[type.AssemblyQualifiedNameShort()] = type;
        var rows = this.database.Query<LiteDbBrokerMessage>("messages", BsonExpression.Create("$.Archived = false AND $.Waiting = true AND $.Type = @0", type.AssemblyQualifiedNameShort()), this.options.BatchSize);
        foreach (var row in rows)
        {
            var name = type.PrettyName(false);
            row.HandlerStates = this.Subscriptions.GetAll(name).Select(subscription => new LiteDbBrokerMessageHandlerState
            {
                SubscriptionKey = CreateSubscriptionKey(subscription), HandlerType = subscription.HandlerType.FullName, Status = BrokerMessageHandlerStatus.Pending
            }).ToList();
            row.WaitingForHandler = row.HandlerStates.Count == 0;
        }

        return Task.FromResult(rows.Count);

            }).ConfigureAwait(false);
            if (count < this.options.BatchSize) { return; }
        }
    }

    /// <summary>
    /// Processes a persisted broker row and updates its handler states.
    /// </summary>
    /// <param name="brokerMessage">The broker message row to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="persistProgress">Fenced persistence after each completed handler.</param>
    /// <returns>A task that completes when processing and state mutation are done.</returns>
    public async Task ProcessStoredMessageAsync(LiteDbBrokerMessage brokerMessage, CancellationToken cancellationToken = default, Func<Task> persistProgress = null)
    {
        EnsureArg.IsNotNull(brokerMessage, nameof(brokerMessage));

        if (this.IsExpired(brokerMessage))
        {
            this.ExpireMessage(brokerMessage);
            return;
        }

        if (HashHelper.Compute(brokerMessage.Content) != brokerMessage.ContentHash) { this.DeadLetterMessage(brokerMessage, "Stored message integrity check failed."); return; }

        var messageType = Type.GetType(brokerMessage.Type);
        if (messageType is null)
        {
            this.DeadLetterMessage(brokerMessage, $"message type could not be resolved ({brokerMessage.Type})");
            return;
        }

        IMessage message;
        try
        {
            message = this.options.Serializer.Deserialize(brokerMessage.Content, messageType) as IMessage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException && !exception.IsTransientException())
        {
            this.DeadLetterMessage(brokerMessage, "message content could not be deserialized");
            return;
        }

        if (message is null)
        {
            this.DeadLetterMessage(brokerMessage, "message content could not be deserialized");
            return;
        }

        message.Properties.AddOrUpdate(brokerMessage.Properties);
        var messageTypeName = message.GetType().PrettyName(false);

        foreach (var handlerState in brokerMessage.HandlerStates
                     .Where(state => state.Status is BrokerMessageHandlerStatus.Pending or BrokerMessageHandlerStatus.Failed)
                     .OrderBy(state => state.HandlerType)
                     .ToList())
        {
            var subscription = this.Subscriptions.Exists(messageTypeName)
                ? this.Subscriptions.GetAll(messageTypeName)
                    .FirstOrDefault(candidate => string.Equals(candidate.HandlerType?.FullName, handlerState.HandlerType, StringComparison.Ordinal))
                : null;

            if (subscription is null)
            {
                handlerState.AttemptCount++;
                handlerState.Status = BrokerMessageHandlerStatus.DeadLettered;
                handlerState.LastError = $"subscription not found for handler '{handlerState.HandlerType}'";
                handlerState.ProcessedDate = this.clock.GetUtcNow();
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            handlerState.AttemptCount++;
            handlerState.Status = BrokerMessageHandlerStatus.Processing;
            var request = new MessageRequest(message, cancellationToken);
            var success = await this.ProcessSubscription(request, subscription, messageTypeName);

            if (success)
            {
                handlerState.Status = BrokerMessageHandlerStatus.Succeeded;
                handlerState.LastError = null;
                handlerState.ProcessedDate = this.clock.GetUtcNow();
            }
            else if (handlerState.AttemptCount >= this.options.MaxDeliveryAttempts)
            {
                handlerState.Status = BrokerMessageHandlerStatus.DeadLettered;
                handlerState.LastError ??= "max delivery attempts reached";
                handlerState.ProcessedDate = this.clock.GetUtcNow();
            }
            else
            {
                handlerState.Status = BrokerMessageHandlerStatus.Failed;
                handlerState.LastError ??= "handler processing failed";
                handlerState.ProcessedDate = null;
            }

            if (persistProgress is not null) { await persistProgress().ConfigureAwait(false); }
        }

        this.UpdateAggregateStatus(brokerMessage);
    }

    private LiteDbBrokerMessage CreateBrokerMessage(IMessage message)
    {
        var createdDate = message.Timestamp == default ? this.clock.GetUtcNow() : message.Timestamp;
        var messageType = message.GetType().PrettyName(false);
        var handlerStates = this.Subscriptions.Exists(messageType)
            ? this.Subscriptions.GetAll(messageType)
                .Select(subscription => new LiteDbBrokerMessageHandlerState
                {
                    SubscriptionKey = CreateSubscriptionKey(subscription),
                    HandlerType = subscription.HandlerType?.FullName ?? subscription.HandlerType?.Name,
                    Status = BrokerMessageHandlerStatus.Pending
                })
                .ToList()
            : [];

        var status = BrokerMessageStatus.Pending;

        return new LiteDbBrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            Type = message.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(message),
            ContentHash = HashHelper.Compute(this.options.Serializer.SerializeToString(message)),
            CreatedDate = createdDate,
            ExpiresOn = this.options.MessageExpiration.HasValue ? createdDate.Add(this.options.MessageExpiration.Value) : null,
            Status = status,
            ProcessedDate = null,
            WaitingForHandler = handlerStates.Count == 0,
            Properties = message.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? [],
            HandlerStates = handlerStates
        };
    }

    private static string CreateSubscriptionKey(SubscriptionDetails subscription)
    {
        return $"{subscription.MessageType?.PrettyName(false)}:{subscription.HandlerType?.FullName}";
    }

    private bool IsExpired(LiteDbBrokerMessage brokerMessage)
    {
        return brokerMessage.ExpiresOn.HasValue && brokerMessage.ExpiresOn.Value <= this.clock.GetUtcNow();
    }

    private void ExpireMessage(LiteDbBrokerMessage brokerMessage)
    {
        foreach (var handlerState in brokerMessage.HandlerStates.Where(state => state.Status is BrokerMessageHandlerStatus.Pending or BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.Processing))
        {
            handlerState.Status = BrokerMessageHandlerStatus.Expired;
            handlerState.LastError = "message expired before processing completed";
            handlerState.ProcessedDate = this.clock.GetUtcNow();
        }

        brokerMessage.Status = BrokerMessageStatus.Expired;
        brokerMessage.LastError = "message expired before processing completed";
        brokerMessage.ProcessedDate = this.clock.GetUtcNow();
    }

    private void DeadLetterMessage(LiteDbBrokerMessage brokerMessage, string error)
    {
        foreach (var handlerState in brokerMessage.HandlerStates.Where(state => state.Status is BrokerMessageHandlerStatus.Pending or BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.Processing))
        {
            handlerState.AttemptCount = Math.Max(handlerState.AttemptCount, this.options.MaxDeliveryAttempts);
            handlerState.Status = BrokerMessageHandlerStatus.DeadLettered;
            handlerState.LastError = error;
            handlerState.ProcessedDate = this.clock.GetUtcNow();
        }

        brokerMessage.Status = BrokerMessageStatus.DeadLettered;
        brokerMessage.LastError = error;
        brokerMessage.ProcessedDate = this.clock.GetUtcNow();
    }

    private void UpdateAggregateStatus(LiteDbBrokerMessage brokerMessage)
    {
        if (brokerMessage.HandlerStates.Count == 0)
        {
            brokerMessage.Status = BrokerMessageStatus.Succeeded;
            brokerMessage.ProcessedDate ??= this.clock.GetUtcNow();
            return;
        }

        if (brokerMessage.HandlerStates.All(state => state.Status == BrokerMessageHandlerStatus.Succeeded))
        {
            brokerMessage.Status = BrokerMessageStatus.Succeeded;
            brokerMessage.LastError = null;
            brokerMessage.ProcessedDate = this.clock.GetUtcNow();
            return;
        }

        if (brokerMessage.HandlerStates.All(state => state.Status is BrokerMessageHandlerStatus.Succeeded or BrokerMessageHandlerStatus.DeadLettered) &&
            brokerMessage.HandlerStates.Any(state => state.Status == BrokerMessageHandlerStatus.DeadLettered))
        {
            brokerMessage.Status = BrokerMessageStatus.DeadLettered;
            brokerMessage.LastError ??= brokerMessage.HandlerStates.FirstOrDefault(state => !state.LastError.IsNullOrEmpty())?.LastError;
            brokerMessage.ProcessedDate = this.clock.GetUtcNow();
            return;
        }

        brokerMessage.Status = BrokerMessageStatus.Pending;
        brokerMessage.LastError = brokerMessage.HandlerStates.FirstOrDefault(state => !state.LastError.IsNullOrEmpty())?.LastError;
        brokerMessage.ProcessedDate = null;
    }
}
