// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using Common;
using Humanizer;

public class ServiceBusMessageBrokerOptionsBuilder
    : OptionsBuilderBase<ServiceBusMessageBrokerOptions, ServiceBusMessageBrokerOptionsBuilder>
{
    public ServiceBusMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;
        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
        }

        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder TopicScope(string scope)
    {
        if (!string.IsNullOrEmpty(scope))
        {
            this.Target.TopicScope = scope;
        }

        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder MachineTopicScope(string suffix = null)
    {
        this.TopicScope($"{Environment.MachineName.Humanize().Dehumanize().ToLowerInvariant()}{suffix}");

        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }

    public ServiceBusMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }
}