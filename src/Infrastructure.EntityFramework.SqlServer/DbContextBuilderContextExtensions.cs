// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Provides SQL Server specific health check extensions for EF Core DbContext registrations.
/// </summary>
public static class DbContextBuilderContextExtensions
{
    /// <summary>
    /// Registers the standard EF Core DbContext health probe for the configured SQL Server context.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The database context builder context.</param>
    /// <param name="name">An optional health check name. Defaults to the DbContext type name.</param>
    /// <param name="failureStatus">An optional failure status override.</param>
    /// <param name="tags">Optional health check tags.</param>
    /// <returns>The same <paramref name="context"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSqlServerDbContext&lt;CoreDbContext&gt;(o => o.UseConnectionString(connectionString))
    ///     .WithHealthCheck();
    /// </code>
    /// </example>
    public static SqlServerDbContextBuilderContext<TContext> WithHealthCheck<TContext>(
        this SqlServerDbContextBuilderContext<TContext> context,
        string name = null,
        HealthStatus? failureStatus = null,
        IEnumerable<string> tags = null)
        where TContext : DbContext
    {
        context.Services.AddHealthChecks()
            .AddDbContextCheck<TContext>(
                name ?? typeof(TContext).Name,
                failureStatus,
                tags ?? ["db", "dbcontext", "entityframework", "sqlserver"]);

        return context;
    }
}
