// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>
///     Builder for configuring <see cref="StartupTaskOptions" />.
/// </summary>
public class StartupTaskOptionsBuilder : OptionsBuilderBase<StartupTaskOptions, StartupTaskOptionsBuilder>
{
    /// <summary>
    ///     Halt the application process when this startup task fails.
    /// </summary>
    public StartupTaskOptionsBuilder HaltOnFailure(bool value = true)
    {
        this.Target.HaltOnFailure = value;

        return this;
    }

    /// <summary>
    ///     Enable or disable this startup task.
    /// </summary>
    public StartupTaskOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    ///     Disable this startup task.
    /// </summary>
    public StartupTaskOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    ///     Delay startup task execution.
    /// </summary>
    public StartupTaskOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    ///     Delay startup task execution.
    /// </summary>
    public StartupTaskOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    ///     Delay startup task execution.
    /// </summary>
    public StartupTaskOptionsBuilder StartupDelay(TimeSpan timeSpan)
    {
        this.Target.StartupDelay = timeSpan;

        return this;
    }

    /// <summary>
    ///     Set the execution order for this startup task.
    /// </summary>
    public StartupTaskOptionsBuilder Order(int value)
    {
        this.Target.Order = value;

        return this;
    }
}
