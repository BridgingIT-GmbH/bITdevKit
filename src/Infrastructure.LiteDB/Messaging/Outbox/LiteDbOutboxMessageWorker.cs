// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Forwards committed publications through an independently selected broker using the native fenced delivery protocol.</summary>
/// <param name="broker">The private durable envelope broker.</param>
/// <param name="serializer">The original message serializer.</param>
public sealed class LiteDbOutboxMessageWorker(LiteDbMessageBroker broker, ISerializer serializer) : IOutboxMessageWorker
{
    /// <summary>Gets the private outbox broker owning durable claims and per-envelope progress.</summary>
    public LiteDbMessageBroker Broker { get; } = broker;

    /// <summary>Gets the original message serializer.</summary>
    public ISerializer Serializer { get; } = serializer;

    /// <inheritdoc />
    public Task ProcessAsync(string messageId = null, CancellationToken cancellationToken = default) => this.Broker.ProcessPendingAsync(cancellationToken, messageId);

    /// <inheritdoc />
    public Task PurgeAsync(bool processedOnly = false, CancellationToken cancellationToken = default) => this.Broker.PurgeMessagesAsync(
        statuses: processedOnly ? [BrokerMessageStatus.Succeeded, BrokerMessageStatus.DeadLettered, BrokerMessageStatus.Expired] : null, cancellationToken: cancellationToken);
}

/// <summary>Publishes one restored outgoing message to the configured downstream broker.</summary>
/// <param name="services">The current forwarding scope.</param>
/// <param name="serializer">The configured original message serializer.</param>
public sealed class LiteDbOutboxMessageForwarder(IServiceProvider services, ISerializer serializer) : IMessageHandler<LiteDbOutboxDispatchMessage>
{
    /// <summary>Gets the private DI key preserving the independently selected downstream broker.</summary>
    public const string DeliveryBrokerKey = "LiteDB.Outbox.DeliveryBroker";

    /// <inheritdoc />
    public Task Handle(LiteDbOutboxDispatchMessage message, CancellationToken cancellationToken)
    {
        var type = Type.GetType(message.MessageType, true)!;
        var restored = serializer.Deserialize(message.Content, type) as IMessage ?? throw new InvalidOperationException("The outgoing payload is not a message.");
        if (restored.MessageId != message.MessageId) { throw new InvalidOperationException("The outgoing envelope identity does not match its content."); }

        var broker = services.GetRequiredKeyedService<IMessageBroker>(DeliveryBrokerKey);
        var auditing = services.GetServices<IMessagePublisherBehavior>().OfType<AuditingMessagePublisherBehavior>().FirstOrDefault();
        return auditing is null ? broker.Publish(restored, cancellationToken)
            : auditing.PublishDeferredAsync(restored, () => broker.Publish(restored, cancellationToken));
    }
}
