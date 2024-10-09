// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

public class InProcessMessageBrokerOptionsBuilder
    : OptionsBuilderBase<InProcessMessageBrokerOptions, InProcessMessageBrokerOptionsBuilder>
{
    public InProcessMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder WithBehavior(IMessagePublisherBehavior behavior)
    {
        this.Target.PublisherBehaviors = this.Target.PublisherBehaviors.Insert(behavior, -1);

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder WithBehavior(IMessageHandlerBehavior behavior)
    {
        this.Target.HandlerBehaviors = this.Target.HandlerBehaviors.Insert(behavior, -1);

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder FilterScope(string scope)
    {
        this.Target.FilterScope = scope;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder MessageScope(string scope)
    {
        this.Target.MessageScope = scope;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;

        return this;
    }

    public InProcessMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }
}