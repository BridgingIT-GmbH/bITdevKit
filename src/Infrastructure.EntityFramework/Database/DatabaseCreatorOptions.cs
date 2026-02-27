// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

public class DatabaseCreatorOptions : OptionsBase
{
    /// <summary>
    ///     Gets or sets a value indicating whether the application process should be terminated immediately when database creation fails.
    /// </summary>
    /// <remarks>
    ///     When enabled, a failure triggers <see cref="Environment.FailFast(string, Exception)" /> after logging.
    /// </remarks>
    public bool HaltOnFailure { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the database creator is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the delay before the database creator starts.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    ///     Gets or sets a value indicating whether to log the EF Core model during startup.
    /// </summary>
    public bool LogModel { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to delete the database on startup.
    /// </summary>
    public bool EnsureDeleted { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to truncate all tables on startup.
    /// </summary>
    public bool EnsureTruncated { get; set; }

    /// <summary>
    ///     Gets or sets the tables that should be excluded when truncating on startup.
    /// </summary>
    public List<string> EnsureTruncatedIgnoreTables { get; set; } = ["MigrationsHistory"];
}
