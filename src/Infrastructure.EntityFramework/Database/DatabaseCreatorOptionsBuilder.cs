// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DatabaseCreatorOptionsBuilder : OptionsBuilderBase<DatabaseCreatorOptions, DatabaseCreatorOptionsBuilder>
{
    /// <summary>
    ///     Enable or Disable the database migrator.
    /// </summary>
    public DatabaseCreatorOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    ///     Disable the database migrator.
    /// </summary>
    public DatabaseCreatorOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCreatorOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCreatorOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseCreatorOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    ///     Log the model.
    /// </summary>
    public DatabaseCreatorOptionsBuilder LogModel(bool value = true)
    {
        this.Target.LogModel = value;

        return this;
    }

    /// <summary>
    ///     Delete database on startup.
    /// </summary>
    public DatabaseCreatorOptionsBuilder DeleteOnStartup(bool value = true)
    {
        this.Target.EnsureDeleted = value;

        return this;
    }

    /// <summary>
    ///     Truncate the database on startup.
    /// </summary>
    public DatabaseCreatorOptionsBuilder PurgeOnStartup(bool value = true, IEnumerable<string> ignoreTables = null)
    {
        this.Target.EnsureTruncated = value;
        this.Target.EnsureTruncatedIgnoreTables.AddRange(ignoreTables.SafeNull());

        return this;
    }
}