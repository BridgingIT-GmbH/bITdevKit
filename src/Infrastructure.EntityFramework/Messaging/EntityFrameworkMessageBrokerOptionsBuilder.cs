// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using BridgingIT.DevKit.Application.Messaging;

/// <summary>
/// Builds <see cref="EntityFrameworkMessageBrokerOptions"/> instances for the Entity Framework broker.
/// </summary>
/// <example>
/// <code>
/// var options = new EntityFrameworkMessageBrokerOptionsBuilder()
///     .Serializer(new SystemTextJsonSerializer())
///     .MaxDeliveryAttempts(5)
///     .LeaseDuration(TimeSpan.FromSeconds(30))
///     .Build();
/// </code>
/// </example>
public class EntityFrameworkMessageBrokerOptionsBuilder
    : OptionsBuilderBase<EntityFrameworkMessageBrokerOptions, EntityFrameworkMessageBrokerOptionsBuilder>
{
    /// <summary>
    /// Sets the publisher behaviors used by the broker.
    /// </summary>
    /// <param name="behaviors">The behaviors to execute around publish operations.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessagePublisherBehavior> behaviors)
    {
        this.Target.PublisherBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Sets the handler behaviors used by the broker.
    /// </summary>
    /// <param name="behaviors">The behaviors to execute around handler processing.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder Behaviors(IEnumerable<IMessageHandlerBehavior> behaviors)
    {
        this.Target.HandlerBehaviors = behaviors;

        return this;
    }

    /// <summary>
    /// Sets the handler factory used to resolve subscribed message handlers.
    /// </summary>
    /// <param name="handlerFactory">The handler factory.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder HandlerFactory(IMessageHandlerFactory handlerFactory)
    {
        this.Target.HandlerFactory = handlerFactory;

        return this;
    }

    /// <summary>
    /// Sets the serializer used for persisted broker messages.
    /// </summary>
    /// <param name="serializer">The serializer to use.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Enables or disables the broker runtime.
    /// </summary>
    /// <param name="value">True to enable the broker; otherwise false.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Configures whether publish operations save broker rows immediately.
    /// </summary>
    /// <param name="value">True to save immediately; otherwise false.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;

        return this;
    }

    /// <summary>
    /// Sets the startup delay before worker processing begins.
    /// </summary>
    /// <param name="value">The startup delay.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder StartupDelay(TimeSpan value)
    {
        this.Target.StartupDelay = value;

        return this;
    }

    /// <summary>
    /// Sets the interval between worker processing cycles.
    /// </summary>
    /// <param name="value">The processing interval.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder ProcessingInterval(TimeSpan value)
    {
        this.Target.ProcessingInterval = value;

        return this;
    }

    /// <summary>
    /// Sets an optional delay applied before each worker cycle.
    /// </summary>
    /// <param name="value">The processing delay.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder ProcessingDelay(TimeSpan value)
    {
        this.Target.ProcessingDelay = value;

        return this;
    }

    /// <summary>
    /// Sets the maximum number of messages processed in one cycle.
    /// </summary>
    /// <param name="value">The maximum count.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder ProcessingCount(int value)
    {
        this.Target.ProcessingCount = value;

        return this;
    }

    /// <summary>
    /// Sets the maximum number of delivery attempts per handler entry.
    /// </summary>
    /// <param name="value">The maximum delivery attempts.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder MaxDeliveryAttempts(int value)
    {
        this.Target.MaxDeliveryAttempts = value;

        return this;
    }

    /// <summary>
    /// Sets the duration of a worker lease on a broker message.
    /// </summary>
    /// <param name="value">The lease duration.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder LeaseDuration(TimeSpan value)
    {
        this.Target.LeaseDuration = value;

        return this;
    }

    /// <summary>
    /// Sets the interval used to renew active leases.
    /// </summary>
    /// <param name="value">The lease renewal interval.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder LeaseRenewalInterval(TimeSpan value)
    {
        this.Target.LeaseRenewalInterval = value;

        return this;
    }

    /// <summary>
    /// Sets the default expiration applied to published messages.
    /// </summary>
    /// <param name="value">The expiration window.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder MessageExpiration(TimeSpan? value)
    {
        this.Target.MessageExpiration = value;

        return this;
    }

    /// <summary>
    /// Sets the age after which terminal messages may be archived automatically.
    /// </summary>
    /// <param name="value">The archive age threshold.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder AutoArchiveAfter(TimeSpan? value)
    {
        this.Target.AutoArchiveAfter = value;

        return this;
    }

    /// <summary>
    /// Sets the aggregate statuses eligible for automatic archiving.
    /// </summary>
    /// <param name="values">The statuses to archive automatically.</param>
    /// <returns>The current builder instance.</returns>
    public EntityFrameworkMessageBrokerOptionsBuilder AutoArchiveStatuses(IEnumerable<BrokerMessageStatus> values)
    {
        this.Target.AutoArchiveStatuses = values?.ToArray() ?? [];

        return this;
    }
}