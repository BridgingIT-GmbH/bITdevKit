// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;

/// <summary>
/// Registers the SQLite database adapter of a development instance explicitly. The call is optional:
/// <c>AddDevAppInstance</c> finds the adapter automatically when the SQLite provider package is referenced.
/// An explicit call makes the adapter run before the automatically found ones.
/// </summary>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithSqliteDatabases());
/// </code>
/// </example>
public static class DevAppInstanceBuilderSqliteExtensions
{
    /// <summary>
    /// Gives each linked worktree its own SQLite database file. Every connection string whose configuration path contains
    /// <c>ConnectionString</c> and that names a database file gets the file <c>&lt;name&gt;_wNN&lt;extension&gt;</c> in the
    /// same directory. In-memory databases are left alone.
    /// </summary>
    /// <param name="builder">The development instance builder.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.WithSqliteDatabases(); // Data Source=data/forecasthub.db becomes Data Source=data/forecasthub_w01.db in slot 1
    /// </code>
    /// </example>
    public static DevAppInstanceBuilder WithSqliteDatabases(this DevAppInstanceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithDatabases(SqliteDevAppInstanceAdapter.Instance);
    }
}
