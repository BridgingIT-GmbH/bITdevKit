// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Checks modular startup contributions, configuration precedence, lazy activation, and catalog finalization.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheModuleCompositionTests</code></example>
public sealed class CacheModuleCompositionTests
{
    /// <summary>Independent modules share one builder and runtime registration while retaining isolated, usable collection identities.</summary>
    [Fact]
    public async Task AddApplicationCaching_TwoModules_ReuseOneRuntimeAndBothCollections()
    {
        // Arrange
        var services = CreateServices();
        var first = AddModule(services, "catalog");
        var second = AddModule(services, "orders");

        // Act
        second.ShouldBeSameAs(first);
        first.IsFinalized.ShouldBeFalse();
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>();
        var catalog = sut.GetCollection<string>("catalog.items");
        var orders = sut.GetCollection<string>("orders.items");
        (await catalog.SetAsync(new("same-key"), "catalog value")).IsSuccess.ShouldBeTrue();
        (await orders.SetAsync(new("same-key"), "orders value")).IsSuccess.ShouldBeTrue();

        // Assert
        (await catalog.GetAsync("same-key")).Value.Value.ShouldBe("catalog value");
        (await orders.GetAsync("same-key")).Value.Value.ShouldBe("orders value");
        first.IsFinalized.ShouldBeTrue();
        provider.GetRequiredService<ApplicationCachingBuilderContext>().ShouldBeSameAs(first);
        provider.GetRequiredService<CacheCatalog>().Collections.Count.ShouldBe(2);
        foreach (var type in new[]
        {
            typeof(ApplicationCachingBuilderContext), typeof(CacheCatalog), typeof(IApplicationCache), typeof(ICacheCollectionFactory),
            typeof(CacheProviderRegistry), typeof(CacheProviderInitialization), typeof(CacheRuntimeResources), typeof(CacheRuntimeTasks),
            typeof(CacheDiagnostics), typeof(CacheMetricHistory), typeof(CacheMaintenanceService), typeof(CacheRefreshService)
        })
        {
            services.Where(descriptor => descriptor.ServiceType == type).ShouldHaveSingleItem().Lifetime.ShouldBe(ServiceLifetime.Singleton);
        }

        var workers = provider.GetServices<IHostedService>().ToArray();
        workers.OfType<CacheMaintenanceService>().ShouldHaveSingleItem().ShouldBeSameAs(provider.GetRequiredService<CacheMaintenanceService>());
        workers.OfType<CacheRefreshService>().ShouldHaveSingleItem().ShouldBeSameAs(provider.GetRequiredService<CacheRefreshService>());
    }

    /// <summary>Separate host containers keep different builders, catalogs, resource accounts, and in-memory entries.</summary>
    [Fact]
    public async Task AddApplicationCaching_IndependentContainers_DoNotShareCompositionOrEntries()
    {
        // Arrange
        var firstServices = CreateServices();
        var secondServices = CreateServices();
        var first = AddModule(firstServices, "catalog");
        var second = AddModule(secondServices, "catalog");
        await using var firstProvider = firstServices.BuildServiceProvider();
        var firstCache = firstProvider.GetRequiredService<IApplicationCache>();
        (await firstCache.GetCollection<string>("catalog.items").SetAsync(new("key"), "first host")).IsSuccess.ShouldBeTrue();

        // Act
        second.IsFinalized.ShouldBeFalse();
        second.WithCollection<string>("catalog.extra", collection => collection.UseContract("catalog.text").UseProvider("catalog.memory"));
        await using var secondProvider = secondServices.BuildServiceProvider();
        var sut = secondProvider.GetRequiredService<IApplicationCache>();
        var missing = await sut.GetCollection<string>("catalog.items").GetAsync("key");

        // Assert
        second.ShouldNotBeSameAs(first);
        secondProvider.GetRequiredService<CacheCatalog>().ShouldNotBeSameAs(firstProvider.GetRequiredService<CacheCatalog>());
        secondProvider.GetRequiredService<CacheRuntimeResources>().ShouldNotBeSameAs(firstProvider.GetRequiredService<CacheRuntimeResources>());
        missing.IsSuccess.ShouldBeTrue();
        missing.Value.Found.ShouldBeFalse();
        firstProvider.GetRequiredService<CacheCatalog>().Collections.Count.ShouldBe(1);
        secondProvider.GetRequiredService<CacheCatalog>().Collections.Count.ShouldBe(2);
    }

