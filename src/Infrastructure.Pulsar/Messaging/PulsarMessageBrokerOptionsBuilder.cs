// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Pulsar;

using Application.Messaging;
using Common;

public class PulsarMessageBrokerOptionsBuilder
    : OptionsBuilderBase<PulsarMessageBrokerOptions, PulsarMessageBrokerOptionsBuilder>
{
    public PulsarMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder ServiceUrl(string serviceUrl)
    {
        this.Target.ServiceUrl = serviceUrl;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder RetryInterval(TimeSpan retryInterval)
    {
        this.Target.RetryInterval = retryInterval;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder KeepAliveInterval(TimeSpan keepAliveInterval)
    {
        this.Target.KeepAliveInterval = keepAliveInterval;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder Subscription(string subscription)
    {
        this.Target.Subscription = subscription;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder MessageScope(string scope)
    {
        this.Target.MessageScope = scope;
        return this;
    }

    public PulsarMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;
        return this;
    }
}