// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.Tests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;

/// <summary>Exercises the actual named-provider breaker with a real SQLite engine and independent source policy.</summary>
public sealed class CacheProviderResilienceTests
{
    [Fact]
    public async Task SourceCircuit_RealProviderBehavior_PreservesIndependentProviderHealth()
    {
        await using var host = await Host.CreateAsync();
        var sut = host.Cache.GetCollection<string>("local");
        var callbacks = 0;
        ValueTask<Result<CacheLoadResult<string>>> Fail(CacheLoadContext _, CancellationToken token)
        {
            callbacks++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Source unavailable.")));
        }

        (await sut.GetOrLoadAsync(new("one"), Fail)).IsFailure.ShouldBeTrue();
        (await sut.GetOrLoadAsync(new("two"), Fail)).IsFailure.ShouldBeTrue();
        var rejected = await sut.GetOrLoadAsync(new("three"), Fail);

        rejected.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.SourceRejected);
        callbacks.ShouldBe(2);
        host.SourceState.CircuitState.ShouldBe(CircuitState.Open);
        host.Provider.Snapshot().State.ShouldBe("Closed");
        host.Provider.Snapshot().ConsecutiveFailures.ShouldBe(0);
    }

    [Fact]
    public async Task NativeOutage_DefaultTypedFallback_RejectsSharedAndBinarySourceWork()
    {
        await using var host = await Host.CreateAsync();
        var sut = host.Cache.GetCollection<string>("local");
        (await sut.SetAsync(new("ready"), "value")).IsSuccess.ShouldBeTrue();
        File.Move(host.DatabasePath, host.DatabasePath + ".offline");
        var typedCallbacks = 0;
        var binaryCallbacks = 0;
        ValueTask<Result<CacheLoadResult<string>>> Load(CacheLoadContext _, CancellationToken token)
        {
            typedCallbacks++;
            return ValueTask.FromResult(Result<CacheLoadResult<string>>.Success(new("fresh")));
        }

        var fallback = await sut.GetOrLoadAsync(new("local-miss"), Load);
        var shared = await host.Cache.GetCollection<string>("shared").GetOrLoadAsync(new("shared-miss"), Load);
        var binary = await host.Cache.GetBinaryCollection("binary").GetOrLoadAsync(new("binary-miss"), (_, _, _) =>
        {
            binaryCallbacks++;
            return ValueTask.FromResult(Result<CacheBinaryLoadResult>.Success(new()));
        });

        fallback.IsSuccess.ShouldBeTrue();
        fallback.Value.Value.ShouldBe("fresh");
        fallback.Value.StorageOutcome.ShouldNotBe(CacheStorageOutcome.Stored);
        shared.IsFailure.ShouldBeTrue();
        binary.IsFailure.ShouldBeTrue();
        typedCallbacks.ShouldBe(1);
        binaryCallbacks.ShouldBe(0);
        host.Provider.Snapshot().State.ShouldBe("Open");
        host.SourceState.CircuitState.ShouldBe(CircuitState.Closed);
        (await sut.SetAsync(new("explicit"), "value")).IsFailure.ShouldBeTrue();
    }

    private sealed class Host(ServiceProvider services, string directory, CircuitBreakerStateProvider sourceState) : IAsyncDisposable
    {
        public string DatabasePath => Path.Combine(directory, "cache.db");

        public IApplicationCache Cache => services.GetRequiredService<IApplicationCache>();

        public CircuitBreakingCacheStoreProviderBehavior Provider => (CircuitBreakingCacheStoreProviderBehavior)services.GetRequiredService<CacheProviderRegistry>().Get("store");

        public CircuitBreakerStateProvider SourceState => sourceState;

        public static async Task<Host> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "bdk-cache-resilience-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var builder = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "cache.db"), Pooling = false };
            var schemaOptions = new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite(builder.ToString(),
                migration => migration.MigrationsAssembly(typeof(CacheTestHostDbContext).Assembly.GetName().Name)).Options;
            await using (var bootstrap = new CacheTestHostDbContext(schemaOptions))
            {
                await bootstrap.Database.MigrateAsync();
                await bootstrap.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
            }

            builder.Mode = SqliteOpenMode.ReadWrite;
            var options = new DbContextOptionsBuilder<CacheTestHostDbContext>().UseSqlite(builder.ToString()).Options;
            CacheTestHostDbContext Context()
            {
                var context = new CacheTestHostDbContext(options);
                context.Database.OpenConnection();
                context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
                return context;
            }

            var services = new ServiceCollection();
            services.AddLogging();
            var state = new CircuitBreakerStateProvider();
            var nativeOptions = new CacheProviderOptions { Engine = "Sqlite" };
            services.AddApplicationCaching().WithProviderKind("EntityFramework", EntityFrameworkCacheStoreProvider.CapabilitiesFor(nativeOptions),
                    (providerServices, name, configured) => new EntityFrameworkCacheStoreProvider(name, configured with { Engine = "Sqlite" }, Context,
                        resources: providerServices.GetRequiredService<CacheRuntimeResources>()))
                .WithProvider("store", provider => provider.With(x => x.Kind, "EntityFramework").With(x => x.Breaker.Enabled, true).With(x => x.Breaker.FailureThreshold, 1))
                .WithSourceResilience("dependency", _ => new ResiliencePipelineBuilder().AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    StateProvider = state, ShouldHandle = new PredicateBuilder().Handle<CacheSourceException>(), FailureRatio = 1,
                    MinimumThroughput = 2, SamplingDuration = TimeSpan.FromMinutes(1), BreakDuration = TimeSpan.FromMinutes(1)
                }).Build()).WithValueContract<string>("text").WithBinaryContract("raw")
                .Defaults(collection => collection.With(x => x.Provider, "store").With(x => x.Loading.SourceResilience, "dependency"))
                .WithCollection<string>("local").WithCollection<string>("shared", collection => collection.With(x => x.Loading.CoordinationScope, CacheCoordinationScope.SharedStore))
                .WithBinaryCollection("binary");
            return new(services.BuildServiceProvider(), directory, state);
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
            Directory.Delete(directory, true);
        }
    }
}