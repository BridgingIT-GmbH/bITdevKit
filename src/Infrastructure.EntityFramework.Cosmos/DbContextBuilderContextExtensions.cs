// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Diagnostics.HealthChecks;
using EntityFrameworkCore;

/// <summary>
///     Extension methods for configuring <see cref="CosmosDbContextBuilderContext{TContext}" />.
/// </summary>
public static class DbContextBuilderContextExtensions
{
    /// <summary>
    ///     Adds Cosmos database health-check configuration to the builder context.
    /// </summary>
    /// <typeparam name="TContext">The database context type.</typeparam>
    /// <param name="context">The Cosmos database context builder context.</param>
    /// <param name="healthQuery">An optional query used for health checks.</param>
    /// <param name="name">An optional health check registration name.</param>
    /// <param name="failureStatus">An optional health status returned when the check fails.</param>
    /// <param name="tags">Optional health check tags.</param>
    /// <param name="timeout">An optional health check timeout.</param>
    /// <returns>The same <paramref name="context" /> instance for fluent chaining.</returns>
    /// <remarks>
    ///     This method currently does not register a health check and acts as a fluent no-op.
    /// </remarks>
    public static CosmosDbContextBuilderContext<TContext> WithHealthChecks<TContext>(
        this CosmosDbContextBuilderContext<TContext> context,
        string healthQuery = default,
        string name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string> tags = null,
        TimeSpan? timeout = default)
        where TContext : DbContext
    {
        //context.Services.AddHealthChecks()
        //    .AddAzureCosmosDB(context.ConnectionString, healthQuery, null, name ?? $"{context.Provider.ToString().ToLower()} ({typeof(TContext).Name})", failureStatus, tags ?? new[] { "ready" }, timeout);

        return context;
    }
}
