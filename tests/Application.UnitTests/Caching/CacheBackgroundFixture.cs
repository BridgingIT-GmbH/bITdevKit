// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

using System.Collections.Concurrent;

internal sealed class CacheBackgroundFixture : IAsyncDisposable
{
    internal CacheBackgroundFixture(Action<ApplicationCachingBuilderContext> configure = null, Action<IServiceCollection> servicesAction = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(this.Time);
        services.AddSingleton<IHostApplicationLifetime>(this.ApplicationLifetime);
        services.AddSingleton(this.Source);
        servicesAction?.Invoke(services);
        var builder = services.AddApplicationCaching().WithValueContract<string>("text").WithRefreshLoader<string, Loader>("source")
            .WithOptions(o => o.With(x => x.Namespace, "workers").With(x => x.BackgroundServices.StartupDelay, TimeSpan.Zero))
            .Defaults(c => c.With(x => x.Expiration.AbsoluteTtl, TimeSpan.FromMinutes(10)).With(x => x.Refresh.Loader, "source")
                .With(x => x.Warmup.Loader, "source"));
        configure?.Invoke(builder);
        this.Services = services.BuildServiceProvider();
        this.Cache = this.Services.GetRequiredService<IApplicationCache>();
    }

    internal FakeTimeProvider Time { get; } = new();

    internal Lifetime ApplicationLifetime { get; } = new();

    internal SourceState Source { get; } = new();

    internal ServiceProvider Services { get; }

    internal IApplicationCache Cache { get; }

    internal CacheCatalog Catalog => this.Services.GetRequiredService<CacheCatalog>();

    internal ICacheCollection<string> Collection(string name = "cards") => this.Cache.GetCollection<string>(name);

    internal ICacheStoreProvider Provider => this.Services.GetRequiredService<CacheProviderRegistry>().Get("memory");

    internal CacheEntryIdentity Identity(string key, string collection = "cards") => new("workers", collection, "1", key);

    internal async Task MakeDueAsync(string key = "key")
    {
        (await this.Collection().SetAsync(new(key), "old")).IsSuccess.ShouldBeTrue();
        (await this.Collection().GetAsync(key)).Value.Value.ShouldBe("old");
        this.Time.Advance(TimeSpan.FromMinutes(9.5));
    }

    public async ValueTask DisposeAsync()
    {
        this.Source.Release?.TrySetResult();
        await this.Services.DisposeAsync();
    }

    internal sealed class SourceState
    {
        internal int Calls;
        internal int Disposed;
        internal readonly ConcurrentQueue<string> Collections = [];
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource DisposedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release;
        internal bool Fail;
    }

    internal sealed class Loader(SourceState state) : ICacheRefreshLoader<string>, IDisposable
    {
        public async ValueTask<Result<CacheLoadResult<string>>> LoadAsync(CacheLoadContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref state.Calls);
            state.Collections.Enqueue(context.Identity.Collection);
            state.Entered.TrySetResult();
            if (state.Release is not null) { await state.Release.Task; }

            return state.Fail ? Result<CacheLoadResult<string>>.Failure(new CacheError(CacheErrorCode.SourceFailure, "Source unavailable.")) :
                Result<CacheLoadResult<string>>.Success(new("fresh"));
        }

        public void Dispose()
        {
            Interlocked.Increment(ref state.Disposed);
            state.DisposedSignal.TrySetResult();
        }
    }

    internal sealed class Lifetime : IHostApplicationLifetime
    {
        internal readonly CancellationTokenSource Started = new();
        internal readonly CancellationTokenSource Stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => this.Started.Token;

        public CancellationToken ApplicationStopping => this.Stopping.Token;

        public CancellationToken ApplicationStopped => this.stopped.Token;

        public void StopApplication() => this.Stopping.Cancel();
    }
}