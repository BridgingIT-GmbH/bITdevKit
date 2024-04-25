// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System;

public class StartupTaskOptionsBuilder :
    OptionsBuilderBase<StartupTaskOptions, StartupTaskOptionsBuilder>
{
    public StartupTaskOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public StartupTaskOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    public StartupTaskOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);
        return this;
    }

    public StartupTaskOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public StartupTaskOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;
        return this;
    }

    public StartupTaskOptionsBuilder Order(int value)
    {
        this.Target.Order = value;
        return this;
    }
}