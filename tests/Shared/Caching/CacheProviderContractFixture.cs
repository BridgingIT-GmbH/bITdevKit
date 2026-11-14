// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Tests.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

/// <summary>Contains only provider construction differences for the linked observable cache contract scenarios.</summary>
/// <param name="services">One fully configured host-owned service provider.</param>
/// <param name="clock">The validity clock shared by the engine and complete provider.</param>
/// <param name="cleanup">Optional provider-specific temporary-resource cleanup, invoked after service disposal.</param>
/// <example><code>await using var fixture = new CacheProviderContractFixture(services, clock, cleanup);</code></example>
public sealed class CacheProviderContractFixture(ServiceProvider services, CacheContractClock clock, Func<ValueTask> cleanup = null) : IAsyncDisposable
{
    /// <summary>Gets the configured fixture service provider.</summary>
    public ServiceProvider Services { get; } = services;

    /// <summary>Gets the controlled validity clock. No deadline-sensitive admitted work is advanced by these expiry vectors.</summary>
    public CacheContractClock Clock { get; } = clock;

    /// <summary>Gets the ordinary advanced application cache.</summary>
    public IApplicationCache Cache => this.Services.GetRequiredService<IApplicationCache>();

    /// <summary>Gets the complete selected native provider after ordinary lazy initialization.</summary>
    public ICacheStoreProvider Provider => this.Services.GetRequiredService<CacheProviderRegistry>().Get("store");

    /// <summary>Gets the declared typed collection.</summary>
    public ICacheCollection<CacheContractValue> Items => this.Cache.GetCollection<CacheContractValue>("items");

    /// <summary>Gets the declared nullable-string collection.</summary>
    public ICacheCollection<string> Text => this.Cache.GetCollection<string>("text");

    /// <summary>Gets the declared raw binary collection.</summary>
    public ICacheBinaryCollection Binary => this.Cache.GetBinaryCollection("binary");

    /// <summary>Gets the declared namespace/collection/contract identity for a test key.</summary>
    /// <param name="key">The exact bounded key.</param>
    /// <param name="collection">The declared collection, defaulting to items.</param>
    /// <returns>The provider-portable canonical identity.</returns>
    public CacheEntryIdentity Identity(string key, string collection = "items") => new("contracts", collection, "1", key);

    /// <summary>Composes identical contracts and policies while keeping complete native provider factories code-owned.</summary>
    /// <param name="clock">The shared validity clock.</param>
    /// <param name="kind">InMemory, EntityFramework, or FileSystem, used by ordinary engine optimizations.</param>
    /// <param name="capabilities">Truthful complete native capabilities, unused for the built-in InMemory registration.</param>
    /// <param name="factory">The complete native factory, unused for InMemory.</param>
    /// <param name="configure">Optional policy changes for one scenario, applied after the common declarations.</param>
    /// <param name="configureProvider">Optional native settings applied within the single provider declaration.</param>
    /// <param name="configureItems">Optional typed collection settings applied within the single items declaration.</param>
    /// <returns>A service collection ready for the provider fixture to build.</returns>
    public static IServiceCollection CreateServices(CacheContractClock clock, string kind = "InMemory", CacheProviderCapabilities capabilities = null,
        Func<IServiceProvider, string, CacheProviderOptions, ICacheStoreProvider> factory = null, Action<ApplicationCachingBuilderContext> configure = null,
        Action<CacheProviderBuilder> configureProvider = null, Action<CacheCollectionBuilder> configureItems = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock);
        var builder = services.AddApplicationCaching().WithOptions(o => o.With(x => x.Namespace, "contracts"))
            .WithValueContract<CacheContractValue>("card", c => c.WithImmutableSharing(value => 128 + (value?.Name?.Length ?? 0) * 2L))
            .WithValueContract<string>("text").WithBinaryContract("raw")
            .Defaults(c => c.With(x => x.Provider, "store").With(x => x.Expiration.AbsoluteTtl, TimeSpan.FromMinutes(2))
                .With(x => x.Expiration.RetentionGrace, TimeSpan.Zero).With(x => x.NegativeCaching.Enabled, true)
                .With(x => x.NegativeCaching.Ttl, TimeSpan.FromSeconds(10))
                .With(x => x.Dimensions, new CacheDimensionSchema[]
                {
                    new("region", CacheDimensionKind.String), new("count", CacheDimensionKind.Integer),
                    new("updated", CacheDimensionKind.Timestamp), new("active", CacheDimensionKind.Boolean),
                    new("owner", CacheDimensionKind.Identifier), new("tags", CacheDimensionKind.String, CacheDimensionForm.Set, true),
                    new("window", CacheDimensionKind.Integer, CacheDimensionForm.Interval)
                }))
            .WithCollection<CacheContractValue>("items", configureItems).WithCollection<string>("text").WithBinaryCollection("binary");
        if (!kind.Equals("InMemory", StringComparison.OrdinalIgnoreCase)) { builder.WithProviderKind(kind, capabilities, factory); }

        builder.WithProvider("store", p =>
        {
            p.With(x => x.Kind, kind);
            configureProvider?.Invoke(p);
        });
        configure?.Invoke(builder);
        return services;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.Services.DisposeAsync();
        if (cleanup is not null) { await cleanup(); }
    }
}

/// <summary>Represents a fictional immutable source value shared read-only by callers.</summary>
/// <param name="Name">The fictional display name.</param>
/// <param name="Quantity">The domain quantity, independent of cache metadata.</param>
/// <example><code>var value = new CacheContractValue("North stock", 12);</code></example>
public sealed record CacheContractValue(string Name, int Quantity);

/// <summary>Controls UTC validity for portable expiry cases without introducing a scheduling framework.</summary>
/// <remarks>Deadline/race vectors use explicit gates. This clock advances only after admitted source/transfer work has ended.</remarks>
/// <example><code>clock.Advance(TimeSpan.FromMinutes(3));</code></example>
public sealed class CacheContractClock : TimeProvider
{
    private long ticks = new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref this.ticks), TimeSpan.Zero);

    /// <summary>Advances only the authoritative validity observation for a completed-work expiry scenario.</summary>
    /// <param name="duration">A nonnegative finite duration.</param>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(duration)); }

        Interlocked.Add(ref this.ticks, duration.Ticks);
    }
}