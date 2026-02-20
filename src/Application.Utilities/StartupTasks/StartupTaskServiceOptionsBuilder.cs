// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Utilities;

using Common;

/// <summary>
///     Builder for configuring <see cref="StartupTaskServiceOptions" />.
/// </summary>
public class StartupTaskServiceOptionsBuilder
    : OptionsBuilderBase<StartupTaskServiceOptions, StartupTaskServiceOptionsBuilder>
{
    /// <summary>
    ///     Halt the application process when startup task execution fails.
    /// </summary>
    public StartupTaskServiceOptionsBuilder HaltOnFailure(bool value = true)
    {
        this.Target.HaltOnFailure = value;

        return this;
    }

    /// <summary>
    ///     Enable or disable the startup tasks service.
    /// </summary>
    public StartupTaskServiceOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    ///     Disable the startup tasks service.
    /// </summary>
    public StartupTaskServiceOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    ///     Delay startup task service execution.
    /// </summary>
    public StartupTaskServiceOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    ///     Delay startup task service execution.
    /// </summary>
    public StartupTaskServiceOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    ///     Delay startup task service execution.
    /// </summary>
    public StartupTaskServiceOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    ///     Set the maximum degree of parallelism for startup task execution.
    /// </summary>
    public StartupTaskServiceOptionsBuilder MaxDegreeOfParallelism(int value)
    {
        this.Target.MaxDegreeOfParallelism = value;

        return this;
    }
}
