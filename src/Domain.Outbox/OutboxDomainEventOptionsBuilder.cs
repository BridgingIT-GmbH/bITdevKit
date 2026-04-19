// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

/// <summary>
/// Provides fluent configuration helpers for <see cref="OutboxDomainEventOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new OutboxDomainEventOptionsBuilder()
///     .ProcessingModeImmediate()
///     .ProcessingCount(25)
///     .LeaseDuration(TimeSpan.FromSeconds(30))
///     .LeaseRenewalInterval(TimeSpan.FromSeconds(10))
///     .AutoArchiveAfter(TimeSpan.FromHours(1))
///     .Build();
/// </code>
/// </example>
public class OutboxDomainEventOptionsBuilder
    : OptionsBuilderBase<OutboxDomainEventOptions, OutboxDomainEventOptionsBuilder>
{
    /// <summary>
    /// Enables or disables outbox processing.
    /// </summary>
    public OutboxDomainEventOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    /// Disables outbox processing.
    /// </summary>
    public OutboxDomainEventOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    /// Sets the startup delay.
    /// </summary>
    public OutboxDomainEventOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    /// Sets the startup delay in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the startup delay from a parsable time span string.
    /// </summary>
    public OutboxDomainEventOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Sets the processing interval.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;

        return this;
    }

    /// <summary>
    /// Sets the processing interval in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the processing interval from a parsable time span string.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingInterval(string value)
    {
        this.Target.ProcessingInterval = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Sets the pre-processing delay.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingDelay(TimeSpan timeSpan)
    {
        this.Target.ProcessingDelay = timeSpan;

        return this;
    }

    /// <summary>
    /// Sets the pre-processing delay in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingDelay(int milliseconds)
    {
        this.Target.ProcessingDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the pre-processing delay from a parsable time span string.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingDelay(string value)
    {
        this.Target.ProcessingDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Sets the random processing jitter.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingJitter(TimeSpan timeSpan)
    {
        this.Target.ProcessingJitter = timeSpan;

        return this;
    }

    /// <summary>
    /// Sets the random processing jitter in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingJitter(int milliseconds)
    {
        this.Target.ProcessingJitter = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the random processing jitter from a parsable time span string.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingJitter(string value)
    {
        this.Target.ProcessingJitter = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    /// Sets the processing mode.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingMode(OutboxDomainEventProcessMode mode)
    {
        this.Target.ProcessingMode = mode;

        return this;
    }

    /// <summary>
    /// Switches the processing mode to immediate dispatch.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxDomainEventProcessMode.Immediate;
        }

        return this;
    }

    /// <summary>
    /// Enables purging all rows on startup.
    /// </summary>
    public OutboxDomainEventOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;

        return this;
    }

    /// <summary>
    /// Enables purging processed rows on startup.
    /// </summary>
    public OutboxDomainEventOptionsBuilder PurgeProcessedOnStartup(bool value = true)
    {
        this.Target.PurgeProcessedOnStartup = value;

        return this;
    }

    /// <summary>
    /// Sets the serializer used to persist and restore domain events.
    /// </summary>
    public OutboxDomainEventOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    /// <summary>
    /// Enables automatic persistence for queued domain events.
    /// </summary>
    public OutboxDomainEventOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;

        return this;
    }

    /// <summary>
    /// Sets the processing batch size.
    /// </summary>
    public OutboxDomainEventOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;

        return this;
    }

    /// <summary>
    /// Sets the retry limit.
    /// </summary>
    public OutboxDomainEventOptionsBuilder RetryCount(int retries)
    {
        this.Target.RetryCount = retries;

        return this;
    }

    /// <summary>
    /// Sets the processing lease duration.
    /// </summary>
    public OutboxDomainEventOptionsBuilder LeaseDuration(TimeSpan value)
    {
        this.Target.LeaseDuration = value;

        return this;
    }

    /// <summary>
    /// Sets the processing lease duration in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder LeaseDuration(int milliseconds)
    {
        this.Target.LeaseDuration = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the lease renewal interval.
    /// </summary>
    public OutboxDomainEventOptionsBuilder LeaseRenewalInterval(TimeSpan value)
    {
        this.Target.LeaseRenewalInterval = value;

        return this;
    }

    /// <summary>
    /// Sets the lease renewal interval in milliseconds.
    /// </summary>
    public OutboxDomainEventOptionsBuilder LeaseRenewalInterval(int milliseconds)
    {
        this.Target.LeaseRenewalInterval = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    /// Sets the age after which processed domain events may be archived automatically.
    /// </summary>
    public OutboxDomainEventOptionsBuilder AutoArchiveAfter(TimeSpan? value)
    {
        this.Target.AutoArchiveAfter = value;

        return this;
    }
}
