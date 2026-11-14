// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks that code registrations cannot change a finalized host catalog or its background source behavior.</summary>
public sealed class CacheComponentRegistryTests
{
    /// <summary>A finalized catalog keeps the selected factories even when callers mutate every retained startup collection.</summary>
    [Fact]
    public async Task Catalog_StartupRegistrationsChangedAfterFinalization_WarmupUsesItsOriginalSources()
    {
        ApplicationCachingBuilderContext startup = null;
        await using var fixture = new CacheBackgroundFixture(builder =>
        {
            startup = builder;
            builder.WithWarmupSource<Keys>("keys")
                .WithCollection<string>("cards", collection => collection.With(x => x.Warmup.Enabled, true)
                    .With(x => x.Warmup.KeySource, "keys"));
        });
        var catalog = fixture.Catalog;
        catalog.Registry.ShouldNotBeSameAs(startup.Components);
        startup.Components.Contracts.Clear();
        startup.Components.Kinds.Clear();
        startup.Components.Loaders.Clear();
        startup.Components.WarmupSources.Clear();
        startup.Components.Integrations.Clear();
        startup.Components.SourceResilience.Clear();
        startup.Components.PayloadTransforms.Clear();
        startup.Components.Behaviors.Clear();

        var result = await fixture.Services.GetRequiredService<CacheRefreshService>().SweepOnceAsync();

        result.IsSuccess.ShouldBeTrue();
        fixture.Source.Calls.ShouldBe(1);
        (await fixture.Collection().GetAsync("seed")).Value.Value.ShouldBe("fresh");
        catalog.Registry.Loaders.ContainsKey("source").ShouldBeTrue();
        catalog.Registry.WarmupSources.ContainsKey("keys").ShouldBeTrue();
    }

    /// <summary>Every public catalog registration collection rejects mutation, including empty optional stages and integration capabilities.</summary>
    [Fact]
    public void Catalog_ComponentSnapshot_RejectsAllExposedMutationPaths()
    {
        var builder = new ServiceCollection().AddApplicationCaching()
            .WithValueContract<string>("text").WithCollection<string>("items");
        var sut = builder.BuildCatalog().Registry;

        Should.Throw<NotSupportedException>(() => sut.Contracts.Clear());
        Should.Throw<NotSupportedException>(() => sut.Kinds.Clear());
        Should.Throw<NotSupportedException>(() => sut.Loaders.Clear());
        Should.Throw<NotSupportedException>(() => sut.WarmupSources.Clear());
        Should.Throw<NotSupportedException>(() => sut.Integrations.Add("late"));
        Should.Throw<NotSupportedException>(() => sut.SourceResilience.Clear());
        Should.Throw<NotSupportedException>(() => sut.PayloadTransforms.Clear());
        Should.Throw<NotSupportedException>(() => sut.Behaviors.Clear());
        sut.Contracts.ContainsKey("text").ShouldBeTrue();
        sut.Freeze().ShouldBeSameAs(sut);
    }

    private sealed class Keys : ICacheWarmupSource
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<CacheItem> ReadAsync(CacheWarmupContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            yield return new("seed");
        }
    }
}