// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Owns code-registered module context dispatch behind one shared EntityFramework cache kind.</summary>
/// <remarks>Named registrations override the optional default factory. The owner builder's finalization closes every registration path; native provider creation only reads the private factory catalog.</remarks>
/// <example><code>services.AddApplicationCaching().WithEntityFrameworkProvider&lt;CatalogDbContext&gt;("catalog");</code></example>
public sealed class EntityFrameworkCacheRegistration
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, FactoryRegistration> named = new(StringComparer.OrdinalIgnoreCase);
    private FactoryRegistration fallback;

    /// <summary>Registers one shared native kind dispatcher over the owning startup builder.</summary>
    /// <param name="builder">The shared service collection's still-mutable caching builder.</param>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <exception cref="InvalidOperationException">The owner catalog is already finalized.</exception>
    /// <example><code>var registration = new EntityFrameworkCacheRegistration(builder);</code></example>
    public EntityFrameworkCacheRegistration(ApplicationCachingBuilderContext builder)
    {
        this.Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        this.EnsureMutable();
        builder.WithProviderKind("EntityFramework", EntityFrameworkCacheStoreProvider.CapabilitiesFor, this.CreateProvider);
    }

    /// <summary>Gets the one shared builder whose lifetime controls this native registration catalog.</summary>
    public ApplicationCachingBuilderContext Builder { get; }

    /// <summary>Registers an optional fallback for configuration-defined provider names while rejecting ambiguous module context selection.</summary>
    /// <typeparam name="TContext">The code-approved standalone or module cache context.</typeparam>
    /// <param name="contextFactory">An explicit independent context factory, or null to resolve the registered IDbContextFactory&lt;TContext&gt; lazily.</param>
    /// <exception cref="InvalidOperationException">The catalog is finalized or a different fallback context/factory is already registered.</exception>
    /// <example><code>registration.RegisterFallback&lt;CatalogDbContext&gt;();</code></example>
    public void RegisterFallback<TContext>(Func<IServiceProvider, string, CacheProviderOptions, TContext> contextFactory = null)
        where TContext : DbContext, IApplicationCachingContext
    {
        lock (this.gate)
        {
            this.EnsureMutable();
            if (this.fallback is not null)
            {
                if (this.fallback.ContextType == typeof(TContext) && Equals(this.fallback.ContextFactory, contextFactory)) { return; }

                throw new InvalidOperationException("An EntityFramework cache fallback context or factory is already registered. Use WithEntityFrameworkProvider<TContext>(name, configure) for independent module providers.");
            }

            this.fallback = Registration(contextFactory);
        }
    }

    /// <summary>Declares one unique provider alias and its independently owned module context factory.</summary>
    /// <typeparam name="TContext">The code-approved module cache context.</typeparam>
    /// <param name="name">The provider alias referenced by collections.</param>
    /// <param name="configure">Optional native provider settings, below configuration overrides.</param>
    /// <exception cref="ArgumentException">The alias is invalid or already registered.</exception>
    /// <exception cref="InvalidOperationException">The owner catalog is finalized.</exception>
    /// <example><code>registration.RegisterNamed&lt;CatalogDbContext&gt;("catalog");</code></example>
    public void RegisterNamed<TContext>(string name, Action<EntityFrameworkCacheProviderOptionsBuilder> configure = null)
        where TContext : DbContext, IApplicationCachingContext
    {
        lock (this.gate)
        {
            this.EnsureMutable();
            var alias = CacheIdentityNormalizer.Name(name);
            if (this.named.ContainsKey(alias)) { throw new ArgumentException("A named EntityFramework cache provider was registered twice.", nameof(name)); }

            var options = new EntityFrameworkCacheProviderOptionsBuilder();
            configure?.Invoke(options);
            this.EnsureMutable();
            var settings = options.Patch.Apply(new CacheProviderOptions(), "Fluent", new Dictionary<string, string>());
            var metadataBytes = Math.Min(16 * CacheDefaults.MiB, Math.Max(1, settings.MaxResidentBytes / 8));
            this.Builder.WithProvider(alias, provider =>
            {
                provider.Patch.Merge(null, options.Patch);
                this.EnsureMutable();
                provider.With(value => value.Kind, "EntityFramework");
            }, services => new CacheProviderOptions { Engine = InferEngine<TContext>(services), MetadataCacheBytes = metadataBytes });
            this.EnsureMutable();
            this.named.Add(alias, Registration<TContext>(null));
        }
    }

    /// <summary>Creates the exact named module provider, or the explicit fallback, using only code-approved context factories.</summary>
    /// <param name="services">The singleton-compatible host services captured by the cache runtime.</param>
    /// <param name="name">The finalized provider alias.</param>
    /// <param name="options">The finalized native settings; configuration supplies no CLR context.</param>
    /// <returns>An independently owned complete relational provider whose context creation remains lazy.</returns>
    /// <exception cref="InvalidOperationException">No code-registered context factory permits this provider name.</exception>
    /// <example><code>var provider = registration.CreateProvider(services, "catalog", options);</code></example>
    public ICacheStoreProvider CreateProvider(IServiceProvider services, string name, CacheProviderOptions options)
    {
        FactoryRegistration registration;
        lock (this.gate)
        {
            registration = this.named.GetValueOrDefault(CacheIdentityNormalizer.Name(name)) ?? this.fallback ??
                throw new InvalidOperationException($"EntityFramework cache provider '{name}' has no code-registered context factory. Use WithEntityFrameworkProvider<TContext>(name, configure) or register an explicit fallback.");
        }

        return registration.Create(services, name, options);
    }

    private static FactoryRegistration Registration<TContext>(Func<IServiceProvider, string, CacheProviderOptions, TContext> contextFactory)
        where TContext : DbContext, IApplicationCachingContext => new(typeof(TContext), contextFactory, (services, name, options) =>
            new EntityFrameworkCacheStoreProvider<TContext>(name, options,
                () => contextFactory is null ? services.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext() : contextFactory(services, name, options),
                services.GetRequiredService<TimeProvider>(), services.GetRequiredService<CacheRuntimeResources>(), services.GetService<IDatabaseReadyService>(),
                services.GetService<ILogger<EntityFrameworkCacheStoreProvider>>(), services.GetRequiredService<CacheCatalog>().Options.Diagnostics));

    private static string InferEngine<TContext>(IServiceCollection services)
    {
        var registration = services.Where(descriptor => descriptor.ServiceType == typeof(DbContextRegistration) && !descriptor.IsKeyedService)
            .Select(descriptor => descriptor.ImplementationInstance).OfType<DbContextRegistration>()
            .LastOrDefault(candidate => candidate.ContextType == typeof(TContext));
        return registration?.Provider.ToUpperInvariant() switch
        {
            "SQLSERVER" => "SqlServer",
            "POSTGRES" or "POSTGRESQL" => "PostgreSql",
            _ => "Sqlite"
        };
    }

    private void EnsureMutable()
    {
        if (this.Builder.IsFinalized) { throw new InvalidOperationException("The cache startup catalog is finalized; register module providers before resolving it."); }
    }

    private sealed record FactoryRegistration(Type ContextType, Delegate ContextFactory,
        Func<IServiceProvider, string, CacheProviderOptions, ICacheStoreProvider> Create);
}