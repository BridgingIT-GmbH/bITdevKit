// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.UnitTests.EntityFramework.Caching;

using System.Text.Json;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Infrastructure.EntityFramework;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises independent module registrations over one native kind and separate consumer-owned SQLite databases.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheModuleRegistrationTests</code></example>
public sealed class CacheModuleRegistrationTests
{
    /// <summary>Two modules contribute collections and native context routes through repeated Add calls without replacing each other's services or configuration.</summary>
    [Fact]
    public async Task Modules_RepeatedAdd_RoutesNamedProvidersToIndependentOwnedContexts()
    {
        // Arrange
        await using var catalogStore = await Store<CatalogModule>.CreateAsync();
        await using var shippingStore = await Store<ShippingModule>.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<ModuleContext<CatalogModule>>>(catalogStore.Factory);
        services.AddSingleton<IDbContextFactory<ModuleContext<ShippingModule>>>(shippingStore.Factory);
        var catalogConfiguration = Configuration("catalog");
        var shippingConfiguration = Configuration("shipping");
        var first = services.AddApplicationCaching().WithConfiguration(catalogConfiguration)
            .WithEntityFrameworkProvider<ModuleContext<CatalogModule>>("catalog", provider => provider.With(value => value.Engine, "SqlServer"))
            .WithValueContract<string>("catalog-text")
            .WithCollection<string>("products", collection => collection.UseContract("catalog-text").UseProvider("catalog"));
        var second = services.AddApplicationCaching().WithConfiguration(shippingConfiguration)
            .WithEntityFrameworkProvider<ModuleContext<ShippingModule>>("shipping", provider => provider.With(value => value.Engine, "SqlServer"))
            .WithValueContract<string>("shipping-text")
            .WithCollection<string>("deliveries", collection => collection.UseContract("shipping-text").UseProvider("shipping"));
        first.ShouldBeSameAs(second);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var cache = provider.GetRequiredService<IApplicationCache>();
        var products = cache.GetCollection<string>("products");
        var deliveries = cache.GetCollection<string>("deliveries");
        var catalog = provider.GetRequiredService<CacheCatalog>();
        catalog.Registry.ProviderKinds.Values.Count(kind => kind.Alias == "EntityFramework").ShouldBe(1);
        catalog.Providers["catalog"].Options.Engine.ShouldBe("Sqlite");
        catalog.Providers["shipping"].Options.Engine.ShouldBe("Sqlite");
        catalogStore.Factory.Contexts.ShouldBeEmpty();
        shippingStore.Factory.Contexts.ShouldBeEmpty();

        // Act
        var firstWrite = await products.SetAsync(new("same-key"), "catalog value");
        var secondWrite = await deliveries.SetAsync(new("same-key"), "shipping value");
        var firstRead = await products.GetAsync("same-key");
        var secondRead = await deliveries.GetAsync("same-key");

        // Assert
        firstWrite.IsSuccess.ShouldBeTrue(string.Join(", ", firstWrite.Errors.Select(error => error.Message)));
        secondWrite.IsSuccess.ShouldBeTrue(string.Join(", ", secondWrite.Errors.Select(error => error.Message)));
        firstRead.IsSuccess.ShouldBeTrue();
        secondRead.IsSuccess.ShouldBeTrue();
        firstRead.Value.Value.ShouldBe("catalog value");
        secondRead.Value.Value.ShouldBe("shipping value");
        provider.GetRequiredService<CacheProviderRegistry>().Get("catalog").ShouldBeOfType<EntityFrameworkCacheStoreProvider<ModuleContext<CatalogModule>>>();
        provider.GetRequiredService<CacheProviderRegistry>().Get("shipping").ShouldBeOfType<EntityFrameworkCacheStoreProvider<ModuleContext<ShippingModule>>>();
        await AssertCollectionAsync(catalogStore.Options, "PRODUCTS");
        await AssertCollectionAsync(shippingStore.Options, "DELIVERIES");
        catalogStore.Factory.Contexts.ShouldNotBeEmpty();
        shippingStore.Factory.Contexts.ShouldNotBeEmpty();
        catalogStore.Factory.Contexts.ShouldAllBe(context => context.Disposed);
        shippingStore.Factory.Contexts.ShouldAllBe(context => context.Disposed);
        catalogStore.Factory.Contexts.Distinct<ModuleContext<CatalogModule>>(ReferenceEqualityComparer.Instance).Count().ShouldBe(catalogStore.Factory.Contexts.Count);
        shippingStore.Factory.Contexts.Distinct<ModuleContext<ShippingModule>>(ReferenceEqualityComparer.Instance).Count().ShouldBe(shippingStore.Factory.Contexts.Count);
    }

