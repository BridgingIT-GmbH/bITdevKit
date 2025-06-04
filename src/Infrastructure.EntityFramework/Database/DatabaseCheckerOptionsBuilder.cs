// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DatabaseCheckerOptionsBuilder : OptionsBuilderBase<DatabaseCheckerOptions, DatabaseCheckerOptionsBuilder>
{
    /// <summary>
    ///     Enable or Disable the database migrator.
    /// </summary>
    public DatabaseCheckerOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    ///     Disable the database migrator.
    /// </summary>
    public DatabaseCheckerOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCheckerOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCheckerOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCheckerOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }
}