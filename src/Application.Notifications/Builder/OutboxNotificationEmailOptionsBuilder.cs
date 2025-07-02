namespace BridgingIT.DevKit.Application.Notifications;

using System;
using BridgingIT.DevKit.Common;

public class OutboxNotificationEmailOptionsBuilder : OptionsBuilderBase<OutboxNotificationEmailOptions, OutboxNotificationEmailOptionsBuilder>
{
    public OutboxNotificationEmailOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder ProcessingMode(OutboxNotificationEmailProcessingMode mode)
    {
        this.Target.ProcessingMode = mode;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxNotificationEmailProcessingMode.Immediate;
        }
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder PurgeProcessedOnStartup(bool value = true)
    {
        this.Target.PurgeProcessedOnStartup = value;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;
        return this;
    }

    public OutboxNotificationEmailOptionsBuilder RetryCount(int retries)
    {
        this.Target.RetryCount = retries;
        return this;
    }
}