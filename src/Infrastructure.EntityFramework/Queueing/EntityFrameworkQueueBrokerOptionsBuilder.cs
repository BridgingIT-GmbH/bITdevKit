namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;

/// <summary>
/// Builds <see cref="EntityFrameworkQueueBrokerOptions"/> instances.
/// </summary>
public class EntityFrameworkQueueBrokerOptionsBuilder : OptionsBuilderBase<EntityFrameworkQueueBrokerOptions, EntityFrameworkQueueBrokerOptionsBuilder>
{
    /// <summary>
    /// Sets whether the broker runtime is enabled.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    /// <summary>
    /// Sets whether queue messages are saved immediately on enqueue.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;
        return this;
    }

    /// <summary>
    /// Sets the startup delay before worker processing begins.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder StartupDelay(TimeSpan value)
    {
        this.Target.StartupDelay = value;
        return this;
    }

    /// <summary>
    /// Sets the interval between worker processing cycles.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder ProcessingInterval(TimeSpan value)
    {
        this.Target.ProcessingInterval = value;
        return this;
    }

    /// <summary>
    /// Sets the delay applied before each worker processing cycle.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder ProcessingDelay(TimeSpan value)
    {
        this.Target.ProcessingDelay = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of messages processed in a single cycle.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder ProcessingCount(int value)
    {
        this.Target.ProcessingCount = value;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of delivery attempts allowed per message.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder MaxDeliveryAttempts(int value)
    {
        this.Target.MaxDeliveryAttempts = value;
        return this;
    }

    /// <summary>
    /// Sets the duration of a worker lease on a queue message.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder LeaseDuration(TimeSpan value)
    {
        this.Target.LeaseDuration = value;
        return this;
    }

    /// <summary>
    /// Sets the interval used to renew active leases.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder LeaseRenewalInterval(TimeSpan value)
    {
        this.Target.LeaseRenewalInterval = value;
        return this;
    }

    /// <summary>
    /// Sets the default expiration window applied to queued messages.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder MessageExpiration(TimeSpan? value)
    {
        this.Target.MessageExpiration = value;
        return this;
    }

    /// <summary>
    /// Sets the age after which terminal queue messages may be archived automatically.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder AutoArchiveAfter(TimeSpan? value)
    {
        this.Target.AutoArchiveAfter = value;
        return this;
    }

    /// <summary>
    /// Sets the aggregate statuses eligible for automatic archiving.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder AutoArchiveStatuses(IEnumerable<QueueMessageStatus> values)
    {
        this.Target.AutoArchiveStatuses = values?.ToArray() ?? [];
        return this;
    }

    /// <summary>
    /// Sets the queue name prefix.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder QueueNamePrefix(string value)
    {
        this.Target.QueueNamePrefix = value;
        return this;
    }

    /// <summary>
    /// Sets the queue name suffix.
    /// </summary>
    public EntityFrameworkQueueBrokerOptionsBuilder QueueNameSuffix(string value)
    {
        this.Target.QueueNameSuffix = value;
        return this;
    }
}