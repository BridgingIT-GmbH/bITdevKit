// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Captures outgoing messages in the caller's scoped database transaction.</summary>
/// <param name="worker">The private durable outbox and forwarding worker.</param>
/// <param name="session">The caller's scoped database session.</param>
/// <param name="services">The caller's scoped service provider.</param>
/// <param name="publisherBehaviors">Optional admission behaviors, or the current scope's registered behaviors.</param>
public sealed class LiteDbOutboxMessagePublisher(LiteDbOutboxMessageWorker worker, LiteDbDatabaseSession session, IServiceProvider services,
    IEnumerable<IMessagePublisherBehavior> publisherBehaviors = null)
    : MessageBrokerBase(services.GetService<ILoggerFactory>(), new ServiceProviderMessageHandlerFactory(services), worker.Serializer,
        publisherBehaviors ?? services.GetServices<IMessagePublisherBehavior>())
{
    /// <inheritdoc />
    protected override Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var envelope = new LiteDbOutboxDispatchMessage(message.MessageId, message.Timestamp)
        {
            MessageType = message.GetType().AssemblyQualifiedNameShort(), Content = worker.Serializer.SerializeToString(message)
        };
        return worker.Broker.CaptureAsync(envelope, session, services, cancellationToken);
    }
}

/// <summary>Stores a typed outgoing publication independently of the receiving broker.</summary>
public sealed class LiteDbOutboxDispatchMessage : MessageBase
{
    private LiteDbOutboxDispatchMessage() { }

    /// <summary>Creates the envelope using the original logical message identity.</summary>
    /// <param name="messageId">The outgoing logical identity.</param>
    /// <param name="timestamp">The original creation time, retained for idempotent capture.</param>
    public LiteDbOutboxDispatchMessage(string messageId, DateTimeOffset timestamp)
    {
        this.MessageId = messageId;
        this.Timestamp = timestamp;
    }

    /// <summary>Gets or sets the original message CLR type.</summary>
    public string MessageType { get; set; }

    /// <summary>Gets or sets its serialized content.</summary>
    public string Content { get; set; }
}
