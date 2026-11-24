// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Registers the trusted complete relational provider independently of simple Common.Caching and key/value storage.</summary>
/// <example><code>builder.WithEntityFrameworkProvider&lt;CatalogDbContext&gt;();</code></example>
public static class CacheRegistrationExtensions
{
    /// <summary>Registers the complete EntityFramework provider using the host's registered context factory.</summary>
    /// <remarks>
    /// Register IDbContextFactory&lt;TContext&gt; with a lifetime compatible with the cache provider. Factory resolution
    /// and context creation remain lazy until native access. Every bounded operation owns and disposes its fresh context;
    /// request-scoped contexts are never borrowed. Consumers deploy their own migrations before database readiness.
    /// </remarks>
    /// <typeparam name="TContext">The host context implementing the advanced-cache persistence capability.</typeparam>
    /// <param name="builder">The advanced-cache registration builder.</param>
    /// <returns>The same builder for additional cache registration.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <example><code>
    /// services.AddDbContextFactory&lt;CatalogDbContext&gt;(options =&gt; options.UseSqlServer(connectionString));
    /// services.AddApplicationCaching().WithEntityFrameworkProvider&lt;CatalogDbContext&gt;();
    /// </code></example>
    public static ApplicationCachingBuilderContext WithEntityFrameworkProvider<TContext>(this ApplicationCachingBuilderContext builder)
        where TContext : DbContext, IApplicationCachingContext
    {
        Registration(builder).RegisterFallback<TContext>();
        return builder;
    }

    /// <summary>Declares one named EntityFramework provider using its module's registered context factory.</summary>
    /// <remarks>Each name selects only its code-registered context type. The engine is inferred from DevKit context registrations when the catalog is finalized; absent metadata defaults to SQLite. An explicit typed engine overrides inference, and configuration overrides fluent settings. Register a singleton-compatible IDbContextFactory&lt;TContext&gt;; each native operation creates and disposes its own context. Select a database readiness alias explicitly when required by the host.</remarks>
    /// <typeparam name="TContext">The module context implementing the advanced-cache persistence capability.</typeparam>
    /// <param name="builder">The shared advanced-cache registration builder.</param>
    /// <param name="name">The unique provider alias selected by collections.</param>
    /// <param name="configure">Optional native provider settings, below configuration overrides.</param>
    /// <returns>The same builder for additional module registration.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <exception cref="ArgumentException">The provider alias is invalid or already declared.</exception>
    /// <exception cref="InvalidOperationException">The shared startup catalog is finalized.</exception>
    /// <example><code>services.AddApplicationCaching().WithEntityFrameworkProvider&lt;CatalogDbContext&gt;("catalog", provider =&gt; provider.SqlServer());</code></example>
    public static ApplicationCachingBuilderContext WithEntityFrameworkProvider<TContext>(this ApplicationCachingBuilderContext builder,
        string name, Action<EntityFrameworkCacheProviderOptionsBuilder> configure = null) where TContext : DbContext, IApplicationCachingContext
    {
        Registration(builder).RegisterNamed<TContext>(name, configure);
        return builder;
    }

    /// <summary>Registers the EntityFramework kind using a standalone or module context implementing the cache capability.</summary>
    /// <remarks>Consumers configure their engine and scaffold/apply migrations for their own cache-capable context. The runtime package ships no migrations. Automatic execution-strategy mutation retries are rejected.</remarks>
    /// <typeparam name="TContext">The host context exposing the annotated advanced-cache persistence sets.</typeparam>
    /// <param name="builder">The advanced-cache registration builder.</param>
    /// <param name="contextFactory">Creates a fresh independently owned context for each bounded native call.</param>
    /// <returns>The builder for additional cache registration.</returns>
    /// <example><code>builder.WithEntityFrameworkProvider((services, name, options) =&gt; new CatalogDbContext(contextOptions));</code></example>
    public static ApplicationCachingBuilderContext WithEntityFrameworkProvider<TContext>(this ApplicationCachingBuilderContext builder,
        Func<IServiceProvider, string, CacheProviderOptions, TContext> contextFactory)
        where TContext : DbContext, IApplicationCachingContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(contextFactory);
        Registration(builder).RegisterFallback(contextFactory);
        return builder;
    }

    private static EntityFrameworkCacheRegistration Registration(ApplicationCachingBuilderContext builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.IsFinalized) { throw new InvalidOperationException("The cache startup catalog is finalized; register module providers before resolving it."); }

        var existing = builder.Services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(EntityFrameworkCacheRegistration));
        if (existing?.ImplementationInstance is EntityFrameworkCacheRegistration registration)
        {
            if (!ReferenceEquals(registration.Builder, builder))
            {
                throw new InvalidOperationException("EntityFramework caching must use the shared AddApplicationCaching registration builder.");
            }

            return registration;
        }

        if (existing is not null) { throw new InvalidOperationException("The EntityFramework cache registration state must be a single code-owned instance."); }

        var created = new EntityFrameworkCacheRegistration(builder);
        builder.Services.AddSingleton(created);
        return created;
    }
}