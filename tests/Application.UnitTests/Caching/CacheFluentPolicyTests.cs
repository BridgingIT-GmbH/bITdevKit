// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.IO.Compression;
using System.Text;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises typed policy composition through finalized options and usable public cache collections.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheFluentPolicyTests</code></example>
public sealed class CacheFluentPolicyTests
{
    /// <summary>A short typed configuration enables history explicitly and produces a usable bounded collection.</summary>
    [Fact]
    public async Task TypedPolicies_FinalizedCatalogAndPublicAccess_UseTheRequestedSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplicationCaching()
            .WithOptions(options => options.Namespace("catalog")
                .Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)))
                .Limits(limits => limits.MaxActiveSources(16).MaxTransientBytes(ByteSize.Megabytes(32)))
                .BackgroundServices(background => background.StartupDelay(TimeSpan.Zero))
                .HotEntryBudget(hot => hot.MaxResidentBytes(ByteSize.Megabytes(64))))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.UseJson().ExpireAfter(TimeSpan.FromMinutes(5))
                .Limits(limits => limits.MaxEntries(1000).MaxPayloadBytes(ByteSize.Megabytes(1)))
                .Loading(loading => loading.SourceTimeout(TimeSpan.FromSeconds(20)).WaitTimeout(TimeSpan.FromSeconds(10))));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("items");

        // Act
        var stored = await sut.SetAsync(new("key"), "value");
        var found = await sut.GetAsync("key");

        // Assert
        stored.IsSuccess.ShouldBeTrue();
        found.IsSuccess.ShouldBeTrue();
        found.Value.Value.ShouldBe("value");
        var catalog = provider.GetRequiredService<CacheCatalog>();
        catalog.Options.Namespace.ShouldBe("catalog");
        catalog.Options.Diagnostics.HistoryEnabled.ShouldBeTrue();
        catalog.Options.Diagnostics.HistoryRetention.ShouldBe(TimeSpan.FromHours(1));
        catalog.Options.Diagnostics.ChartWindow.ShouldBe(TimeSpan.FromMinutes(15));
        catalog.Options.Limits.MaxActiveSources.ShouldBe(16);
        catalog.Options.Limits.MaxTransientBytes.ShouldBe(32 * CacheDefaults.MiB);
        catalog.Options.BackgroundServices.StartupDelay.ShouldBe(TimeSpan.Zero);
        catalog.Options.HotEntryBudget.MaxResidentBytes.ShouldBe(64 * CacheDefaults.MiB);
        var collectionOptions = catalog.Collections["items"].Options;
        collectionOptions.Codec.ShouldBe("JSON");
        collectionOptions.Limits.MaxEntries.ShouldBe(1000);
        collectionOptions.Limits.MaxEncodedBytes.ShouldBe(CacheDefaults.MiB);
        collectionOptions.Limits.MaxLogicalBytes.ShouldBe(CacheDefaults.MiB);
        (stored.Value.Metadata.AbsoluteExpiresAt - stored.Value.Metadata.PublishedAt).ShouldBe(TimeSpan.FromMinutes(5));
    }

    /// <summary>Repeated nested callbacks retain earlier explicit siblings and do not replace whole option records.</summary>
    [Fact]
    public void NestedCallbacks_RepeatedPolicyConfiguration_AccumulatesOnlyExplicitFields()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)))
                .Limits(limits => limits.MaxActiveSources(12)))
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.ChartWindow(TimeSpan.FromMinutes(30)))
                .Limits(limits => limits.MaxTransfers(6)))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection
                .Limits(limits => limits.MaxEntries(500))
                .Limits(limits => limits.MaxPayloadBytes(2 * CacheDefaults.MiB))
                .Loading(loading => loading.Attempts(3, TimeSpan.FromMilliseconds(50)))
                .Loading(loading => loading.WaitTimeout(TimeSpan.FromSeconds(12)))
                .Expiration(expiration => expiration.Sliding(TimeSpan.FromMinutes(2), TimeSpan.FromHours(1)))
                .Expiration(expiration => expiration.Jitter(0.1))
                .Streaming(streaming => streaming.BufferSize(32 * (int)CacheDefaults.KiB))
                .Streaming(streaming => streaming.IdleTimeout(TimeSpan.FromSeconds(10))));

        // Act
        var sut = builder.BuildCatalog();
        var collectionOptions = sut.Collections["items"].Options;

        // Assert
        sut.Options.Diagnostics.HistoryEnabled.ShouldBeTrue();
        sut.Options.Diagnostics.HistoryRetention.ShouldBe(TimeSpan.FromHours(1));
        sut.Options.Diagnostics.ChartWindow.ShouldBe(TimeSpan.FromMinutes(30));
        sut.Options.Limits.MaxActiveSources.ShouldBe(12);
        sut.Options.Limits.MaxTransfers.ShouldBe(6);
        collectionOptions.Limits.MaxEntries.ShouldBe(500);
        collectionOptions.Limits.MaxEncodedBytes.ShouldBe(2 * CacheDefaults.MiB);
        collectionOptions.Limits.MaxLogicalBytes.ShouldBe(2 * CacheDefaults.MiB);
        collectionOptions.Loading.MaxAttempts.ShouldBe(3);
        collectionOptions.Loading.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(50));
        collectionOptions.Loading.WaitTimeout.ShouldBe(TimeSpan.FromSeconds(12));
        collectionOptions.Expiration.SlidingEnabled.ShouldBeTrue();
        collectionOptions.Expiration.SlidingIdleTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        collectionOptions.Expiration.AbsoluteCap.ShouldBe(TimeSpan.FromHours(1));
        collectionOptions.Expiration.JitterRatio.ShouldBe(0.1);
        collectionOptions.Streaming.BufferSize.ShouldBe(32 * (int)CacheDefaults.KiB);
        collectionOptions.Streaming.IdleTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    /// <summary>Configured false, zero, empty lists, and scalar values retain precedence over typed fluent opt-ins.</summary>
    [Fact]
    public void Configuration_TypedFluentPolicies_PreservesPresenceAwareOverrides()
    {
        // Arrange
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("""
            {
              "Diagnostics": { "HistoryEnabled": false },
              "Collections": {
                "items": {
                  "Expiration": { "AbsoluteTtl": "00:03:00", "JitterRatio": 0 },
                  "Limits": { "MaxEntries": 20 },
                  "Compression": { "Enabled": false },
                  "NegativeCaching": { "Enabled": false },
                  "Behaviors": { "CollectionOperations": [] }
                }
              }
            }
            """));
        using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddJsonStream(input).Build();
        var builder = new ServiceCollection().AddApplicationCaching().WithConfiguration(configuration)
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1))))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.ExpireAfter(TimeSpan.FromMinutes(5))
                .Expiration(expiration => expiration.Jitter(0.2))
                .Limits(limits => limits.MaxEntries(1000)).Compression().NegativeCaching()
                .Behaviors(behaviors => behaviors.CollectionOperations("logging", "metrics")));

        // Act
        var sut = builder.BuildCatalog();
        var collectionOptions = sut.Collections["items"].Options;

        // Assert
        sut.Options.Diagnostics.HistoryEnabled.ShouldBeFalse();
        sut.Options.Diagnostics.HistoryRetention.ShouldBe(TimeSpan.FromHours(1));
        collectionOptions.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(3));
        collectionOptions.Expiration.JitterRatio.ShouldBe(0);
        collectionOptions.Limits.MaxEntries.ShouldBe(20);
        collectionOptions.Compression.Enabled.ShouldBeFalse();
        collectionOptions.NegativeCaching.Enabled.ShouldBeFalse();
        collectionOptions.Behaviors.CollectionOperations.ShouldBeEmpty();
    }

    /// <summary>Tuning ordinary limits, stream settings, worker timing, and chart presentation does not enable optional collection policies.</summary>
    [Fact]
    public void Policies_NotExplicitlyEnabled_RemainOffWithTypedTuning()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.ChartWindow(TimeSpan.FromMinutes(5)))
                .BackgroundServices(background => background.MaintenanceInterval(TimeSpan.FromMinutes(2)))
                .HotEntryBudget(hot => hot.MaxResidentBytes(64 * CacheDefaults.MiB)))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection
                .Limits(limits => limits.MaxActiveSources(2))
                .Loading(loading => loading.NodeLocal())
                .Streaming(streaming => streaming.Lifetime(TimeSpan.FromMinutes(2))));

        // Act
        var sut = builder.BuildCatalog();
        var collectionOptions = sut.Collections["items"].Options;

        // Assert
        sut.Options.Diagnostics.HistoryEnabled.ShouldBeFalse();
        collectionOptions.HotEntries.Enabled.ShouldBeFalse();
        collectionOptions.Compression.Enabled.ShouldBeFalse();
        collectionOptions.NegativeCaching.Enabled.ShouldBeFalse();
        collectionOptions.Refresh.Enabled.ShouldBeFalse();
        collectionOptions.Warmup.Enabled.ShouldBeFalse();
    }

    /// <summary>Omitted settings in a collection callback inherit earlier global fluent policies.</summary>
    [Fact]
    public void CollectionPolicyCallbacks_OmittedSiblings_InheritGlobalDefaults()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching().WithValueContract<string>("text")
            .Defaults(collection => collection.Limits(limits => limits.MaxEntries(500).MaxPayloadBytes(2 * CacheDefaults.MiB).MaxDecodedBytes(8 * CacheDefaults.MiB))
                .Loading(loading => loading.SourceTimeout(TimeSpan.FromSeconds(15)).WaitTimeout(TimeSpan.FromSeconds(12)))
                .ExpireAfter(TimeSpan.FromMinutes(10)))
            .WithCollection<string>("items", collection => collection.Limits(limits => limits.MaxEntries(1000))
                .Loading(loading => loading.WaitTimeout(TimeSpan.FromSeconds(5))));

        // Act
        var sut = builder.BuildCatalog().Collections["items"].Options;

        // Assert
        sut.Limits.MaxEntries.ShouldBe(1000);
        sut.Limits.MaxEncodedBytes.ShouldBe(2 * CacheDefaults.MiB);
        sut.Limits.MaxLogicalBytes.ShouldBe(2 * CacheDefaults.MiB);
        sut.Limits.MaxDecodedBytes.ShouldBe(8 * CacheDefaults.MiB);
        sut.Loading.SourceTimeout.ShouldBe(TimeSpan.FromSeconds(15));
        sut.Loading.WaitTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        sut.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(10));
    }

    /// <summary>Explicit disables preserve tuning, optional arguments preserve inherited values, and fixed expiration clears sliding mode.</summary>
    [Fact]
    public void TypedPolicyChanges_ExplicitOptOuts_DoNotResetUnrelatedSettings()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)))
                .Diagnostics(diagnostics => diagnostics.History(false)))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection
                .WithSlidingExpiration(TimeSpan.FromMinutes(2), TimeSpan.FromHours(1)).ExpireAfter(TimeSpan.FromMinutes(3))
                .NegativeCaching(TimeSpan.FromSeconds(20)).NegativeCaching().DisableNegativeCaching()
                .Compression(CacheCompressionAlgorithm.Brotli, CompressionLevel.Optimal).DisableCompression()
                .Limits(limits => limits.MaxPayloadBytes(2 * CacheDefaults.MiB))
                .Limits(limits => limits.MaxEncodedBytes(CacheDefaults.MiB))
                .With(value => value.Limits.MaxEntries, 33)
                .Limits(limits => limits.MaxActiveSources(2)));

        // Act
        var sut = builder.BuildCatalog();
        var collectionOptions = sut.Collections["items"].Options;

        // Assert
        sut.Options.Diagnostics.HistoryEnabled.ShouldBeFalse();
        sut.Options.Diagnostics.HistoryRetention.ShouldBe(TimeSpan.FromHours(1));
        collectionOptions.Expiration.SlidingEnabled.ShouldBeFalse();
        collectionOptions.Expiration.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(3));
        collectionOptions.NegativeCaching.Enabled.ShouldBeFalse();
        collectionOptions.NegativeCaching.Ttl.ShouldBe(TimeSpan.FromSeconds(20));
        collectionOptions.Compression.Enabled.ShouldBeFalse();
        collectionOptions.Compression.Algorithm.ShouldBe(CacheCompressionAlgorithm.Brotli);
        collectionOptions.Compression.Level.ShouldBe(CompressionLevel.Optimal);
        collectionOptions.Limits.MaxEncodedBytes.ShouldBe(CacheDefaults.MiB);
        collectionOptions.Limits.MaxLogicalBytes.ShouldBe(2 * CacheDefaults.MiB);
        collectionOptions.Limits.MaxEntries.ShouldBe(33);
    }

    /// <summary>Explicitly incompatible chart windows and invalid retention limits remain errors.</summary>
    /// <param name="minutes">Retention that conflicts with the explicit window or exceeds the supported bound.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(61)]
    public void HistoryRetention_InvalidFinalCombination_IsNotClamped(int minutes)
    {
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithOptions(options => options.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromMinutes(minutes))
                .ChartWindow(TimeSpan.FromMinutes(15))))
            .WithValueContract<string>("text").WithCollection<string>("items");

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    /// <summary>Numeric and duration helpers preserve explicit invalid values for final policy validation.</summary>
    /// <param name="policy">The typed policy receiving an invalid explicit setting.</param>
    [Theory]
    [InlineData("payload")]
    [InlineData("attempts")]
    [InlineData("expiration")]
    [InlineData("jitter")]
    [InlineData("stream")]
    public void TypedPolicies_InvalidExplicitSetting_FailsFinalization(string policy)
    {
        var builder = new ServiceCollection().AddApplicationCaching().WithValueContract<string>("text")
            .WithCollection<string>("items", collection =>
            {
                switch (policy)
                {
                    case "payload": collection.Limits(limits => limits.MaxPayloadBytes(0)); break;
                    case "attempts": collection.Loading(loading => loading.Attempts(0)); break;
                    case "expiration": collection.ExpireAfter(TimeSpan.Zero); break;
                    case "jitter": collection.Expiration(expiration => expiration.Jitter(1)); break;
                    default: collection.Streaming(streaming => streaming.Lifetime(TimeSpan.Zero)); break;
                }
            });

        Should.Throw<ArgumentException>(() => builder.BuildCatalog());
    }

    /// <summary>Typed provider capacity and lease settings finalize without constructing the selected store.</summary>
    [Fact]
    public void ProviderPolicies_TypedCapacityAndLease_FinalizeWithoutActivation()
    {
        var builder = new ServiceCollection().AddApplicationCaching().WithValueContract<string>("text")
            .WithProvider("store", provider => provider.Kind("InMemory").MaxResidentBytes(128 * CacheDefaults.MiB)
                .MaxStorageBytes(64 * CacheDefaults.MiB).MaxEntries(1000).MetadataCacheBytes(8 * CacheDefaults.MiB)
                .AttemptTimeout(TimeSpan.FromSeconds(3)).Lease(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5)))
            .WithCollection<string>("items", collection => collection.UseProvider("store"));

        var sut = builder.BuildCatalog().Providers["store"].Options;

        sut.MaxResidentBytes.ShouldBe(128 * CacheDefaults.MiB);
        sut.MaxStorageBytes.ShouldBe(64 * CacheDefaults.MiB);
        sut.MaxEntries.ShouldBe(1000);
        sut.MetadataCacheBytes.ShouldBe(8 * CacheDefaults.MiB);
        sut.AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(3));
        sut.LeaseLifetime.ShouldBe(TimeSpan.FromSeconds(20));
        sut.LeaseRenewalInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    /// <summary>Typed refresh and warm-up opt-ins select trusted sources while remaining lazy during catalog finalization.</summary>
    [Fact]
    public void BackgroundPolicies_TypedRefreshAndWarmup_FinalizeWithoutSourceWork()
    {
        var services = new ServiceCollection();
        var sourceCalls = 0;
        services.AddScoped(_ => new Loader(() => Interlocked.Increment(ref sourceCalls)));
        var builder = services.AddApplicationCaching().WithValueContract<string>("text")
            .WithRefreshLoader<string, Loader>("source")
            .WithCollection<string>("items", collection => collection
                .Refresh(refresh => refresh.UseLoader("source").Ahead(TimeSpan.FromSeconds(30)).Interval(TimeSpan.FromSeconds(10))
                    .ActivityWindow(TimeSpan.FromMinutes(15)).CandidateLimit(64).NewWorkBudget(TimeSpan.FromSeconds(2))
                    .FailureBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)).PauseAfterFailures(4, TimeSpan.FromMinutes(2)))
                .Warmup(warmup => warmup.UseLoader("source").Items(new CacheItem("current")).Interval(TimeSpan.FromHours(1)))
                .Warmup(warmup => warmup.StartupOnly()));

        var sut = builder.BuildCatalog().Collections["items"].Options;

        sut.Refresh.Enabled.ShouldBeTrue();
        sut.Refresh.Loader.ShouldBe("SOURCE");
        sut.Refresh.RefreshAhead.ShouldBe(TimeSpan.FromSeconds(30));
        sut.Refresh.Interval.ShouldBe(TimeSpan.FromSeconds(10));
        sut.Refresh.FailureBackoff.ShouldBe(TimeSpan.FromSeconds(1));
        sut.Refresh.MaximumFailureBackoff.ShouldBe(TimeSpan.FromSeconds(10));
        sut.Refresh.FailuresBeforePause.ShouldBe(4);
        sut.Warmup.Enabled.ShouldBeTrue();
        sut.Warmup.Loader.ShouldBe("SOURCE");
        sut.Warmup.Items.ShouldHaveSingleItem().Key.ShouldBe("current");
        sut.Warmup.Interval.ShouldBeNull();
        sourceCalls.ShouldBe(0);
    }

    /// <summary>Shared-load convenience selects provider-backed shared ownership rather than inheriting independent execution.</summary>
    [Fact]
    public void ShareLoadsAcrossNodes_PreviouslyIndependent_SelectsOneSharedSourceOwner()
    {
        var capabilities = new CacheProviderCapabilities(true, true, true, false, true, true);
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithProviderKind("shared-test", capabilities, (_, _, _) => throw new InvalidOperationException("Finalization must remain lazy."))
            .WithProvider("store", provider => provider.Kind("shared-test"))
            .WithValueContract<string>("text")
            .WithCollection<string>("items", collection => collection.UseProvider("store")
                .Loading(loading => loading.Strategy(CacheLoadingStrategy.Independent)).ShareLoadsAcrossNodes());

        var sut = builder.BuildCatalog().Collections["items"].Options.Loading;

        sut.CoordinationScope.ShouldBe(CacheCoordinationScope.SharedStore);
        sut.Strategy.ShouldBe(CacheLoadingStrategy.Shared);
    }

    /// <summary>Provides a trusted source whose execution can be observed independently of policy finalization.</summary>
    /// <param name="onLoad">The actual source invocation counter.</param>
    private sealed class Loader(Action onLoad) : ICacheRefreshLoader<string>
    {
        /// <inheritdoc />
        public ValueTask<Result<CacheLoadResult<string>>> LoadAsync(CacheLoadContext context, CancellationToken cancellationToken = default)
        {
            onLoad();
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("loaded")));
        }
    }
}