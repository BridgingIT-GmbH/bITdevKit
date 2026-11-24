namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class CacheRegistrationTests
{
    private static readonly CacheProviderCapabilities MemoryCapabilities = new(false, false, true, true, false, true);

    [Fact]
    public void Catalog_MixedSetup_HonorsEnvironmentAndAddsConfiguredCollections()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "Providers": { "memory": { "Kind": "InMemory" } },
              "Collections": {
                "product-cards": { "Contract": "product-card-v1", "Provider": "memory",
                  "Expiration": { "AbsoluteTtl": "00:30:00" } },
                "product-cards-preview": { "Contract": "product-card-v1", "Provider": "memory",
                  "Expiration": { "AbsoluteTtl": "00:02:00" } }
              }
            }
            """));
        var configuration = new ConfigurationBuilder().AddJsonStream(input).Build();
        var services = new ServiceCollection();
        var builder = services.AddApplicationCaching().WithConfiguration(configuration);
        RegisterMemory(builder);
        builder.WithValueContract<ProductCard>("product-card-v1")
            .WithCollection<ProductCard>("product-cards", c => c.UseContract("product-card-v1")
                .UseProvider("memory").WithScalarDimension<string>("catalog")
                .ExpireAfter(TimeSpan.FromMinutes(5)));
        using var provider = services.BuildServiceProvider();

        var result = provider.GetRequiredService<CacheCatalog>();

        result.Collections.Count.ShouldBe(2);
        result.Collections["product-cards"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(30));
        result.Collections["product-cards"].Options.Dimensions.Single().Name.ShouldBe("CATALOG");
        result.Collections["product-cards-preview"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(2));
        result.Collections["product-cards-preview"].Contract.ValueType.ShouldBe(typeof(ProductCard));
        result.Collections["product-cards"].Provenance["Expiration.AbsoluteTtl"].ShouldBe("Configuration");
        builder.BuildCatalog().ShouldBeSameAs(result);
        Should.Throw<InvalidOperationException>(() => builder.WithBinaryContract("late"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Catalog_FivePrecedenceLevels_AreIndependentOfCodeCallOrder(bool reverse)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Defaults:Expiration:AbsoluteTtl"] = "00:20:00",
            ["Collections:other:Contract"] = "card",
            ["Collections:other:Expiration:AbsoluteTtl"] = "00:30:00",
            ["Collections:code:HotEntries:Enabled"] = "false"
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration);
        RegisterMemory(builder);
        if (reverse)
        {
            builder.WithCollection<ProductCard>("code", c => c.ExpireAfter(TimeSpan.FromMinutes(5))
                .WithHotEntries(h => h.WithMaxEntries(12)));
        }

        builder.Defaults(c => c.ExpireAfter(TimeSpan.FromMinutes(2))
            .With(x => x.HotEntries.MaxIdleTime, TimeSpan.FromSeconds(12)));
        builder.WithValueContract<ProductCard>("card");
        if (!reverse)
        {
            builder.WithCollection<ProductCard>("code", c => c.ExpireAfter(TimeSpan.FromMinutes(5))
                .WithHotEntries(h => h.WithMaxEntries(12)));
        }

        var result = builder.BuildCatalog();

        result.Collections["code"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(20));
        result.Collections["other"].Options.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(30));
        result.Collections["code"].Options.HotEntries.Enabled.ShouldBeFalse();
        result.Collections["code"].Options.HotEntries.MaxEntries.ShouldBe(12);
        result.Collections["code"].Options.HotEntries.MaxIdleTime.ShouldBe(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void Catalog_SecretsAndCallerLists_AreNotExposedAsMutableEffectiveSettings()
    {
        var aliases = new[] { "observer" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Providers:memory:ConnectionString"] = "secret-value"
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration);
        RegisterMemory(builder);
        builder.WithValueContract<ProductCard>("card")
            .WithBehavior<object>("observer", CacheBehaviorStage.CollectionOperations)
            .WithCollection<ProductCard>("cards", c => c.With(x => x.Behaviors.CollectionOperations, aliases));
        var result = builder.BuildCatalog();
        aliases[0] = "changed";

        result.Providers["memory"].Options.ConnectionString.ShouldBe("[redacted]");
        result.Collections["cards"].Options.Behaviors.CollectionOperations.Single().ShouldBe("observer");
        result.Collections["cards"].Provenance.Values.ShouldNotContain("secret-value");
    }

    [Fact]
    public void Registration_MinimalCaching_RemainsIndependentlyRegistered()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching().WithInMemoryProvider();
        var minimal = services.Single(d => d.ServiceType == typeof(ICacheProvider));
        var builder = services.AddApplicationCaching();
        RegisterMemory(builder);
        builder.WithValueContract<ProductCard>("card").WithCollection<ProductCard>("cards");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<CacheCatalog>().Collections.Count.ShouldBe(1);
        services.Single(d => d.ServiceType == typeof(ICacheProvider)).ShouldBeSameAs(minimal);
        provider.GetRequiredService<ICacheProvider>().ShouldNotBeNull();
        services.Any(d => d.ServiceType.FullName.Contains("KeyValue", StringComparison.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Limits:MaxEntries", "0")]
    [InlineData("Expiration:AbsoluteTtl", "00:00:00")]
    [InlineData("Expiration:JitterRatio", "1")]
    [InlineData("Loading:CoordinationScope", "SharedStore")]
    [InlineData("HotEntries:Validation", "999")]
    [InlineData("HotEntries:Representation", "Decoded")]
    [InlineData("Compression:Enabled", "true")]
    [InlineData("Refresh:Enabled", "true")]
    [InlineData("Loading:SourceResilience", "unregistered")]
    public void Catalog_UnsafeOrUnsupportedCombinations_FailStartup(string setting, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Collections:cards:Contract"] = "card",
            ["Collections:cards:HotEntries:Enabled"] = "true",
            ["Collections:cards:" + setting] = value
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration);
        RegisterMemory(builder);
        builder.WithValueContract<ProductCard>("card");

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    [Fact]
    public void Catalog_UnknownAliasesAndDuplicateNormalizedNames_FailStartup()
    {
        var builder = new ServiceCollection().AddApplicationCaching();
        RegisterMemory(builder);
        builder.WithValueContract<ProductCard>("card").WithCollection<ProductCard>("cards");
        Should.Throw<ArgumentException>(() => builder.WithCollection<ProductCard>("CARDS"));
        Should.Throw<ArgumentException>(() => builder.WithValueContract<ProductCard>("CARD"));
        Should.Throw<ArgumentException>(() => builder.WithCollection<ProductCard>("other", c => c.UseContract("missing")).BuildCatalog());
    }

    [Fact]
    public void Catalog_UnavailablePresentationCapability_FailsWithoutInstallingTransport()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Endpoints:Enabled"] = "true"
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration);
        RegisterMemory(builder);

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    private static void RegisterMemory(ApplicationCachingBuilderContext builder) => builder.WithProviderKind("CatalogTestMemory",
        MemoryCapabilities, (_, _, _) => throw new InvalidOperationException("Catalog finalization must not instantiate providers."))
        .WithProvider("memory", p => p.With(x => x.Kind, "CatalogTestMemory"));

    public sealed record ProductCard(string Code, decimal Price);
}