    /// <summary>Repeated default registrations share one kind and every extension/state mutation rejects a finalized owner.</summary>
    [Fact]
    public async Task Fallback_RepeatedSameContext_IsIdempotentAndFreezesAllRegistrationPaths()
    {
        var services = new ServiceCollection();
        var first = services.AddApplicationCaching().WithEntityFrameworkProvider<ModuleContext<CatalogModule>>()
            .WithEntityFrameworkProvider<ModuleContext<CatalogModule>>("catalog");
        var second = services.AddApplicationCaching().WithEntityFrameworkProvider<ModuleContext<CatalogModule>>();
        second.ShouldBeSameAs(first);
        await using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<CacheCatalog>();
        var state = provider.GetRequiredService<EntityFrameworkCacheRegistration>();

        first.IsFinalized.ShouldBeTrue();
        catalog.Registry.ProviderKinds.Values.Count(kind => kind.Alias == "EntityFramework").ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => first.WithEntityFrameworkProvider<ModuleContext<CatalogModule>>());
        Should.Throw<InvalidOperationException>(() => first.WithEntityFrameworkProvider<ModuleContext<ShippingModule>>("late"));
        Should.Throw<InvalidOperationException>(() => state.RegisterFallback<ModuleContext<CatalogModule>>());
        Should.Throw<InvalidOperationException>(() => state.RegisterNamed<ModuleContext<ShippingModule>>("late"));
        catalog.Providers.ContainsKey("late").ShouldBeFalse();
    }

    /// <summary>Conflicting default contexts or factories explain the named API while exact explicit delegate reuse remains idempotent.</summary>
    [Fact]
    public void Fallback_DifferentContextOrFactory_RejectsAmbiguousModuleRouting()
    {
        var services = new ServiceCollection();
        var builder = services.AddApplicationCaching().WithEntityFrameworkProvider<ModuleContext<CatalogModule>>();
        var differentContext = Should.Throw<InvalidOperationException>(() =>
            services.AddApplicationCaching().WithEntityFrameworkProvider<ModuleContext<ShippingModule>>());
        differentContext.Message.ShouldContain("WithEntityFrameworkProvider<TContext>(name, configure)");
        var factory = (IServiceProvider _, string __, CacheProviderOptions ___) =>
            new ModuleContext<CatalogModule>(new DbContextOptionsBuilder<ModuleContext<CatalogModule>>().UseSqlite("Data Source=:memory:").Options);
        var differentFactory = Should.Throw<InvalidOperationException>(() => builder.WithEntityFrameworkProvider(factory));
        differentFactory.Message.ShouldContain("WithEntityFrameworkProvider<TContext>(name, configure)");
        builder.WithEntityFrameworkProvider<ModuleContext<CatalogModule>>();

        var explicitServices = new ServiceCollection();
        explicitServices.AddApplicationCaching().WithEntityFrameworkProvider(factory);
        explicitServices.AddApplicationCaching().WithEntityFrameworkProvider(factory);
        var anotherFactory = (IServiceProvider _, string __, CacheProviderOptions ___) =>
            new ModuleContext<CatalogModule>(new DbContextOptionsBuilder<ModuleContext<CatalogModule>>().UseSqlite("Data Source=:memory:").Options);
        Should.Throw<InvalidOperationException>(() => explicitServices.AddApplicationCaching().WithEntityFrameworkProvider(anotherFactory));
    }

    /// <summary>Named aliases stay unique, force their fluent native kind, and coexist with one fallback for configuration-defined providers.</summary>
    [Fact]
    public async Task NamedAndConfiguredProviders_UseOnlyTheirCodeRegisteredContextRoutes()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Providers:configured:Kind"] = "EntityFramework", ["Providers:configured:Engine"] = "Sqlite"
        }).Build();
        var services = new ServiceCollection();
        var builder = services.AddApplicationCaching().WithConfiguration(configuration)
            .WithEntityFrameworkProvider<ModuleContext<CatalogModule>>("catalog", settings => settings.With(value => value.Kind, "InMemory"))
            .WithEntityFrameworkProvider<ModuleContext<ShippingModule>>();
        Should.Throw<ArgumentException>(() => builder.WithEntityFrameworkProvider<ModuleContext<ShippingModule>>("CATALOG"));
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<CacheProviderRegistry>();

        registry.Get("catalog").ShouldBeOfType<EntityFrameworkCacheStoreProvider<ModuleContext<CatalogModule>>>();
        registry.Get("configured").ShouldBeOfType<EntityFrameworkCacheStoreProvider<ModuleContext<ShippingModule>>>();
    }

    /// <summary>Configuration-defined names cannot silently select a module context when no fallback was approved in code.</summary>
    [Fact]
    public async Task ConfiguredUnboundName_WithoutFallback_IsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Providers:unbound:Kind"] = "EntityFramework", ["Providers:unbound:Engine"] = "Sqlite"
        }).Build();
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithConfiguration(configuration).WithEntityFrameworkProvider<ModuleContext<CatalogModule>>("catalog");
        await using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<CacheProviderRegistry>().Get("unbound"));

        exception.Message.ShouldContain("code-registered context factory");
        exception.Message.ShouldContain("WithEntityFrameworkProvider<TContext>(name, configure)");
    }

    /// <summary>A configuration value cannot replace a code-registered module route with a CLR type name.</summary>
    [Fact]
    public void ConfiguredContextType_IsRejectedBeforeAnyFactoryActivation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Providers:catalog:ContextType"] = typeof(ModuleContext<ShippingModule>).AssemblyQualifiedName
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration)
            .WithEntityFrameworkProvider<ModuleContext<CatalogModule>>("catalog");

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    private static IConfiguration Configuration(string provider) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
    {
        [$"Providers:{provider}:Engine"] = "Sqlite"
    }).Build();

    private static async Task AssertCollectionAsync<TModule>(DbContextOptions<ModuleContext<TModule>> options, string collection)
    {
        await using var context = new ModuleContext<TModule>(options);
        var entry = await context.CacheEntries.SingleAsync();
        JsonSerializer.Deserialize<CacheEntryMetadata>(entry.MetadataJson).Identity.Collection.ShouldBe(collection);
    }

    private sealed class CatalogModule;

    private sealed class ShippingModule;

    private sealed class Store<TModule> : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "module-cache-registration-" + Guid.NewGuid().ToString("N") + ".db");

        private Store()
        {
            this.Options = new DbContextOptionsBuilder<ModuleContext<TModule>>().UseSqlite("Data Source=" + this.path + ";Pooling=False;Default Timeout=5").Options;
            this.Factory = new(this.Options);
        }

        /// <summary>Gets this module's independently configured database options.</summary>
        public DbContextOptions<ModuleContext<TModule>> Options { get; }

        /// <summary>Gets the code-registered factory whose contexts are owned by native cache calls.</summary>
        public TrackingFactory<TModule> Factory { get; }

        /// <summary>Explicitly deploys one temporary consumer schema independently of cache registration.</summary>
        /// <returns>The owned temporary module database fixture.</returns>
        public static async Task<Store<TModule>> CreateAsync()
        {
            var store = new Store<TModule>();
            await using var context = new ModuleContext<TModule>(store.Options);
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            return store;
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            File.Delete(this.path);
            File.Delete(this.path + "-wal");
            File.Delete(this.path + "-shm");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingFactory<TModule>(DbContextOptions<ModuleContext<TModule>> options) : IDbContextFactory<ModuleContext<TModule>>
    {
        /// <summary>Gets the exact contexts created for this module's native cache operations.</summary>
        public List<ModuleContext<TModule>> Contexts { get; } = [];

        /// <inheritdoc />
        public ModuleContext<TModule> CreateDbContext()
        {
            var context = new ModuleContext<TModule>(options);
            this.Contexts.Add(context);
            try
            {
                context.Database.OpenConnection();
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA busy_timeout=5000;";
                command.ExecuteNonQuery();
                return context;
            }
            catch { context.Dispose(); throw; }
        }
    }

    private sealed class ModuleContext<TModule> : ModuleDbContextBase, IApplicationCachingContext
    {
        /// <summary>Creates a module context whose cache model comes from its declared capability.</summary>
        /// <param name="options">This module's native context options.</param>
        public ModuleContext(DbContextOptions<ModuleContext<TModule>> options) : base(options)
        {
            this.ApplyConfigurationsFromAssembly = false;
            this.Schema = "cache";
        }

        /// <summary>Gets whether native context disposal has completed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        public DbSet<CacheStoreEntity> CacheStores { get; set; }

        /// <inheritdoc />
        public DbSet<CacheCollectionEntity> CacheCollections { get; set; }

        /// <inheritdoc />
        public DbSet<CacheContractEntity> CacheContracts { get; set; }

        /// <inheritdoc />
        public DbSet<CacheKeyEntity> CacheKeys { get; set; }

        /// <inheritdoc />
        public DbSet<CacheEntryEntity> CacheEntries { get; set; }

        /// <inheritdoc />
        public DbSet<CachePayloadEntity> CachePayloads { get; set; }

        /// <inheritdoc />
        public DbSet<CacheChunkEntity> CacheChunks { get; set; }

        /// <inheritdoc />
        public DbSet<CacheDimensionEntity> CacheDimensions { get; set; }

        /// <inheritdoc />
        public DbSet<CacheFenceEntity> CacheFences { get; set; }

        /// <inheritdoc />
        public DbSet<CacheOwnershipEntity> CacheOwnerships { get; set; }

        /// <inheritdoc />
        public DbSet<CacheReaderEntity> CacheReaders { get; set; }

        /// <inheritdoc />
        public DbSet<CacheOperationEntity> CacheOperations { get; set; }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            this.Disposed = true;
        }

        /// <inheritdoc />
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            this.Disposed = true;
        }
    }
}