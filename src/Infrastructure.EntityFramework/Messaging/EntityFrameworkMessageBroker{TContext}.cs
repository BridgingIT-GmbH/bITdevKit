// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Implements a durable SQL-backed <see cref="IMessageBroker"/> transport using Entity Framework persistence.
/// </summary>
/// <typeparam name="TContext">The database context type that implements <see cref="IMessagingContext"/>.</typeparam>
public class EntityFrameworkMessageBroker<TContext> : MessageBrokerBase
    where TContext : DbContext, IMessagingContext
{
    private readonly IServiceProvider serviceProvider;
    private readonly TContext context;
    private readonly EntityFrameworkMessageBrokerOptions options;

    /// <summary>
    /// Initializes a new broker instance that resolves scoped database contexts from the root service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to create scoped broker contexts.</param>
    /// <param name="options">The broker runtime options.</param>
    public EntityFrameworkMessageBroker(
        IServiceProvider serviceProvider,
        EntityFrameworkMessageBrokerOptions options)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.PublisherBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.HandlerFactory, nameof(options.HandlerFactory));

        this.serviceProvider = serviceProvider;
        this.options = options;
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Initializes a new broker instance that writes directly to a provided context.
    /// </summary>
    /// <param name="context">The database context to use for broker persistence.</param>
    /// <param name="options">The broker runtime options.</param>
    public EntityFrameworkMessageBroker(
        TContext context,
        EntityFrameworkMessageBrokerOptions options)
        : base(
            options?.LoggerFactory,
            options?.HandlerFactory,
            options?.Serializer,
            options?.PublisherBehaviors,
            options?.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.HandlerFactory, nameof(options.HandlerFactory));

        this.context = context;
        this.options = options;
        this.options.Serializer ??= new SystemTextJsonSerializer();
    }

    /// <summary>
    /// Persists the broker message and its handler snapshot during publish.
    /// </summary>
    /// <param name="message">The message being published.</param>
    /// <param name="cancellationToken">The cancellation token for the publish operation.</param>
    /// <returns>A task that completes when the durable broker row has been stored.</returns>
    protected override async Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        using var scope = this.serviceProvider?.CreateScope();
        var context = this.context ?? scope?.ServiceProvider?.GetRequiredService<TContext>();
        EnsureArg.IsNotNull(context, nameof(context));

        var brokerMessage = this.CreateBrokerMessage(message);
        context.BrokerMessages.Add(brokerMessage);

        if (this.options.AutoSave)
        {
            await context.SaveChangesAsync(cancellationToken).AnyContext();
        }
    }

    /// <summary>
    /// Processes a persisted broker row and updates its handler states.
    /// </summary>
    /// <param name="brokerMessage">The broker message row to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when processing and state mutation are done.</returns>
    public async Task ProcessStoredMessageAsync(BrokerMessage brokerMessage, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(brokerMessage, nameof(brokerMessage));

        if (this.IsExpired(brokerMessage))
        {
            this.ExpireMessage(brokerMessage);
            return;
        }

        var messageType = Type.GetType(brokerMessage.Type);
        if (messageType is null)
        {
            this.DeadLetterMessage(brokerMessage, $"message type could not be resolved ({brokerMessage.Type})");
            return;
        }

        if (this.options.Serializer.Deserialize(brokerMessage.Content, messageType) is not IMessage message)
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
                handlerState.ProcessedDate = DateTimeOffset.UtcNow;
                continue;
            }

            handlerState.AttemptCount++;
            handlerState.Status = BrokerMessageHandlerStatus.Processing;
            var request = new MessageRequest(message, cancellationToken);
            var success = await this.ProcessSubscription(request, subscription, messageTypeName);

            if (success)
            {
                handlerState.Status = BrokerMessageHandlerStatus.Succeeded;
                handlerState.LastError = null;
                handlerState.ProcessedDate = DateTimeOffset.UtcNow;
            }
            else if (handlerState.AttemptCount >= this.options.MaxDeliveryAttempts)
            {
                handlerState.Status = BrokerMessageHandlerStatus.DeadLettered;
                handlerState.LastError ??= "max delivery attempts reached";
                handlerState.ProcessedDate = DateTimeOffset.UtcNow;
            }
            else
            {
                handlerState.Status = BrokerMessageHandlerStatus.Failed;
                handlerState.LastError ??= "handler processing failed";
                handlerState.ProcessedDate = null;
            }
        }

        this.UpdateAggregateStatus(brokerMessage);
    }

    private BrokerMessage CreateBrokerMessage(IMessage message)
    {
        var createdDate = message.Timestamp == default ? DateTimeOffset.UtcNow : message.Timestamp;
        var messageType = message.GetType().PrettyName(false);
        var handlerStates = this.Subscriptions.Exists(messageType)
            ? this.Subscriptions.GetAll(messageType)
                .Select(subscription => new BrokerMessageHandlerState
                {
                    SubscriptionKey = CreateSubscriptionKey(subscription),
                    HandlerType = subscription.HandlerType?.FullName ?? subscription.HandlerType?.Name,
                    Status = BrokerMessageHandlerStatus.Pending
                })
                .ToList()
            : [];

        var status = handlerStates.Count == 0 ? BrokerMessageStatus.Succeeded : BrokerMessageStatus.Pending;

        return new BrokerMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            Type = message.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(message),
            ContentHash = HashHelper.Compute(message),
            CreatedDate = createdDate,
            ExpiresOn = this.options.MessageExpiration.HasValue ? createdDate.Add(this.options.MessageExpiration.Value) : null,
            Status = status,
            ProcessedDate = status == BrokerMessageStatus.Succeeded ? createdDate : null,
            Properties = message.Properties?.ToDictionary(item => item.Key, item => item.Value) ?? [],
            HandlerStates = handlerStates
        };
    }

    private static string CreateSubscriptionKey(SubscriptionDetails subscription)
    {
        return $"{subscription.MessageType?.PrettyName(false)}:{subscription.HandlerType?.FullName}";
    }

    private bool IsExpired(BrokerMessage brokerMessage)
    {
        return brokerMessage.ExpiresOn.HasValue && brokerMessage.ExpiresOn.Value <= DateTimeOffset.UtcNow;
    }

    private void ExpireMessage(BrokerMessage brokerMessage)
    {
        foreach (var handlerState in brokerMessage.HandlerStates.Where(state => state.Status is BrokerMessageHandlerStatus.Pending or BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.Processing))
        {
            handlerState.Status = BrokerMessageHandlerStatus.Expired;
            handlerState.LastError = "message expired before processing completed";
            handlerState.ProcessedDate = DateTimeOffset.UtcNow;
        }

        brokerMessage.Status = BrokerMessageStatus.Expired;
        brokerMessage.LastError = "message expired before processing completed";
        brokerMessage.ProcessedDate = DateTimeOffset.UtcNow;
    }

    private void DeadLetterMessage(BrokerMessage brokerMessage, string error)
    {
        foreach (var handlerState in brokerMessage.HandlerStates.Where(state => state.Status is BrokerMessageHandlerStatus.Pending or BrokerMessageHandlerStatus.Failed or BrokerMessageHandlerStatus.Processing))
        {
            handlerState.AttemptCount = Math.Max(handlerState.AttemptCount, this.options.MaxDeliveryAttempts);
            handlerState.Status = BrokerMessageHandlerStatus.DeadLettered;
            handlerState.LastError = error;
            handlerState.ProcessedDate = DateTimeOffset.UtcNow;
        }

        brokerMessage.Status = BrokerMessageStatus.DeadLettered;
        brokerMessage.LastError = error;
        brokerMessage.ProcessedDate = DateTimeOffset.UtcNow;
    }

    private void UpdateAggregateStatus(BrokerMessage brokerMessage)
    {
        if (brokerMessage.HandlerStates.Count == 0)
        {
            brokerMessage.Status = BrokerMessageStatus.Succeeded;
            brokerMessage.ProcessedDate ??= DateTimeOffset.UtcNow;
            return;
        }

        if (brokerMessage.HandlerStates.All(state => state.Status == BrokerMessageHandlerStatus.Succeeded))
        {
            brokerMessage.Status = BrokerMessageStatus.Succeeded;
            brokerMessage.LastError = null;
            brokerMessage.ProcessedDate = DateTimeOffset.UtcNow;
            return;
        }

        if (brokerMessage.HandlerStates.All(state => state.Status is BrokerMessageHandlerStatus.Succeeded or BrokerMessageHandlerStatus.DeadLettered) &&
            brokerMessage.HandlerStates.Any(state => state.Status == BrokerMessageHandlerStatus.DeadLettered))
        {
            brokerMessage.Status = BrokerMessageStatus.DeadLettered;
            brokerMessage.LastError ??= brokerMessage.HandlerStates.FirstOrDefault(state => !state.LastError.IsNullOrEmpty())?.LastError;
            brokerMessage.ProcessedDate = DateTimeOffset.UtcNow;
            return;
        }

        brokerMessage.Status = BrokerMessageStatus.Pending;
        brokerMessage.LastError = brokerMessage.HandlerStates.FirstOrDefault(state => !state.LastError.IsNullOrEmpty())?.LastError;
        brokerMessage.ProcessedDate = null;
    }
}