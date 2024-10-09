// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.JobScheduling;

public class JobSchedulingOptionsBuilder : OptionsBuilderBase<JobSchedulingOptions, JobSchedulingOptionsBuilder>
{
    public JobSchedulingOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    public JobSchedulingOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    public JobSchedulingOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    public JobSchedulingOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    public JobSchedulingOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }
}