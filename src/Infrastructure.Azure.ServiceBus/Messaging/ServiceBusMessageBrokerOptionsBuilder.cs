// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Azure;

using Application.Messaging;
using Common;
using Humanizer;

/// <summary>
/// Builds service bus message broker options configuration.
/// </summary>
public class ServiceBusMessageBrokerOptionsBuilder
    : OptionsBuilderBase<ServiceBusMessageBrokerOptions, ServiceBusMessageBrokerOptionsBuilder>
{
    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Executes the behaviors operation.
    /// </summary>
    /// <param name="behaviors">The behaviors used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Handles r factory.
    /// </summary>
    /// <param name="handlerFactory">The handler factory used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;

        return this;
    }

    /// <summary>
    /// Executes the serializer operation.
    /// </summary>
    /// <param name="serializer">The serializer used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Executes the connection string operation.
    /// </summary>
    /// <param name="connectionString">The connection string used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder ConnectionString(string connectionString)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            this.Target.ConnectionString = connectionString;
        }

        return this;
    }

    /// <summary>
    /// Executes the topic scope operation.
    /// </summary>
    /// <param name="scope">The scope used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder TopicScope(string scope)
    {
        if (!string.IsNullOrEmpty(scope))
        {
            this.Target.TopicScope = scope;
        }

        return this;
    }

    /// <summary>
    /// Executes the machine topic scope operation.
    /// </summary>
    /// <param name="suffix">The suffix used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder MachineTopicScope(string suffix = null)
    {
        this.TopicScope($"{Environment.MachineName.Humanize().Dehumanize().ToLowerInvariant()}{suffix}");

        return this;
    }

    /// <summary>
    /// Executes the process delay operation.
    /// </summary>
    /// <param name="milliseconds">The milliseconds used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder ProcessDelay(int milliseconds)
    {
        this.Target.ProcessDelay = milliseconds;

        return this;
    }

    /// <summary>
    /// Executes the message expiration operation.
    /// </summary>
    /// <param name="expiration">The expiration used by the operation.</param>
    /// <returns>The result of the operation.</returns>
    public ServiceBusMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? expiration)
    {
        if (expiration.HasValue)
        {
            this.Target.MessageExpiration = expiration;
        }

        return this;
    }

    /// <summary>
    /// Executes the auto create topic operation.
    /// </summary>
    /// <param name="value">The value used by the operation.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public ServiceBusMessageBrokerOptionsBuilder AutoCreateTopic(bool value = true)
    {
        this.Target.AutoCreateTopic = value;

        return this;
    }
}
