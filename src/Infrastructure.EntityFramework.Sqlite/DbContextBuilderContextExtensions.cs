// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides SQLite specific health check extensions for EF Core DbContext registrations.
/// </summary>
public static class DbContextBuilderContextExtensions
{
    /// <summary>
    /// Registers the standard EF Core DbContext health probe for the configured SQLite context.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="name">An optional health check name. Defaults to the DbContext type name.</param>
    /// <param name="failureStatus">An optional failure status override.</param>
    /// <param name="tags">Optional health check tags.</param>
    /// <returns>The same <paramref name="context"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSqliteDbContext&lt;CoreDbContext&gt;(o => o.UseConnectionString(connectionString))
    ///     .WithHealthCheck();
    /// </code>
    /// </example>
    public static SqliteDbContextBuilderContext<TContext> WithHealthCheck<TContext>(
        this SqliteDbContextBuilderContext<TContext> context,
        string name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
        where TContext : DbContext
    {
        context.Services.AddHealthChecks()
            .AddDbContextCheck<TContext>(
                name ?? typeof(TContext).Name,
                failureStatus,
                tags ?? ["db", "dbcontext", "entityframework", "sqlite"]);

        return context;
    }
}