    /// <summary>Later present values override earlier values while distinct module provider and collection subtrees remain available.</summary>
    [Fact]
    public void Configuration_MultipleModuleSources_MergeDistinctSubtreesAndApplyLaterScalars()
    {
        // Arrange
        using var catalogConfiguration = Json("""
            {
              "Namespace": "first-host",
              "Diagnostics": { "HistoryEnabled": true },
              "Providers": { "catalog.memory": { "Kind": "InMemory", "MaxEntries": 111 } },
              "Defaults": { "Expiration": { "AbsoluteTtl": "00:20:00" } },
              "Collections": {
                "catalog.items": { "Expiration": { "AbsoluteTtl": "00:30:00" } },
                "catalog.extra": { "Contract": "catalog.text", "Provider": "catalog.memory" }
              }
            }
            """);
        using var ordersConfiguration = Json("""
            {
              "Namespace": "later-host",
              "Diagnostics": { "HistoryEnabled": false },
              "Providers": {
                "catalog.memory": { "MaxEntries": 112 },
                "orders.memory": { "Kind": "InMemory", "MaxEntries": 222 }
              },
              "Defaults": { "Expiration": { "AbsoluteTtl": "00:05:00" } },
              "Collections": { "orders.items": { "Limits": { "MaxEntries": 81 } } }
            }
            """);
        var services = CreateServices();
        var first = services.AddApplicationCaching().WithConfiguration(catalogConfiguration).WithValueContract<string>("catalog.text")
            .WithCollection<string>("catalog.items", collection => collection.UseContract("catalog.text").UseProvider("catalog.memory"));
        var second = services.AddApplicationCaching().WithConfiguration(ordersConfiguration).WithValueContract<string>("orders.text")
            .WithCollection<string>("orders.items", collection => collection.UseContract("orders.text").UseProvider("orders.memory"));

        // Act
        var sut = second.BuildCatalog();

        // Assert
        second.ShouldBeSameAs(first);
        sut.Options.Namespace.ShouldBe("later-host");
        sut.Options.Diagnostics.HistoryEnabled.ShouldBeFalse();
        sut.Providers.Count.ShouldBe(2);
        sut.Providers["catalog.memory"].Options.MaxEntries.ShouldBe(112);
        sut.Providers["orders.memory"].Options.MaxEntries.ShouldBe(222);
        sut.Collections.Count.ShouldBe(3);
        sut.Collections["catalog.items"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(30));
        sut.Collections["catalog.extra"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(5));
        sut.Collections["orders.items"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(5));
        sut.Collections["orders.items"].Options.Limits.MaxEntries.ShouldBe(81);
    }

    /// <summary>Collection-specific configuration has higher specificity than global defaults regardless of source registration order.</summary>
    /// <param name="specificFirst">Whether the specific collection source is registered before the global defaults source.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Configuration_CollectionSpecificSettings_OutrankEveryGlobalConfigurationSource(bool specificFirst)
    {
        // Arrange
        using var specific = Json("""
            { "Collections": { "catalog.items": { "Expiration": { "AbsoluteTtl": "00:17:00" },
              "Behaviors": { "SourceExecution": ["logging", "metrics"] } } } }
            """);
        using var defaults = Json("""
            { "Defaults": { "Expiration": { "AbsoluteTtl": "00:05:00" },
              "Behaviors": { "SourceExecution": ["profiling"] } } }
            """);
        var services = CreateServices();
        var builder = services.AddApplicationCaching().WithConfiguration(specificFirst ? specific : defaults)
            .WithValueContract<string>("catalog.text")
            .Defaults(collection => collection.ExpireAfter(TimeSpan.FromMinutes(2)))
            .WithCollection<string>("catalog.items", collection => collection.UseContract("catalog.text").ExpireAfter(TimeSpan.FromMinutes(3)))
            .WithCollection<string>("catalog.other", collection => collection.UseContract("catalog.text"));
        builder.WithConfiguration(specificFirst ? defaults : specific).ShouldBeSameAs(builder);

        // Act
        var sut = builder.BuildCatalog();

        // Assert
        sut.Collections["catalog.items"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(17));
        sut.Collections["catalog.items"].Options.Behaviors.SourceExecution.ShouldBe(new[] { "logging", "metrics" });
        sut.Collections["catalog.other"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(5));
        sut.Collections["catalog.other"].Options.Behaviors.SourceExecution.ShouldBe(new[] { "profiling" });
    }

