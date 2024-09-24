// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Pulsar;

using System.Buffers;
using System.Collections.Concurrent;
using Application.Messaging;
using Common;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Microsoft.Extensions.Logging;
using Constants = Application.Messaging.Constants;
using IMessage = Application.Messaging.IMessage;

/// <summary>
///     An message broker that uses Apache Pulsar to provide asynchronous messaging capabilities.
/// </summary>
public class PulsarMessageBroker : MessageBrokerBase, IAsyncDisposable
{
    private readonly PulsarMessageBrokerOptions options;
    private readonly IPulsarClient client;
    private readonly ConcurrentDictionary<string, IProducer<ReadOnlySequence<byte>>> producers = [];

    public PulsarMessageBroker(PulsarMessageBrokerOptions options)
        : base(options.LoggerFactory,
            options.HandlerFactory,
            options.Serializer,
            options.PublisherBehaviors,
            options.HandlerBehaviors)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));
        EnsureArg.IsNotNull(options.Subscription, nameof(options.Subscription));

        this.options = options;

        if (!this.options.ServiceUrl.IsNullOrEmpty())
        {
            this.client = PulsarClient
                .Builder() // https://pulsar.apache.org/docs/en/client-libraries-dotnet/#create-client
                .ServiceUrl(new Uri(this.options.ServiceUrl.EmptyToNull() ?? "pulsar://localhost:6650"))
                .KeepAliveInterval(this.options.KeepAliveInterval ?? new TimeSpan(0, 0, 30))
                .RetryInterval(this.options.RetryInterval ?? new TimeSpan(0, 0, 3))
                .Build();
        }
        else
        {
            throw new Exception(
                $"Cannot create Pulsar client, {nameof(options.ServiceUrl)} option value must be supplied.");
        }

        this.Logger.LogInformation("{LogKey} broker initialized (name={MessageBroker})",
            Constants.LogKey,
            this.GetType().Name);
    }

    public PulsarMessageBroker(Builder<PulsarMessageBrokerOptionsBuilder, PulsarMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new PulsarMessageBrokerOptionsBuilder()).Build()) { }

    public ValueTask DisposeAsync()
    {
        return this.client.DisposeAsync();
    }

    protected override async Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var topic = message.GetType().PrettyName(false);

        var producer = this.producers.GetOrAdd(topic,
            this.client.NewProducer()
                .ProducerName(this.options.Subscription)
                .Topic($"persistent://public/default/{topic}")
                .Create());

        var data = this.options.Serializer.SerializeToBytes(message);
        var metadata = new MessageMetadata { ["message_name"] = message.GetType().PrettyName(false) };
        var messageId = await producer.Send(metadata, data, cancellationToken);
        this.Logger.LogDebug(
            "{LogKey} pulsar message produced (name={MessageName}, id={MessageId}, pulsarMessageId={PulsarMessageId})",
            Constants.LogKey,
            message.GetType().PrettyName(false),
            message.MessageId,
            messageId);
    }

    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    protected override async Task OnSubscribe<TMessage, THandler>()
    {
        await this.Subscribe(typeof(TMessage), typeof(THandler));
    }

    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        var topic = messageType.PrettyName(false);

        var consumer = this.client.NewConsumer() // TODO: make private field and dispose?
            .SubscriptionName(this.options.Subscription)
            .Topic($"persistent://public/default/{topic}")
            .Create();

        this.Logger.LogDebug(
            "{LogKey} pulsar consumer created (subscription={MessageSubscriptionName}, topic={MessageTopicName})",
            Constants.LogKey,
            consumer.SubscriptionName,
            consumer.Topic);

        await foreach (var pulsarMessage in consumer.Messages())
        {
            //var messageName = pulsarMessage.Properties["message_name"];
            //var producer = pulsarMessage.Properties["producer"];
            //var customId = pulsarMessage.Properties["custom_id"];
            var data =
                this.options.Serializer.Deserialize(pulsarMessage.Data.FirstSpan.ToArray(), messageType) as IMessage;
            if (data is not null)
            {
                this.Logger.LogDebug(
                    "{LogKey} pulsar message consumed (name={MessageName}, id={MessageId}, pulsarMessageId={PulsarMessageId})",
                    Constants.LogKey,
                    data.GetType().PrettyName(false),
                    data.MessageId,
                    pulsarMessage.MessageId);

                await this.Process(new MessageRequest(data, CancellationToken.None));
            }

            await consumer.Acknowledge(pulsarMessage);
        }

        this.Logger.LogDebug(
            "{LogKey} pulsar consumer done (subscription={MessageSubscriptionName}, topic={MessageTopicName})",
            Constants.LogKey,
            consumer.SubscriptionName,
            consumer.Topic);
        // TODO: dispose consumer?
    }
}