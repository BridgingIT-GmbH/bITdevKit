// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BridgingIT.DevKit.Infrastructure.EntityFramework;

/// <summary>
/// Provides SQL Server specific health check extensions for EF Core DbContext registrations.
/// </summary>
public static class DbContextBuilderContextExtensions
{
    /// <summary>Optionally registers a singleton factory using the existing SQL Server native configuration and independently owned contexts.</summary>
    /// <remarks>The normal DbContext and its options keep their original lifetimes. Factory options resolve lazily and omit the registered interceptor chain, which belongs to business/request scopes. Additional context-constructor dependencies must be singleton-compatible. The caller disposes every created context; no pooling or request-scope borrowing occurs. An existing factory or absent native configuration leaves registrations unchanged and emits one structured startup warning. Native option and context activation failures retain their normal runtime errors.</remarks>
    /// <typeparam name="TContext">The host context exposing an options constructor and optional singleton-compatible dependencies.</typeparam>
    /// <param name="context">The SQL Server registration returned by AddSqlServerDbContext.</param>
    /// <returns>The same builder for fluent host configuration.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <example><code>services.AddSqlServerDbContext&lt;CatalogDbContext&gt;(connectionString).WithDbContextFactory();</code></example>
    public static SqlServerDbContextBuilderContext<TContext> WithDbContextFactory<TContext>(this SqlServerDbContextBuilderContext<TContext> context)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.NativeFactoryOptions is null)
        {
            Warn(context, "MissingNativeOptions");
            return context;
        }

        var existing = context.Services.Where(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<TContext>)).ToArray();
        if (existing.Length > 0)
        {
            if (existing.Length == 1 && existing[0].ImplementationFactory?.Target is ContextFactoryRegistration<TContext> registered &&
                ReferenceEquals(registered.Context, context)) { return context; }

            Warn(context, "ExistingFactory");
            return context;
        }

        var registration = new ContextFactoryRegistration<TContext>(context);
        context.Services.Add(new ServiceDescriptor(typeof(IDbContextFactory<TContext>), registration.Create, ServiceLifetime.Singleton));
        return context;
    }

    private static void Warn<TContext>(SqlServerDbContextBuilderContext<TContext> context, string reason) where TContext : DbContext
    {
        if (context.Services.Any(descriptor => descriptor.ServiceType == typeof(SqlServerDbContextFactoryWarning<TContext>))) { return; }

        context.Services.AddLogging();
        context.Services.AddSingleton(services => new SqlServerDbContextFactoryWarning<TContext>(
            services.GetRequiredService<ILogger<SqlServerDbContextFactoryWarning<TContext>>>(), reason));
        context.Services.AddHostedService(services => services.GetRequiredService<SqlServerDbContextFactoryWarning<TContext>>());
    }

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

    private sealed class ContextFactoryRegistration<TContext>(SqlServerDbContextBuilderContext<TContext> context) where TContext : DbContext
    {
        /// <summary>Gets the exact SQL registration owning this opt-in factory descriptor.</summary>
        public SqlServerDbContextBuilderContext<TContext> Context { get; } = context;

        /// <summary>Builds independent native options only when the singleton factory is first resolved.</summary>
        /// <param name="services">The host services supplying singleton-compatible native dependencies.</param>
        /// <returns>The factory whose created contexts belong to its callers.</returns>
        public object Create(IServiceProvider services)
        {
            var options = new DbContextOptionsBuilder<TContext>();
            this.Context.NativeFactoryOptions(services, options);
            return new SqlServerDbContextFactory<TContext>(services, options.Options);
        }
    }
}