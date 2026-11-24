// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records bounded startup or periodic warm-up through trusted source and key aliases.</summary>
/// <example><code>warmup.UseLoader("catalog-source").Items(new CacheItem("current"));</code></example>
public sealed class CacheWarmupBuilder : PatchOptionsBuilder<CacheWarmupOptions, CacheWarmupBuilder>
{
    /// <summary>Explicitly enables or disables warm-up work.</summary>
    /// <param name="enabled">Whether warm-up is enabled.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.Enabled(false);</code></example>
    public CacheWarmupBuilder Enabled(bool enabled = true) => this.With(options => options.Enabled, enabled);

    /// <summary>Selects a trusted code-registered scoped loader.</summary>
    /// <param name="alias">The registered typed or binary loader alias.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.UseLoader("catalog-source");</code></example>
    public CacheWarmupBuilder UseLoader(string alias) => this.With(options => options.Loader, CacheIdentityNormalizer.Name(alias));

    /// <summary>Selects a trusted asynchronous key source instead of configured items.</summary>
    /// <param name="alias">The registered bounded key-source alias.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.UseKeySource("catalog-keys");</code></example>
    public CacheWarmupBuilder UseKeySource(string alias) => this.With(options => options.KeySource, CacheIdentityNormalizer.Name(alias));

    /// <summary>Records the complete ordered predefined warm-up item list.</summary>
    /// <param name="items">The bounded identities and dimensions, snapshotted by the presence-aware patch.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.Items(new CacheItem("current"));</code></example>
    public CacheWarmupBuilder Items(params CacheItem[] items) => this.With(options => options.Items, items);

    /// <summary>Enables periodic warm-up in addition to the startup pass.</summary>
    /// <param name="value">The interval between periodic warm-up passes.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.Interval(TimeSpan.FromHours(1));</code></example>
    public CacheWarmupBuilder Interval(TimeSpan value) => this.With(options => options.Interval, value);

    /// <summary>Selects startup-only warm-up without a periodic interval.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>warmup.StartupOnly();</code></example>
    public CacheWarmupBuilder StartupOnly() => this.With(options => options.Interval, (TimeSpan?)null);
}