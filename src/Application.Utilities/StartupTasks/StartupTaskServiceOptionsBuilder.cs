// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using System;
using BridgingIT.DevKit.Common;

public class StartupTaskServiceOptionsBuilder :
    OptionsBuilderBase<StartupTaskServiceOptions, StartupTaskServiceOptionsBuilder>
{
    public StartupTaskServiceOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;
        return this;
    }

    public StartupTaskServiceOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;
        return this;
    }

    public StartupTaskServiceOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;
        return this;
    }

    public StartupTaskServiceOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public StartupTaskServiceOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);
        return this;
    }

    public StartupTaskServiceOptionsBuilder MaxDegreeOfParallelism(int value)
    {
        this.Target.MaxDegreeOfParallelism = value;
        return this;
    }
}