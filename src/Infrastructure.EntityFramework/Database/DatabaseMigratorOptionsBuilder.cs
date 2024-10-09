// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DatabaseMigratorOptionsBuilder
    : OptionsBuilderBase<DatabaseMigratorOptions, DatabaseMigratorOptionsBuilder>
{
    /// <summary>
    ///     Enable or Disable the database migrator.
    /// </summary>
    public DatabaseMigratorOptionsBuilder Enabled(bool value = true)
    {
        this.Target.Enabled = value;

        return this;
    }

    /// <summary>
    ///     Disable the database migrator.
    /// </summary>
    public DatabaseMigratorOptionsBuilder Disabled()
    {
        this.Target.Enabled = false;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseMigratorOptionsBuilder StartupDelay(TimeSpan timespan)
    {
        this.Target.StartupDelay = timespan;

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseMigratorOptionsBuilder StartupDelay(int milliseconds)
    {
        this.Target.StartupDelay = TimeSpan.FromMilliseconds(milliseconds);

        return this;
    }

    /// <summary>
    ///     Delay the startup of the database migrator.
    /// </summary>
    public DatabaseMigratorOptionsBuilder StartupDelay(string value)
    {
        this.Target.StartupDelay = TimeSpan.Parse(value);

        return this;
    }

    /// <summary>
    ///     Log the model.
    /// </summary>
    public DatabaseMigratorOptionsBuilder LogModel(bool value = true)
    {
        this.Target.LogModel = value;

        return this;
    }

    /// <summary>
    ///     Delete database on startup.
    /// </summary>
    public DatabaseMigratorOptionsBuilder DeleteOnStartup(bool value = true)
    {
        this.Target.EnsureDeleted = value;

        return this;
    }

    /// <summary>
    ///     Truncate the database on startup.
    /// </summary>
    public DatabaseMigratorOptionsBuilder PurgeOnStartup(bool value = true, IEnumerable<string> ignoreTables = null)
    {
        this.Target.EnsureTruncated = value;
        this.Target.EnsureTruncatedIgnoreTables.AddRange(ignoreTables.SafeNull());

        return this;
    }
}