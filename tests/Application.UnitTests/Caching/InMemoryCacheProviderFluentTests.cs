// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>Checks named in-memory provider defaults, bounded capacity options, and configuration precedence.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~InMemoryCacheProviderFluentTests</code></example>
public sealed class InMemoryCacheProviderFluentTests
{
    /// <summary>A short provider declaration offers finite coherent limits and supports ordinary typed writes and reads.</summary>
    [Fact]
    public async Task WithInMemoryProvider_Defaults_SupportsTypedRoundTrip()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplicationCaching().WithInMemoryProvider()
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.UseContract("text").UseProvider("memory"));
        await using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<CacheCatalog>();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");
        var options = catalog.Providers["memory"].Options;
        options.Kind.ShouldBe("InMemory");
        options.MaxResidentBytes.ShouldBe(128 * CacheDefaults.MiB);
        options.MaxStorageBytes.ShouldBe(128 * CacheDefaults.MiB);
        options.MaxEntries.ShouldBe(100000);
        options.MetadataCacheBytes.ShouldBe(16 * CacheDefaults.MiB);
        catalog.Collections["items"].Options.HotEntries.Enabled.ShouldBeFalse();
        catalog.Collections["items"].Options.Compression.Enabled.ShouldBeFalse();

        // Act
        var write = await sut.SetAsync(new("one"), "a cached value");
        var read = await sut.GetAsync("one");

        // Assert
        write.IsSuccess.ShouldBeTrue();
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.ShouldBe("a cached value");
    }

    /// <summary>Native and advanced fluent capacity selectors scale only implicit metadata, independently of repeated capacity calls.</summary>
    /// <param name="selector">The coherent-capacity, resident-only, or advanced presence-aware selector.</param>
    [Theory]
    [InlineData("capacity")]
    [InlineData("resident")]
    [InlineData("advanced")]
    public void WithInMemoryProvider_SmallerResidentCapacity_ScalesImplicitMetadata(string selector)
    {
        var sut = new ServiceCollection().AddApplicationCaching().WithInMemoryProvider("small", provider =>
        {
            provider.Capacity(16 * CacheDefaults.MiB);
            if (selector == "capacity") { provider.Capacity(8 * CacheDefaults.MiB); }
            else if (selector == "resident") { provider.MaxResidentBytes(8 * CacheDefaults.MiB); }
            else { provider.With(value => value.MaxResidentBytes, 8 * CacheDefaults.MiB); }
        });

        var options = sut.BuildCatalog().Providers["small"].Options;

        options.MaxResidentBytes.ShouldBe(8 * CacheDefaults.MiB);
        options.MaxStorageBytes.ShouldBe((selector == "capacity" ? 8 : 16) * CacheDefaults.MiB);
        options.MetadataCacheBytes.ShouldBe(CacheDefaults.MiB);
    }

    /// <summary>Explicit metadata survives either fluent call order.</summary>
    /// <param name="metadataFirst">Whether the explicit metadata is declared before capacity.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithInMemoryProvider_ExplicitMetadata_IsPreserved(bool metadataFirst)
    {
        var sut = new ServiceCollection().AddApplicationCaching().WithInMemoryProvider("small", provider =>
        {
            if (metadataFirst) { provider.MetadataCacheBytes(2 * CacheDefaults.MiB); }

            provider.Capacity(8 * CacheDefaults.MiB);
            if (!metadataFirst) { provider.With(value => value.MetadataCacheBytes, 2 * CacheDefaults.MiB); }
        });

        sut.BuildCatalog().Providers["small"].Options.MetadataCacheBytes.ShouldBe(2 * CacheDefaults.MiB);
    }

    /// <summary>Host configuration overrides the typed provider's explicit native policies.</summary>
    [Fact]
    public void WithInMemoryProvider_Configuration_WinsOverTypedPolicies()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Providers:lookups:MaxEntries"] = "2000",
            ["Providers:lookups:MaxStorageBytes"] = (32 * CacheDefaults.MiB).ToString(),
            ["Providers:lookups:MetadataCacheBytes"] = (2 * CacheDefaults.MiB).ToString()
        }).Build();
        var sut = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration).WithInMemoryProvider("lookups", provider => provider
            .Capacity(16 * CacheDefaults.MiB).MaxEntries(1000).MetadataReserveRatio(0.08).AttemptTimeout(TimeSpan.FromSeconds(7)));

        var options = sut.BuildCatalog().Providers["lookups"].Options;

        options.MaxEntries.ShouldBe(2000);
        options.MaxStorageBytes.ShouldBe(32 * CacheDefaults.MiB);
        options.MetadataCacheBytes.ShouldBe(2 * CacheDefaults.MiB);
        options.MetadataReserveRatio.ShouldBe(0.08);
        options.AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(7));
    }

    /// <summary>Aliases stay unique and a finalized catalog rejects further declarations while retained typed builders cannot change its snapshot.</summary>
    [Fact]
    public void WithInMemoryProvider_DuplicateAndLateDeclarations_AreRejected()
    {
        InMemoryCacheProviderOptionsBuilder retained = null;
        var sut = new ServiceCollection().AddApplicationCaching().WithInMemoryProvider("memory", options => retained = options);
        Should.Throw<ArgumentException>(() => sut.WithInMemoryProvider("MEMORY"));
        var catalog = sut.BuildCatalog();

        retained.Capacity(8 * CacheDefaults.MiB);

        catalog.Providers["memory"].Options.MaxResidentBytes.ShouldBe(128 * CacheDefaults.MiB);
        Should.Throw<InvalidOperationException>(() => sut.WithInMemoryProvider("late"));
    }
}