// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using System.Diagnostics;
using System.Globalization;
using Application.Messaging;
using Common;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Constants = Application.Messaging.Constants;

public class RabbitMQMessageBroker : MessageBrokerBase, IDisposable
{
    private readonly RabbitMQMessageBrokerOptions options;
    private readonly ConnectionFactory factory;
    private IConnection publisherConnection;
    private IConnection subscriberConnection;
    private IModel publisherChannel;
    private IModel subscriberChannel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RabbitMQMessageBroker" /> class.
    ///     General dotnet rabbitmw docs: https://www.rabbitmq.com/dotnet-api-guide.html
    ///     <para>
    ///         Direct exchange behaving as fanout (pub/sub): https://www.rabbitmq.com/tutorials/tutorial-three-dotnet.html
    ///         Messages will be broadcasted to all the subscribers, because of the routing keys
    ///         Multiple bindings (exchange fan out):
    ///         - single exchange (messaging)
    ///         - module bound queues with single subscriber (no round robing)
    ///         - multiple routing keys, per msg name
    ///         .-----------.         .------------.
    ///         .------->| Queue 1   |-------->| Consumer 1 |
    ///         bindkey=msg1/name     | (Module1) |         |            |
    ///         /  .------>|           |         |            | single consumer
    ///         .-----------. /  /        "-----------"         "------------"
    ///         .---.       | Exchange  |/  /
    ///         |msg|---->  |           |--"
    ///         "---"       |           |\
    ///         routkey=   "-----------" \           .-----------.         .------------.
    ///         msg name     bindkey=msg2\name      | Queue 2   |-------->| Consumer 2 |
    ///         "-------->| (Module2) |         |            |
    ///         |           |         |            |--.
    ///         "-----------"         "------------"  |
    ///         | Consumer 3 | multiple consumers
    ///         |            | =round-robin
    ///         "------------"
    ///     </para>
    /// </summary>
    public RabbitMQMessageBroker(RabbitMQMessageBrokerOptions options)
        : base(options.LoggerFactory,
            options.HandlerFactory,
            options.Serializer,
            options.PublisherBehaviors = null,
            options.HandlerBehaviors = null)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));

        this.options = options;

        if (!this.options.HostName.IsNullOrEmpty())
        {
            this.factory = new ConnectionFactory
            {
                HostName = this.options.HostName, AutomaticRecoveryEnabled = true, DispatchConsumersAsync = true
            };
        }
        else if (!this.options.ConnectionString.IsNullOrEmpty())
        {
            this.factory = new ConnectionFactory
            {
                Uri = new Uri(options.ConnectionString),
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true
            };
        }
        else
        {
            throw new Exception(
                $"Cannot create RabbitMQ connection, {nameof(options.HostName)} or {nameof(options.ConnectionString)} option values must be supplied.");
        }

        this.Logger.LogInformation("{LogKey} broker initialized (name={MessageBroker})",
            Constants.LogKey,
            this.GetType().Name);
    }

    public RabbitMQMessageBroker(
        Builder<RabbitMQMessageBrokerOptionsBuilder, RabbitMQMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new RabbitMQMessageBrokerOptionsBuilder()).Build()) { }

    private string QueueName
    {
        get
        {
            var name = this.options.QueueName.IsNullOrEmpty() ? KeyGenerator.Create(22) : this.options.QueueName;

            return this.options.QueueNameSuffix.IsNullOrEmpty() ? name : $"{name}-{this.options.QueueNameSuffix}";
        }
    }

    public void Dispose()
    {
        if (this.factory is not null)
        {
            this.factory.AutomaticRecoveryEnabled = false;
        }

        this.ClosePublisherConnection();
        this.CloseSubscriberConnection();
    }

    protected override Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        this.EnsurePublisherChannel();

        var messageName = message.GetType().PrettyName(false);
        var basicProperties = this.publisherChannel.CreateBasicProperties();
        basicProperties.MessageId = message.MessageId;
        basicProperties.Type = messageName;
        basicProperties.CorrelationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey);
        basicProperties.Timestamp = new AmqpTimestamp(message.Timestamp.ToUnixTimeSeconds());

        if (this.options.IsDurable)
        {
            basicProperties.Persistent = true;
        }

        if (this.options.MessageExpiration.HasValue)
        {
            basicProperties.Expiration =
                this.options.MessageExpiration.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        }

        this.publisherChannel.BasicPublish(this.options.ExchangeName,
            messageName, // topic name
            basicProperties,
            this.options.Serializer.SerializeToBytes(message));

        this.Logger.LogDebug(
            "{LogKey} rabbitmq message produced (name={MessageName}, id={MessageId}, exchange={MessageSubscriptionName}, queue={MessageTopicName})",
            Constants.LogKey,
            messageName,
            message.MessageId,
            this.options.ExchangeName,
            this.QueueName);

        return Task.CompletedTask;
    }

    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    protected override async Task OnSubscribe<TMessage, THandler>()
    {
        await this.Subscribe(typeof(TMessage), typeof(THandler));
    }

    protected override Task OnSubscribe(Type messageType, Type handlerType)
    {
        this.EnsureSubscriberChannel();

        var queueName = this.CreateQueue(this.subscriberChannel, messageType.PrettyName(false));
        this.Logger.LogTrace("CreateQueue(queue={QueueName})", queueName);
        var consumer = new AsyncEventingBasicConsumer(this.subscriberChannel);
        consumer.Received += async (s, a) => await this.OnMessage(a, messageType);

        this.subscriberChannel.BasicConsume(queueName, true, consumer);

        return Task.CompletedTask;
    }

    private async Task OnMessage(BasicDeliverEventArgs args, Type messageType)
    {
        this.Logger.LogTrace("OnMessageAsync(messageId={MessageId})", args.BasicProperties?.MessageId);

        var messageName = args.BasicProperties.Type;
        var message = this.options.Serializer.Deserialize(args.Body.ToArray(), messageType) as IMessage;

        if (message is not null)
        {
            this.Logger.LogDebug(
                "{LogKey} rabbitmq message consumed (name={MessageName}, id={MessageId}, rabbitMQMessageId={RabbitMQMessageId})",
                Constants.LogKey,
                messageName,
                message.MessageId,
                args.BasicProperties.MessageId);

            await this.Process(new MessageRequest(message, CancellationToken.None));
        }

        this.Logger.LogDebug(
            "{LogKey} rabbitmq consumer done (exchange={MessageSubscriptionName}, queue={MessageTopicName})",
            Constants.LogKey,
            this.options.ExchangeName,
            this.QueueName);
    }

    private void CreateExchange(IModel channel)
    {
        channel.ExchangeDeclare(this.options.ExchangeName, ExchangeType.Fanout, this.options.IsDurable);
    }

    private string CreateQueue(IModel channel, string routingKey)
    {
        // declare the queue where messages will be send to, the routingkey determines which messages are received
        var result = channel.QueueDeclare(this.QueueName,
            this.options.IsDurable,
            this.options.ExclusiveQueue,
            this.options.AutoDeleteQueue);
        channel.QueueBind(result.QueueName, this.options.ExchangeName, routingKey);

        return result.QueueName;
    }

    private void EnsurePublisherChannel()
    {
        // TODO: lock?
        if (this.publisherChannel is not null)
        {
            return;
        }

        this.publisherConnection = this.factory.CreateConnection();
        this.publisherChannel = this.publisherConnection.CreateModel();

        this.publisherChannel.Close();
        this.publisherChannel.Abort();
        this.publisherChannel.Dispose();

        this.publisherConnection.Close();
        this.publisherConnection.Dispose();

        this.publisherConnection = this.factory.CreateConnection();
        this.publisherChannel = this.publisherConnection.CreateModel();
        this.CreateExchange(this.publisherChannel);

        this.Logger.LogDebug("The unique channel number for the publisher is : {ChannelNumber}",
            this.publisherChannel.ChannelNumber);
    }

    private void EnsureSubscriberChannel()
    {
        if (this.subscriberChannel is not null)
        {
            return;
        }

        this.subscriberConnection = this.factory.CreateConnection();
        this.subscriberChannel = this.subscriberConnection.CreateModel();

        this.subscriberChannel.Close();
        this.subscriberChannel.Abort();
        this.subscriberChannel.Dispose();

        this.subscriberConnection.Close();
        this.subscriberConnection.Dispose();

        this.subscriberConnection = this.factory.CreateConnection();
        this.subscriberChannel = this.subscriberConnection.CreateModel();
        this.CreateExchange(this.subscriberChannel);

        this.Logger.LogDebug("The unique channel number for the subscriber is : {ChannelNumber}",
            this.subscriberChannel.ChannelNumber);
    }

    private void ClosePublisherConnection()
    {
        if (this.publisherConnection is null)
        {
            return;
        }

        if (this.publisherChannel is not null)
        {
            this.publisherChannel.Close();
            this.publisherChannel.Abort();
            this.publisherChannel.Dispose();
            this.publisherChannel = null;
        }

        if (this.publisherConnection is not null)
        {
            this.publisherConnection.Close();
            this.publisherConnection.Dispose();
            this.publisherConnection = null;
        }
    }

    private void CloseSubscriberConnection()
    {
        if (this.subscriberConnection is null)
        {
            return;
        }

        if (this.subscriberChannel is not null)
        {
            this.subscriberChannel.Close();
            this.subscriberChannel.Abort();
            this.subscriberChannel.Dispose();
            this.subscriberChannel = null;
        }

        if (this.subscriberConnection is not null)
        {
            this.subscriberConnection.Close();
            this.subscriberConnection.Dispose();
            this.subscriberConnection = null;
        }
    }
}