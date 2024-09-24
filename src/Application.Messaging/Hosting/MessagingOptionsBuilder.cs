// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Common;

public class MessagingOptionsBuilder : OptionsBuilderBase<MessagingOptions, MessagingOptionsBuilder>
{
    public MessagingOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public MessagingOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    public MessagingOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;
        return this;
    }

    public MessagingOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public MessagingOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);
        return this;
    }
}