    /// <summary>Explicit collection fluent values override global fluent defaults even when another module supplies those defaults later.</summary>
    /// <param name="collectionFirst">Whether the collection is declared before global fluent defaults.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FluentDefaults_ModuleOrder_DoesNotOverrideExplicitCollectionValues(bool collectionFirst)
    {
        // Arrange
        var services = CreateServices();
        var builder = services.AddApplicationCaching().WithValueContract<string>("catalog.text");
        if (collectionFirst) { AddCollection(); }

        services.AddApplicationCaching().Defaults(collection => collection.ExpireAfter(TimeSpan.FromMinutes(2))
            .With(value => value.Limits.MaxEntries, 90));
        if (!collectionFirst) { AddCollection(); }

        // Act
        var sut = builder.BuildCatalog().Collections["catalog.items"].Options;

        // Assert
        sut.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(7));
        sut.Limits.MaxEntries.ShouldBe(80);

        void AddCollection() => builder.WithCollection<string>("catalog.items", collection => collection.UseContract("catalog.text")
            .ExpireAfter(TimeSpan.FromMinutes(7)).With(value => value.Limits.MaxEntries, 80));
    }

    /// <summary>Each configuration source replaces an entire ordered list, including an explicit empty list.</summary>
    /// <param name="empty">Whether the later source explicitly removes every configured source behavior.</param>
    /// <param name="throughRegistration">Whether the later source arrives through another module's registration call.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Configuration_LaterOrderedList_ReplacesTheWholeEarlierList(bool empty, bool throughRegistration)
    {
        // Arrange
        using var first = Json("""
            { "Defaults": { "Behaviors": { "SourceExecution": ["logging", "profiling", "metrics"] } } }
            """);
        using var later = Json(empty ?
            """{ "Defaults": { "Behaviors": { "SourceExecution": [] } } }""" :
            """{ "Defaults": { "Behaviors": { "SourceExecution": ["metrics", "logging"] } } }""");
        var services = CreateServices();
        var builder = services.AddApplicationCaching().WithConfiguration(first).WithValueContract<string>("catalog.text")
            .WithCollection<string>("catalog.items", collection => collection.UseContract("catalog.text"));

        // Act
        var same = throughRegistration ? services.AddApplicationCaching().WithConfiguration(later) : builder.WithConfiguration(later);
        var sut = same.BuildCatalog().Collections["catalog.items"].Options.Behaviors.SourceExecution;

        // Assert
        same.ShouldBeSameAs(builder);
        if (empty) { sut.ShouldBeEmpty(); }
        else { sut.ShouldBe(new[] { "metrics", "logging" }); }
    }

    /// <summary>Assembling and finalizing modular startup configuration does not construct providers or scoped sources.</summary>
    [Fact]
    public async Task Finalization_ModuleContributions_DoesNotActivateProvidersOrSources()
    {
        // Arrange
        var services = CreateServices();
        var providers = 0;
        var sourceConstructions = 0;
        var sourceCalls = 0;
        services.AddScoped(_ =>
        {
            Interlocked.Increment(ref sourceConstructions);
            return new DeferredLoader(() => Interlocked.Increment(ref sourceCalls));
        });
        var builder = services.AddApplicationCaching()
            .WithProviderKind("startup.probe", InMemoryCacheStoreProvider.DefaultCapabilities, (_, name, options) =>
            {
                Interlocked.Increment(ref providers);
                return new InMemoryCacheStoreProvider(name, options);
            })
            .WithProvider("startup.memory", provider => provider.With(value => value.Kind, "startup.probe"))
            .WithValueContract<string>("catalog.text")
            .WithRefreshLoader<string, DeferredLoader>("catalog.source")
            .WithCollection<string>("catalog.items", collection => collection.UseContract("catalog.text").UseProvider("startup.memory")
                .With(value => value.Refresh.Enabled, true).With(value => value.Refresh.Loader, "catalog.source"));
        services.AddApplicationCaching().WithValueContract<int>("orders.number")
            .WithCollection<int>("orders.items", collection => collection.UseContract("orders.number").UseProvider("startup.memory"));

        // Act
        builder.IsFinalized.ShouldBeFalse();
        var catalog = builder.BuildCatalog();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var sut = provider.GetRequiredService<CacheCatalog>();

        // Assert
        sut.ShouldBeSameAs(catalog);
        builder.IsFinalized.ShouldBeTrue();
        providers.ShouldBe(0);
        sourceConstructions.ShouldBe(0);
        sourceCalls.ShouldBe(0);
    }

    /// <summary>Once the catalog is finalized, fluent registration and additional configuration cannot mutate its startup state.</summary>
    [Fact]
    public void Finalization_LateModuleOrConfiguration_RejectsEveryFurtherContribution()
    {
        // Arrange
        var services = CreateServices();
        var builder = AddModule(services, "catalog");
        using var configuration = Json("""{ "Enabled": false }""");
        var catalog = builder.BuildCatalog();

        // Act / Assert
        builder.IsFinalized.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => builder.WithValueContract<int>("late.number"));
        Should.Throw<InvalidOperationException>(() => builder.WithCollection<string>("late.items"));
        Should.Throw<InvalidOperationException>(() => builder.WithProvider("late.memory", provider => provider.With(value => value.Kind, "InMemory")));
        Should.Throw<InvalidOperationException>(() => builder.WithConfiguration(configuration));
        Should.Throw<InvalidOperationException>(() => services.AddApplicationCaching().WithConfiguration(configuration));
        Should.Throw<InvalidOperationException>(() => services.AddApplicationCaching());
        builder.BuildCatalog().ShouldBeSameAs(catalog);
        catalog.Options.Enabled.ShouldBeTrue();
        catalog.Collections.Count.ShouldBe(1);
    }

    /// <summary>Repeated feature registration permits module contributions without silently replacing normalized code aliases.</summary>
    /// <param name="aliasKind">The registry whose duplicate declaration must be rejected.</param>
    [Theory]
    [InlineData("contract")]
    [InlineData("provider")]
    [InlineData("collection")]
    [InlineData("provider-kind")]
    public void Modules_DuplicateNormalizedCodeAlias_RejectsReplacement(string aliasKind)
    {
        // Arrange
        var services = CreateServices();
        var first = AddModule(services, "catalog");
        var second = services.AddApplicationCaching();
        Action duplicate = aliasKind switch
        {
            "contract" => () => second.WithValueContract<int>("CATALOG.TEXT"),
            "provider" => () => second.WithProvider("CATALOG.MEMORY", provider => provider.With(value => value.Kind, "InMemory")),
            "collection" => () => second.WithCollection<string>("CATALOG.ITEMS", collection => collection.UseContract("catalog.text")),
            _ => () => second.WithProviderKind("INMEMORY", InMemoryCacheStoreProvider.DefaultCapabilities,
                (_, name, options) => new InMemoryCacheStoreProvider(name, options))
        };

        // Act / Assert
        second.ShouldBeSameAs(first);
        Should.Throw<ArgumentException>(duplicate);
        var catalog = first.BuildCatalog();
        catalog.Collections.Count.ShouldBe(1);
        catalog.Collections["catalog.items"].Contract.ValueType.ShouldBe(typeof(string));
        catalog.Collections["catalog.items"].Provider.Name.ShouldBe("CATALOG.MEMORY");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        return services;
    }

    private static ApplicationCachingBuilderContext AddModule(IServiceCollection services, string name) => services.AddApplicationCaching()
        .WithValueContract<string>(name + ".text")
        .WithProvider(name + ".memory", provider => provider.With(value => value.Kind, "InMemory"))
        .WithCollection<string>(name + ".items", collection => collection.UseContract(name + ".text").UseProvider(name + ".memory"));

    private static ConfigurationRoot Json(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(stream).Build();
    }

    /// <summary>Records source activation only when the cache owns an actual refresh execution.</summary>
    /// <param name="onLoad">The test's source-execution counter.</param>
    private sealed class DeferredLoader(Action onLoad) : ICacheRefreshLoader<string>
    {
        /// <inheritdoc />
        public ValueTask<Result<CacheLoadResult<string>>> LoadAsync(CacheLoadContext context, CancellationToken cancellationToken = default)
        {
            onLoad();
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("source")));
        }
    }
}