// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Common;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Implements durable native queue delivery, claims, and operational queries.
/// </summary>
public sealed partial class LiteDbQueueBroker : QueueBrokerBase, IQueueBrokerService, IQueueBrokerBackgroundProcessor
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> subscribedTypes = new();
    private readonly LiteDbFeatureDatabase database;
    private readonly LiteDbQueueBrokerOptions options;
    private readonly TimeProvider clock;
    private readonly LiteDbKeyedCollection<Guid, LiteDbQueueMessage> messages;
    private readonly LiteDbKeyedCollection<string, Guid> identities;
    private readonly LiteDbKeyedCollection<string, bool> paused;

    /// <summary>Creates a native durable broker using existing publisher and handler behaviors.</summary>
    /// <param name="database">The queueing database and declared collections.</param>
    /// <param name="options">The frozen delivery settings and handler factory.</param>
    /// <param name="clock">The lease, expiry, and retry clock.</param>
    public LiteDbQueueBroker(LiteDbFeatureDatabase database, LiteDbQueueBrokerOptions options, TimeProvider clock = null)
        : base(options.LoggerFactory, options.HandlerFactory, options.Serializer, options.EnqueuerBehaviors, options.HandlerBehaviors)
    {
        this.database = database;
        this.options = options;
        this.clock = clock ?? TimeProvider.System;
        this.messages = database.Collection<Guid, LiteDbQueueMessage>("messages");
        this.identities = database.Collection<string, Guid>("identities");
        this.paused = database.Collection<string, bool>("paused");
    }

    /// <inheritdoc />
    protected override Task OnEnqueue(IQueueMessage message, CancellationToken cancellationToken) => this.database.ExecuteAsync(() =>
    {
        if (this.identities.TryGetValue(message.MessageId, out var identity))
        {
            if (this.messages[identity].ContentHash != HashHelper.Compute(this.options.Serializer.SerializeToString(message)))
            {
                throw new LiteDbProviderException(new ConflictError("A message identity already has different persisted content."));
            }

            return Task.CompletedTask;
        }

        var row = this.CreateQueueMessage(message);
        this.messages.Add(row.Id, row);
        this.identities.Add(message.MessageId, row.Id);
        return Task.CompletedTask;
    }, cancellationToken);

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
        var rows = this.database.Query<LiteDbQueueMessage>("messages", BsonExpression.Create("$.Archived = false AND $.Waiting = true AND $.Type = @0", type.AssemblyQualifiedNameShort()), this.options.BatchSize);
        foreach (var row in rows)
        {
            row.Status = QueueMessageStatus.Pending;

        }

        return Task.FromResult(rows.Count);

            }).ConfigureAwait(false);
            if (count < this.options.BatchSize) { return; }
        }
    }

    /// <summary>Delivers one claimed queue record through its existing single handler pipeline.</summary>
    /// <param name="storedMessage">The claimed record.</param>
    /// <param name="cancellationToken">Cancels external delivery.</param>
    public async Task ProcessStoredMessageAsync(LiteDbQueueMessage storedMessage, CancellationToken cancellationToken = default)
    {
        EnsureArg.IsNotNull(storedMessage, nameof(storedMessage));

        if (this.IsExpired(storedMessage))
        {
            this.ExpireMessage(storedMessage);
            return;
        }

        if (HashHelper.Compute(storedMessage.Content) != storedMessage.ContentHash)
        {
            this.DeadLetterMessage(storedMessage, "Stored message integrity check failed.");
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

        IQueueMessage message;
        try
        {
            message = this.options.Serializer.Deserialize(storedMessage.Content, subscription.MessageType) as IQueueMessage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException && !exception.IsTransientException())
        {
            this.DeadLetterMessage(storedMessage, "message content could not be deserialized");
            return;
        }

        if (message is null)
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
            storedMessage.ProcessedDate = this.clock.GetUtcNow();
        }
        else if (storedMessage.AttemptCount >= this.options.MaxDeliveryAttempts)
        {
            storedMessage.Status = QueueMessageStatus.DeadLettered;
            storedMessage.LastError ??= "max delivery attempts reached";
            storedMessage.ProcessedDate = this.clock.GetUtcNow();
        }
        else
        {
            storedMessage.Status = QueueMessageStatus.Failed;
            storedMessage.LastError ??= "handler processing failed";
            storedMessage.ProcessedDate = null;
        }
    }

    private LiteDbQueueMessage CreateQueueMessage(IQueueMessage message)
    {
        var createdDate = message.Timestamp == default ? this.clock.GetUtcNow() : message.Timestamp;

        return new LiteDbQueueMessage
        {
            Id = Guid.NewGuid(),
            MessageId = message.MessageId,
            QueueName = this.GetQueueName(message.GetType()),
            Type = message.GetType().AssemblyQualifiedNameShort(),
            Content = this.options.Serializer.SerializeToString(message),
            ContentHash = HashHelper.Compute(this.options.Serializer.SerializeToString(message)),
            CreatedDate = createdDate,
            NextAttemptUtc = createdDate > this.clock.GetUtcNow() ? createdDate : null,
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

    private bool IsExpired(LiteDbQueueMessage storedMessage)
    {
        return storedMessage.ExpiresOn.HasValue && storedMessage.ExpiresOn.Value <= this.clock.GetUtcNow();
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

    private void ExpireMessage(LiteDbQueueMessage storedMessage)
    {
        storedMessage.Status = QueueMessageStatus.Expired;
        storedMessage.LastError = "message expired before processing completed";
        storedMessage.ProcessedDate = this.clock.GetUtcNow();
    }

    private void DeadLetterMessage(LiteDbQueueMessage storedMessage, string error)
    {
        storedMessage.AttemptCount = Math.Max(storedMessage.AttemptCount, this.options.MaxDeliveryAttempts);
        storedMessage.Status = QueueMessageStatus.DeadLettered;
        storedMessage.LastError = error;
        storedMessage.ProcessedDate = this.clock.GetUtcNow();
    }

}
