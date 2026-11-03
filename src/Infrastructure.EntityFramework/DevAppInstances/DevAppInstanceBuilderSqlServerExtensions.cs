// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;

/// <summary>
/// Registers the SQL Server database adapter of a development instance explicitly. The call is optional:
/// <c>AddDevAppInstance</c> finds the adapter automatically when the SQL Server provider package is referenced.
/// An explicit call makes the adapter run before the automatically found ones.
/// </summary>
/// <example>
/// <code>
/// builder.AddDevAppInstance(o => o
///     .WithAppId("forecasthub")
///     .WithSqlServerDatabases());
/// </code>
/// </example>
public static class DevAppInstanceBuilderSqlServerExtensions
{
    /// <summary>
    /// Gives each linked worktree its own SQL Server database. Every connection string whose configuration path contains
    /// <c>ConnectionString</c> and that names a database gets the database <c>&lt;database&gt;_wNN</c>. Keys that point
    /// at the same server and database keep pointing at the same instance database.
    /// </summary>
    /// <param name="builder">The development instance builder.</param>
    /// <returns>The builder.</returns>
    /// <example>
    /// <code>
    /// o.WithSqlServerDatabases(); // Database=ForecastHub becomes Database=ForecastHub_w01 in slot 1
    /// </code>
    /// </example>
    public static DevAppInstanceBuilder WithSqlServerDatabases(this DevAppInstanceBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithDatabases(SqlServerDevAppInstanceAdapter.Instance);
    }
}
