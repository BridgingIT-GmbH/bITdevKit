// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns complete named provider lifetimes independently of the minimal Common cache family.</summary>
/// <example><code>var provider = registry.Get("memory");</code></example>
public sealed class CacheProviderRegistry : IAsyncDisposable
{
    private readonly FrozenDictionary<string, Lazy<ICacheStoreProvider>> providers;
    private readonly ConcurrentBag<ICacheStoreProvider> created = [];
    private int disposed;

    /// <summary>Captures trusted lazy factories. No native store access occurs during construction.</summary>
    public CacheProviderRegistry(CacheCatalog catalog, IServiceProvider services)
    {
        this.providers = catalog.Providers.Values.ToFrozenDictionary(p => p.Name, p => new Lazy<ICacheStoreProvider>(() =>
        {
            var provider = p.Kind.Factory(services, p.Name, p.NativeOptions) ??
                throw new InvalidOperationException("A complete cache provider factory returned null.");
            if (!StringComparer.OrdinalIgnoreCase.Equals(provider.Name, p.Name) || provider.Capabilities != p.Capabilities)
            {
                this.created.Add(provider);
                throw new InvalidOperationException("A cache provider does not match its registered name/capabilities.");
            }

            if (p.Options.Breaker.Enabled && p.Capabilities.Persistent)
            {
                provider = new CircuitBreakingCacheStoreProviderBehavior(provider, p.Options.Breaker, services.GetRequiredService<TimeProvider>(),
                    services.GetService<CacheDiagnostics>()?.ForProvider(p.Name));
            }

            this.created.Add(provider);
            return provider;
        }, LazyThreadSafetyMode.ExecutionAndPublication), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets one declared complete provider. Provider factories must defer native readiness/I/O to InitializeAsync.</summary>
    public ICacheStoreProvider Get(string name)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        return this.providers.TryGetValue(CacheIdentityNormalizer.Name(name), out var provider) ? provider.Value :
            throw new ArgumentException("The complete cache provider was not declared at startup.", nameof(name));
    }

    /// <summary>Reads an existing provider instance without invoking a lazy factory or initializing a native store.</summary>
    /// <param name="name">The declared provider alias.</param>
    /// <param name="provider">The existing complete provider, or null when it has not been created.</param>
    /// <returns>True when a native instance already exists.</returns>
    /// <example><code>if (registry.TryGetCreated("database", out var provider)) { Inspect(provider.Name); }</code></example>
    public bool TryGetCreated(string name, out ICacheStoreProvider provider)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
        provider = null;
        if (!this.providers.TryGetValue(CacheIdentityNormalizer.Name(name), out var declared) || !declared.IsValueCreated) { return false; }

        provider = declared.Value;
        return true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        foreach (var provider in this.created.Distinct<ICacheStoreProvider>(ReferenceEqualityComparer.Instance))
        {
            await provider.DisposeAsync().ConfigureAwait(false);
        }
    }
}