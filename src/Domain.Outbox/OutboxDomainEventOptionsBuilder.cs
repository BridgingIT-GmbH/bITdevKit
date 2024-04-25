// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Outbox;

using System;
using BridgingIT.DevKit.Common;

public class OutboxDomainEventOptionsBuilder :
    OptionsBuilderBase<OutboxDomainEventOptions, OutboxDomainEventOptionsBuilder>
{
    public OutboxDomainEventOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public OutboxDomainEventOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    public OutboxDomainEventOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;
        return this;
    }

    public OutboxDomainEventOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OutboxDomainEventOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingInterval(TimeSpan timeSpan)
    {
        this.Target.ProcessingInterval = timeSpan;
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingInterval(int milliseconds)
    {
        this.Target.ProcessingInterval = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingInterval(string value)
    {
        this.Target.ProcessingInterval = TimeSpan.Parse(value);
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingDelay(TimeSpan timeSpan)
    {
        this.Target.ProcessingDelay = timeSpan;
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingDelay(int milliseconds)
    {
        this.Target.ProcessingDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingDelay(string value)
    {
        this.Target.ProcessingDelay = TimeSpan.Parse(value);
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingMode(OutboxDomainEventProcessMode mode)
    {
        this.Target.ProcessingMode = mode;
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingModeImmediate(bool value = true)
    {
        if (value)
        {
            this.Target.ProcessingMode = OutboxDomainEventProcessMode.Immediate;
        }

        return this;
    }

    public OutboxDomainEventOptionsBuilder PurgeOnStartup(bool value = true)
    {
        this.Target.PurgeOnStartup = value;
        return this;
    }

    public OutboxDomainEventOptionsBuilder Serializer(ISerializer serializer)
    {
        this.Target.Serializer = serializer;
        return this;
    }

    public OutboxDomainEventOptionsBuilder AutoSave(bool value = true)
    {
        this.Target.AutoSave = value;
        return this;
    }

    public OutboxDomainEventOptionsBuilder ProcessingCount(int count)
    {
        this.Target.ProcessingCount = count;
        return this;
    }
}