// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises short typed registration, conventional configuration, and dependent finite defaults.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheFluentRegistrationTests</code></example>
public sealed class CacheFluentRegistrationTests
{
    /// <summary>One typed collection declaration supplies its JSON contract and a bounded memory provider.</summary>
    [Fact]
    public async Task MinimalSetup_UsesJsonAndMemoryWithoutAdditionalDeclarations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching(cache => cache.WithCollection<Card>("cards"));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<Card>("cards");

        var stored = await sut.SetAsync(new("one"), new("One"));
        var read = await sut.GetAsync("one");

        stored.IsSuccess.ShouldBeTrue();
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.ShouldBe(new Card("One"));
        var catalog = provider.GetRequiredService<CacheCatalog>();
        catalog.Collections["cards"].Contract.Alias.ShouldBe("CARDS");
        catalog.Collections["cards"].Options.Codec.ShouldBe("json");
        catalog.Collections["cards"].Options.HotEntries.Enabled.ShouldBeFalse();
        catalog.Collections["cards"].Options.Compression.Enabled.ShouldBeFalse();
        catalog.Providers["memory"].Options.MaxStorageBytes.ShouldBe(128 * CacheDefaults.MiB);
    }

    /// <summary>Module contributions with the same CLR type get stable independent default contracts.</summary>
    [Fact]
    public void Modules_DefaultContracts_AreIndependentAndAvailableToConfiguredCollections()
    {
        var configuration = Configuration(new()
        {
            ["Collections:extra:Contract"] = "orders",
            ["Collections:copy:Contract"] = "orders"
        });
        var services = new ServiceCollection();
        var builder = services.AddApplicationCaching().WithConfiguration(configuration).WithCollection<string>("copy", collection => collection.UseContract("orders"));
        services.AddApplicationCaching().WithCollection<string>("cards");
        services.AddApplicationCaching().WithCollection<string>("orders");

        var sut = builder.BuildCatalog();

        sut.Collections["cards"].Contract.Alias.ShouldBe("CARDS");
        sut.Collections["orders"].Contract.Alias.ShouldBe("ORDERS");
        sut.Collections["copy"].Contract.ShouldBeSameAs(sut.Collections["orders"].Contract);
        sut.Collections["extra"].Contract.ShouldBeSameAs(sut.Collections["orders"].Contract);
        builder.Components.Contracts.ShouldBeEmpty();
        sut.Registry.Contracts.Count.ShouldBe(2);
    }

    /// <summary>A code-declared collection's default contract can also be selected explicitly by its documented alias.</summary>
    /// <param name="fromConfiguration">Whether the alias is selected through configuration instead of fluent setup.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultContract_ExplicitSelfAlias_UsesTheCodeDeclaredType(bool fromConfiguration)
    {
        var configuration = Configuration(new());
        if (fromConfiguration) { configuration["Collections:cards:Contract"] = "cards"; }

        var sut = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration).WithCollection<Card>("cards", collection =>
        {
            if (!fromConfiguration) { collection.UseContract("cards"); }
        }).BuildCatalog();

        sut.Collections["cards"].Contract.Alias.ShouldBe("CARDS");
        sut.Collections["cards"].Contract.ValueType.ShouldBe(typeof(Card));
    }

    /// <summary>The convention resolves the final host configuration without building a temporary service provider.</summary>
    [Fact]
    public void Convention_ResolvesHostRootAtFinalizationAndFreezesTheSnapshot()
    {
        var configuration = Configuration(new() { ["Caching:Application:Namespace"] = "configured" });
        var resolutions = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(_ => { resolutions++; return configuration; });
        var builder = services.AddApplicationCaching(cache => cache.Namespace("fluent").WithCollection<Card>("cards"));
        resolutions.ShouldBe(0);
        using var provider = services.BuildServiceProvider();

        var sut = provider.GetRequiredService<CacheCatalog>();
        configuration["Caching:Application:Namespace"] = "changed";

        resolutions.ShouldBe(1);
        sut.Options.Namespace.ShouldBe("configured");
        builder.BuildCatalog().ShouldBeSameAs(sut);
        Should.Throw<InvalidOperationException>(() => services.AddApplicationCaching("later", cache => cache.Namespace("late")));
    }

    /// <summary>Dotted and colon-separated custom paths select the intended section instead of the conventional section.</summary>
    /// <param name="path">The caller's preferred section separator.</param>
    [Theory]
    [InlineData("Modules.Catalog.Caching")]
    [InlineData("Modules:Catalog:Caching")]
    public void CustomSection_UsesTheSelectedHostSection(string path)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration(new()
        {
            ["Caching:Application:Namespace"] = "ignored",
            ["Modules:Catalog:Caching:Namespace"] = "catalog"
        }));

        var sut = services.AddApplicationCaching(path, cache => cache.WithCollection<Card>("cards")).BuildCatalog();

        sut.Options.Namespace.ShouldBe("catalog");
    }

    /// <summary>Supplying or omitting a setup callback never changes root or explicit section selection.</summary>
    /// <param name="section">Whether the caller passes an explicit section instead of the root.</param>
    /// <param name="withCallback">Whether the caller supplies a fluent setup callback.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SuppliedConfiguration_RootOrExplicitSection_IsPresenceAware(bool section, bool withCallback)
    {
        var configuration = Configuration(new()
        {
            ["Caching:Application:Namespace"] = "conventional",
            ["Custom:Namespace"] = "explicit"
        });
        IConfiguration source = section ? configuration.GetSection("Custom") : configuration;

        var services = new ServiceCollection();
        var builder = withCallback
            ? services.AddApplicationCaching(source, cache => cache.Namespace("fluent"))
            : services.AddApplicationCaching(source).Namespace("fluent");
        var sut = builder.WithCollection<Card>("cards").BuildCatalog();

        sut.Options.Namespace.ShouldBe(section ? "explicit" : "conventional");
    }

    /// <summary>Omitted child ceilings fit a smaller provider without enabling dormant retention or changing explicit settings.</summary>
    [Fact]
    public void SmallerCapacity_FitsDependentDefaultsWithoutRequiringDormantHotSettings()
    {
        var sut = new ServiceCollection().AddApplicationCaching(cache => cache
            .WithInMemoryProvider("small", provider => provider.Capacity(2 * CacheDefaults.MiB).MaxEntries(20))
            .WithCollection<Card>("cards", collection => collection.UseProvider("small"))).BuildCatalog();
        var limits = sut.Collections["cards"].Options;

        limits.Limits.MaxStorageBytes.ShouldBe(2 * CacheDefaults.MiB);
        limits.Limits.MaxResidentBytes.ShouldBe(2 * CacheDefaults.MiB);
        limits.Limits.MaxEntries.ShouldBe(20);
        limits.Limits.MaxEncodedBytes.ShouldBe(2 * CacheDefaults.MiB);
        limits.HotEntries.MaxResidentBytes.ShouldBe(2 * CacheDefaults.MiB);
        limits.HotEntries.Enabled.ShouldBeFalse();
        sut.Collections["cards"].Provenance.ContainsKey("HotEntries.MaxResidentBytes").ShouldBeFalse();
    }

    /// <summary>Configuration-only memory providers inherit the same finite defaults and fit a smaller resident capacity.</summary>
    [Fact]
    public void ConfiguredMemoryProvider_OmittedSettings_UsesCoherentFiniteDefaults()
    {
        var configuration = Configuration(new()
        {
            ["Providers:small:Kind"] = "InMemory",
            ["Providers:small:MaxResidentBytes"] = (8 * CacheDefaults.MiB).ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        var sut = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration)
            .WithCollection<Card>("cards", collection => collection.UseProvider("small")).BuildCatalog();

        sut.Providers["small"].Options.MaxStorageBytes.ShouldBe(8 * CacheDefaults.MiB);
        sut.Providers["small"].Options.MaxEntries.ShouldBe(100000);
        sut.Providers["small"].Options.MetadataCacheBytes.ShouldBe(CacheDefaults.MiB);
        sut.Collections["cards"].Options.HotEntries.Enabled.ShouldBeFalse();
    }

    /// <summary>Explicit invalid child ceilings remain errors rather than being silently reduced.</summary>
    [Fact]
    public void ExplicitOversizedChild_IsRejected()
    {
        var builder = new ServiceCollection().AddApplicationCaching(cache => cache.WithCollection<Card>("cards", collection => collection
            .Limits(limits => limits.MaxStorageBytes(CacheDefaults.MiB).MaxPayloadBytes(2 * CacheDefaults.MiB))));

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    /// <summary>Short history retention inherits a fitting chart window while explicit incompatible chart windows remain errors.</summary>
    [Fact]
    public void HistoryRetention_OmittedChildTiming_FitsTheRequestedHistory()
    {
        var sut = new ServiceCollection().AddApplicationCaching(cache => cache
            .Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromMinutes(2)))
            .WithCollection<Card>("cards")).BuildCatalog();

        sut.Options.Diagnostics.HistoryEnabled.ShouldBeTrue();
        sut.Options.Diagnostics.ChartWindow.ShouldBe(TimeSpan.FromMinutes(2));
        sut.Options.Diagnostics.SamplingInterval.ShouldBe(TimeSpan.FromSeconds(5));
        var invalid = new ServiceCollection().AddApplicationCaching(cache => cache.Diagnostics(diagnostics => diagnostics
            .HistoryRetention(TimeSpan.FromMinutes(2)).ChartWindow(TimeSpan.FromMinutes(5))));
        Should.Throw<ArgumentException>(() => invalid.BuildCatalog());
    }

    /// <summary>Automatic JSON does not resolve ambiguous custom contracts or activate configuration-supplied CLR types.</summary>
    [Fact]
    public void ExplicitContractSelection_RemainsRequiredForAmbiguousOrConfiguredTypes()
    {
        var builder = new ServiceCollection().AddApplicationCaching(cache => cache.WithValueContract<Card>("one")
            .WithValueContract<Card>("two").WithCollection<Card>("cards"));
        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
        var configuration = Configuration(new() { ["Collections:unknown:Contract"] = "not-approved" });
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration).BuildCatalog());
    }

    private static IConfigurationRoot Configuration(Dictionary<string, string> settings) => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    /// <summary>The code-declared immutable lookup value used by short registration examples.</summary>
    /// <param name="Name">The fictional display label.</param>
    public sealed record Card(string Name);
}