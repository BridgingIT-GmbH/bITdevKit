// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.RabbitMQ;

using Application.Messaging;
using Common;

public class RabbitMQMessageBrokerOptionsBuilder
    : OptionsBuilderBase<RabbitMQMessageBrokerOptions, RabbitMQMessageBrokerOptionsBuilder>
{
    public RabbitMQMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder HostName(string hostName)
    {
        if (!string.IsNullOrEmpty(hostName))
        {
            this.Target.HostName = hostName;
            this.Target.ConnectionString = null;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
            this.Target.HostName = null;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder ExchangeName(string name)
    {
        if (!name.IsNullOrEmpty())
        {
            this.Target.ExchangeName = name;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder QueueName(string name)
    {
        if (!name.IsNullOrEmpty())
        {
            this.Target.QueueName = name;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder QueueNameSuffix(string suffix)
    {
        this.Target.QueueNameSuffix = suffix;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder Retries(int? count)
    {
        if (count.HasValue)
        {
            this.Target.Retries = count.Value;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder DurableEnabled(bool enabled = true)
    {
        this.Target.IsDurable = enabled;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder ExclusiveQueueEnabled(bool enabled)
    {
        this.Target.ExclusiveQueue = enabled;
        return this;
    }

    public RabbitMQMessageBrokerOptionsBuilder AutoDeleteQueueEnabled(bool enabled)
    {
        this.Target.AutoDeleteQueue = enabled;
        return this;
    }
}