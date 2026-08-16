// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Builds in process message broker options configuration.
/// </summary>
public class InProcessMessageBrokerOptionsBuilder
    : OptionsBuilderBase<InProcessMessageBrokerOptions, InProcessMessageBrokerOptionsBuilder>
{
    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="behavior">The behavior used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder WithBehavior(IMessagePublisherBehavior behavior)
    {
        this.Target.PublisherBehaviors = this.Target.PublisherBehaviors.Insert(behavior, -1);

        return this;
    }

    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Executes the with behavior operation.
    /// </summary>
    /// <param name="behavior">The behavior used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder WithBehavior(IMessageHandlerBehavior behavior)
    {
        this.Target.HandlerBehaviors = this.Target.HandlerBehaviors.Insert(behavior, -1);

        return this;
    }

    /// <summary>
    /// Handles r factory.
    /// </summary>
    /// <param name="handlerFactory">The handler factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;

        return this;
    }

    /// <summary>
    /// Executes the serializer operation.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Executes the filter scope operation.
    /// </summary>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder FilterScope(string scope)
    {
        this.Target.FilterScope = scope;

        return this;
    }

    /// <summary>
    /// Executes the message scope operation.
    /// </summary>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder MessageScope(string scope)
    {
        this.Target.MessageScope = scope;

        return this;
    }

    /// <summary>
    /// Executes the process delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;

        return this;
    }

    /// <summary>
    /// Executes the message expiration operation.
    /// </summary>
    /// <param name="expiration">The expiration used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public InProcessMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }
}
