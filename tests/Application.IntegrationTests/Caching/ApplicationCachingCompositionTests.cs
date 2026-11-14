// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.IntegrationTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Exercises consumer-owned headless composition, independent legacy cache registration and explicit requester access.</summary>
public sealed class ApplicationCachingCompositionTests
{
    /// <summary>The existing minimal cache and advanced collection API retain separate entries and lifetimes.</summary>
    [Fact]
    public async Task MinimalAndAdvancedCaching_CoexistWithoutReplacingEitherContract()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching().WithInMemoryProvider();
        services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("cards");
        await using var provider = services.BuildServiceProvider();
        var minimal = provider.GetRequiredService<ICacheProvider>();
        var advanced = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("cards");
        await minimal.SetAsync("same-key", "minimal");
        (await advanced.SetAsync(new("same-key"), "advanced")).IsSuccess.ShouldBeTrue();
        (await advanced.ClearAsync()).IsSuccess.ShouldBeTrue();
        (await minimal.GetAsync<string>("same-key")).ShouldBe("minimal");
        (await advanced.GetAsync("same-key")).Value.Found.ShouldBeFalse();
    }

    /// <summary>A generic host starts exactly the two cache workers without web dependencies, routes or eager source work.</summary>
    [Fact]
    public async Task HeadlessHost_StartsOnlyTheTwoDeclaredWorkers()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddApplicationCaching().WithValueContract<string>("text").WithCollection<string>("cards");
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            host.Services.GetServices<IHostedService>().OfType<CacheBackgroundService>().Count().ShouldBe(2);
            host.Services.GetService<IRequester>().ShouldBeNull();
            var catalog = host.Services.GetRequiredService<CacheCatalog>();
            catalog.Collections["cards"].Options.HotEntries.Enabled.ShouldBeFalse();
            catalog.Collections["cards"].Options.Refresh.Enabled.ShouldBeFalse();
            catalog.Collections["cards"].Options.Warmup.Enabled.ShouldBeFalse();
            typeof(IApplicationCache).Assembly.GetReferencedAssemblies().Any(name => name.Name.StartsWith("BridgingIT.DevKit.Presentation", StringComparison.Ordinal)).ShouldBeFalse();
            (await host.Services.GetRequiredService<IApplicationCache>().GetCollection<string>("cards").GetAsync("missing")).Value.Found.ShouldBeFalse();
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Configuration selects registered policy values; it cannot add undeclared executable collection contracts.</summary>
    [Fact]
    public async Task ConfigurationOverlay_PreservesCodePresenceAndRejectsUnknownAliases()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Collections:cards:Enabled"] = "false",
            ["Collections:cards:Loading:MaxAttempts"] = "2"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithConfiguration(configuration).WithValueContract<string>("text").WithCollection<string>("cards", c => c.With(x => x.Loading.MaxAttempts, 1));
        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<CacheCatalog>().Collections["cards"].Options;
        options.Enabled.ShouldBeFalse();
        options.Loading.MaxAttempts.ShouldBe(2);
        Should.Throw<ArgumentException>(() => provider.GetRequiredService<IApplicationCache>().GetCollection<string>("undeclared"));
    }

    /// <summary>Disabled typed loads remain source-protected while binary calls reject before invoking the source.</summary>
    [Fact]
    public async Task DisabledStorage_KeepsTypedSourceAvailableAndRejectsBinarySource()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithOptions(o => o.With(x => x.Enabled, false))
            .WithValueContract<string>("text").WithBinaryContract("raw").WithCollection<string>("cards").WithBinaryCollection("reports");
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IApplicationCache>();
        var typedCalls = 0;
        var typed = await cache.GetCollection<string>("cards").GetOrLoadAsync(new("one"), (_, _) =>
        {
            typedCalls++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("source")));
        });
        typed.IsSuccess.ShouldBeTrue();
        typed.Value.Value.ShouldBe("source");
        typedCalls.ShouldBe(1);
        var binaryCalls = 0;
        var binary = await cache.GetBinaryCollection("reports").GetOrLoadAsync(new("one"), (_, _, _) =>
        {
            binaryCalls++;
            return ValueTask.FromResult(Result<CacheBinaryLoadResult>.Success(new()));
        });
        binary.IsFailure.ShouldBeTrue();
        binaryCalls.ShouldBe(0);
    }

    /// <summary>Explicit management requests run through the ordinary requester with current trusted scope.</summary>
    [Fact]
    public async Task ManagementRequester_RespectsHostScopeWithoutPresentationRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationCaching().WithOptions(o => o.With(x => x.Namespace, "composition"))
            .WithValueContract<string>("text").WithCollection<string>("cards").WithCollection<string>("private").WithManagementRequests();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var sut = scope.ServiceProvider.GetRequiredService<IRequester>();
        var access = new CacheAccessContext("composition", "host-reader", "headless", CacheAccessActions.Read, ["cards"]);
        var visible = await sut.SendAsync(new CacheCollectionsQuery(access));
        visible.IsSuccess.ShouldBeTrue();
        visible.Value.Select(collection => collection.Name).ShouldBe(["CARDS"]);
        (await sut.SendAsync(new CacheEntryMetadataQuery(access, "private", "one"))).IsFailure.ShouldBeTrue();
        (await sut.SendAsync(new CacheEntryRemoveCommand(access, "cards", "one"))).IsFailure.ShouldBeTrue();
    }
}