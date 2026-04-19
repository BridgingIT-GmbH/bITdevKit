namespace BridgingIT.DevKit.Application.Notifications;

using System;
using BridgingIT.DevKit.Common;

/// <summary>
/// Provides fluent configuration helpers for <see cref="OutboxNotificationEmailOptions" />.
/// </summary>
/// <example>
/// <code>
/// var options = new OutboxNotificationEmailOptionsBuilder()
///     .ProcessingModeImmediate()
///     .ProcessingCount(25)
///     .LeaseDuration(TimeSpan.FromMinutes(1))
///     .AutoArchiveAfter(TimeSpan.FromHours(1))
///     .Build();
/// </code>
/// </example>
public class OutboxNotificationEmailOptionsBuilder : OptionsBuilderBase<OutboxNotificationEmailOptions, OutboxNotificationEmailOptionsBuilder>
{
    /// <summary>
    /// Enables or disables outbox processing.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    /// <summary>
    /// Sets the startup delay.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the startup delay in milliseconds.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the processing interval.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the processing interval in milliseconds.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the pre-processing delay.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingDelay(TimeSpan timeSpan)
    {
        this.Target.ProcessingDelay = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the pre-processing delay in milliseconds.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingDelay(int milliseconds)
    {
        this.Target.ProcessingDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the random processing jitter.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingJitter(TimeSpan timeSpan)
    {
        this.Target.ProcessingJitter = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the random processing jitter in milliseconds.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingJitter(int milliseconds)
    {
        this.Target.ProcessingJitter = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the processing mode.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingMode(OutboxNotificationEmailProcessingMode mode)
    {
        this.Target.ProcessingMode = mode;
        return this;
    }

    /// <summary>
    /// Switches the processing mode to immediate dispatch.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxNotificationEmailProcessingMode.Immediate;
        }
        return this;
    }

    /// <summary>
    /// Enables purging all rows on startup.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;
        return this;
    }

    /// <summary>
    /// Enables purging processed rows on startup.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder PurgeProcessedOnStartup(bool value = true)
    {
        this.Target.PurgeProcessedOnStartup = value;
        return this;
    }

    /// <summary>
    /// Enables automatic persistence for queued messages.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;
        return this;
    }

    /// <summary>
    /// Sets the processing batch size.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;
        return this;
    }

    /// <summary>
    /// Sets the retry limit.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder RetryCount(int retries)
    {
        this.Target.RetryCount = retries;
        return this;
    }

    /// <summary>
    /// Sets the processing lease duration.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder LeaseDuration(TimeSpan timeSpan)
    {
        this.Target.LeaseDuration = timeSpan;
        return this;
    }

    /// <summary>
    /// Sets the processing lease duration in milliseconds.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder LeaseDuration(int milliseconds)
    {
        this.Target.LeaseDuration = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    /// <summary>
    /// Sets the age after which terminal notification emails may be archived automatically.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder AutoArchiveAfter(TimeSpan? value)
    {
        this.Target.AutoArchiveAfter = value;
        return this;
    }

    /// <summary>
    /// Sets the terminal statuses eligible for automatic archiving.
    /// </summary>
    public OutboxNotificationEmailOptionsBuilder AutoArchiveStatuses(IEnumerable<EmailMessageStatus> values)
    {
        this.Target.AutoArchiveStatuses = values?.ToArray() ?? [];
        return this;
    }
}
