// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using System.Diagnostics;
using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using global::Azure.Messaging.ServiceBus;
using global::Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

public class ServiceBusMessageBroker : MessageBrokerBase, IDisposable, IAsyncDisposable
{
    private readonly ServiceBusMessageBrokerOptions options;
    private readonly ServiceBusClient client;
    private readonly ServiceBusAdministrationClient managementClient;
    private readonly IDictionary<string, ServiceBusProcessor> processors = new Dictionary<string, ServiceBusProcessor>();

    public ServiceBusMessageBroker(ServiceBusMessageBrokerOptions options)
        : base(options.LoggerFactory, options.HandlerFactory, options.Serializer, options.PublisherBehaviors = null, options.HandlerBehaviors = null)
    {
        EnsureArg.IsNotNull(options, nameof(options));
        EnsureArg.IsNotNull(options.Serializer, nameof(options.Serializer));

        this.options = options;

        if (!this.options.ConnectionString.IsNullOrEmpty())
        {
            this.client = new ServiceBusClient(this.options.ConnectionString);
            this.managementClient = new ServiceBusAdministrationClient(this.options.ConnectionString, new ServiceBusAdministrationClientOptions());
        }
        else
        {
            throw new Exception($"Cannot create ServiceBus connection, {nameof(options.ConnectionString)} option value must be supplied.");
        }

        this.Logger.LogInformation("{LogKey} broker initialized (name={MessageBroker})", Constants.LogKey, this.GetType().Name);
    }

    public ServiceBusMessageBroker(Builder<ServiceBusMessageBrokerOptionsBuilder, ServiceBusMessageBrokerOptions> optionsBuilder)
        : this(optionsBuilder(new ServiceBusMessageBrokerOptionsBuilder()).Build())
    {
    }

    public void Dispose()
    {
#pragma warning disable CA2012 // Use ValueTasks correctly
        this.DisposeAsync().GetAwaiter().GetResult();
#pragma warning restore CA2012 // Use ValueTasks correctly
    }

    public async ValueTask DisposeAsync()
    {
        if (this.processors is not null)
        {
            foreach (var processor in this.processors.Values)
            {
                await processor.DisposeAsync().AnyContext();
            }
        }

        if (this.client is not null)
        {
            await this.client.DisposeAsync().AnyContext();
        }
    }

    protected override async Task OnPublish(IMessage message, CancellationToken cancellationToken)
    {
        var messageName = message.GetType().PrettyName(false); // = topic name
        var topicName = $"{messageName}_{this.options.TopicScope}".Trim('_').ToLowerInvariant();
        await this.EnsureTopicAsync(topicName).AnyContext();

        var serviceBusMessage = new ServiceBusMessage(this.options.Serializer.SerializeToBytes(message))
        {
            MessageId = message.MessageId,
            Subject = messageName,
            CorrelationId = Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey),
            TimeToLive = this.options.MessageExpiration ?? new TimeSpan(0, 59, 59),
            ApplicationProperties =
            {
                //{ "MessageName", messageName },
            },
        };

        await using (var sender = this.client.CreateSender(topicName))
        {
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken).AnyContext();
        }

        this.Logger.LogDebug("{LogKey} servicebus message produced (name={MessageName}, id={MessageId}, topic={MessageTopicName})", Constants.LogKey, messageName, message.MessageId, topicName);
    }

    protected override async Task OnProcess(IMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(this.options.ProcessDelay, cancellationToken);
    }

    protected override async Task OnSubscribe<TMessage, THandler>()
    {
        await this.OnSubscribe(typeof(TMessage), typeof(THandler));
    }

    protected override async Task OnSubscribe(Type messageType, Type handlerType)
    {
        var messageName = messageType.PrettyName(false); // = topic name
        var topicName = $"{messageName}_{this.options.TopicScope}".Trim('_').ToLowerInvariant();
        var subscriptionName = messageName.ToLowerInvariant();

        await this.EnsureTopicAsync(topicName).AnyContext();
        await this.EnsureSubscriptionAsync(topicName, subscriptionName).AnyContext();

        if (!this.processors.ContainsKey(messageName)) // TODO: lock here?
        {
            var processor = this.client.CreateProcessor(topicName, subscriptionName);

            processor.ProcessMessageAsync += async (a) =>
            {
                try
                {
                    await this.OnMessage(a, messageType);
                    await a.CompleteMessageAsync(a.Message);
                }
                catch (Exception)
                {
                    this.Logger.LogError("{LogKey} servicebus message consume failed (name={MessageName}, id={MessageId}, path={ServiceBusEntityPath})", Constants.LogKey, a.Message.Subject, a.Message.MessageId, a.EntityPath);

                    await a.AbandonMessageAsync(a.Message);
                }
            };

            processor.ProcessErrorAsync += (a) =>
            {
                this.Logger.LogError(a.Exception, "{LogKey} servicebus failed (path={ServiceBusEntityPath}) {ErrorMessage}", Constants.LogKey, a.EntityPath, a.Exception.Message);
                return Task.CompletedTask;
            };

            this.processors.Add(messageName, processor);
            await processor.StartProcessingAsync();
        }
    }

    private async Task OnMessage(ProcessMessageEventArgs args, Type messageType)
    {
        this.Logger.LogTrace("OnMessageAsync(messageId={MessageId})", args.Message.MessageId);

        var messageName = args.Message.Subject;
        var message = this.options.Serializer.Deserialize(args.Message.Body.ToArray(), messageType) as IMessage;

        if (message is not null)
        {
            this.Logger.LogDebug("{LogKey} servicebus message consumed (name={MessageName}, id={MessageId}, path={ServiceBusEntityPath})", Constants.LogKey, messageName, message.MessageId, args.EntityPath);

            await this.Process(new MessageRequest(message, CancellationToken.None));
        }

        this.Logger.LogDebug("{LogKey} servicebus consumer done (topic={MessageTopicName})", Constants.LogKey, messageName);
    }

    private async Task EnsureTopicAsync(string topicName)
    {
        if (!await this.managementClient.TopicExistsAsync(topicName).AnyContext())
        {
            this.Logger.LogTrace("CreateTopic(topic={MessageTopicName})", topicName);

            await this.managementClient.CreateTopicAsync(topicName).AnyContext();
        }
    }

    private async Task EnsureSubscriptionAsync(string topicName, string subscriptionName)
    {
        if (!await this.managementClient.SubscriptionExistsAsync(topicName, subscriptionName).AnyContext())
        {
            this.Logger.LogTrace("CreateSubscription(topic={MessageTopicName}, subscription={MessageSubscriptionName})", topicName, subscriptionName);

            await this.managementClient.CreateSubscriptionAsync(topicName, subscriptionName).AnyContext();
        }
    }
}