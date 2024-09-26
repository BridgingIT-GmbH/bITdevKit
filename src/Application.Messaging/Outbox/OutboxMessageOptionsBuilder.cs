// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Common;

public class OutboxMessageOptionsBuilder : OptionsBuilderBase<OutboxMessageOptions, OutboxMessageOptionsBuilder>
{
    public OutboxMessageOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    public OutboxMessageOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    public OutboxMessageOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;

        return this;
    }

    public OutboxMessageOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    public OutboxMessageOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingInterval(string value)
    {
        this.Target.ProcessingInterval = TimeSpan.Parse(value);

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingDelay(TimeSpan timeSpan)
    {
        this.Target.ProcessingDelay = timeSpan;

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingDelay(int milliseconds)
    {
        this.Target.ProcessingDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingDelay(string value)
    {
        this.Target.ProcessingDelay = TimeSpan.Parse(value);

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingMode(OutboxMessageProcessingMode mode)
    {
        this.Target.ProcessingMode = mode;

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxMessageProcessingMode.Immediate;
        }

        return this;
    }

    public OutboxMessageOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;

        return this;
    }

    public OutboxMessageOptionsBuilder PurgeProcessedOnStartup(bool value = true)
    {
        this.Target.PurgeProcessedOnStartup = value;

        return this;
    }

    public OutboxMessageOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;

        return this;
    }

    public OutboxMessageOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;

        return this;
    }

    public OutboxMessageOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;

        return this;
    }

    public OutboxMessageOptionsBuilder RetryCount(int retries)
    {
        this.Target.RetryCount = retries;

        return this;
    }
}