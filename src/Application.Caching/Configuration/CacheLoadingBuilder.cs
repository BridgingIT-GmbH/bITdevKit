// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records source coordination and finite execution budgets independently of cache storage.</summary>
/// <example><code>loading.SharedAcrossNodes().SourceTimeout(TimeSpan.FromSeconds(20));</code></example>
public sealed class CacheLoadingBuilder : PatchOptionsBuilder<CacheLoadingOptions, CacheLoadingBuilder>
{
    /// <summary>Selects coordination scope while preserving the source strategy.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.Coordination(CacheCoordinationScope.NodeLocal);</code></example>
    public CacheLoadingBuilder Coordination(CacheCoordinationScope value) => this.With(options => options.CoordinationScope, value);

    /// <summary>Selects shared or explicitly independent admitted source execution.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.Strategy(CacheLoadingStrategy.Independent);</code></example>
    public CacheLoadingBuilder Strategy(CacheLoadingStrategy value) => this.With(options => options.Strategy, value);

    /// <summary>Sets the owned total deadline including retries and publication.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.SourceTimeout(TimeSpan.FromSeconds(20));</code></example>
    public CacheLoadingBuilder SourceTimeout(TimeSpan value) => this.With(options => options.SourceTimeout, value);

    /// <summary>Sets the individual caller wait budget.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.WaitTimeout(TimeSpan.FromSeconds(10));</code></example>
    public CacheLoadingBuilder WaitTimeout(TimeSpan value) => this.With(options => options.WaitTimeout, value);

    /// <summary>Selects one shared source owner across the capable complete provider.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.SharedAcrossNodes();</code></example>
    public CacheLoadingBuilder SharedAcrossNodes() => this.With(options => options.CoordinationScope, CacheCoordinationScope.SharedStore)
        .With(options => options.Strategy, CacheLoadingStrategy.Shared);

    /// <summary>Selects node-local coordination without changing the source strategy.</summary>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.NodeLocal();</code></example>
    public CacheLoadingBuilder NodeLocal() => this.With(options => options.CoordinationScope, CacheCoordinationScope.NodeLocal);

    /// <summary>Selects bounded continuation after the final interested caller leaves.</summary>
    /// <param name="enabled">Whether already owned source work may continue without waiters.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.ContinueWithoutWaiters();</code></example>
    public CacheLoadingBuilder ContinueWithoutWaiters(bool enabled = true) => this.With(options => options.ContinueWithoutWaiters, enabled);

    /// <summary>Sets the total attempt count and optionally its sequential retry delay.</summary>
    /// <param name="count">The total bounded attempt count, including the first attempt.</param>
    /// <param name="retryDelay">An optional explicit delay; omission preserves existing configuration.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.Attempts(3, TimeSpan.FromMilliseconds(100));</code></example>
    public CacheLoadingBuilder Attempts(int count, TimeSpan? retryDelay = null)
    {
        this.With(options => options.MaxAttempts, count);
        if (retryDelay.HasValue) { this.With(options => options.RetryDelay, retryDelay.Value); }

        return this;
    }

    /// <summary>Sets finite remote observation backoff bounds.</summary>
    /// <param name="minimum">The smallest observation delay.</param>
    /// <param name="maximum">The greatest bounded backoff delay.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.ObservationDelays(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));</code></example>
    public CacheLoadingBuilder ObservationDelays(TimeSpan minimum, TimeSpan maximum) => this.With(options => options.ObservationMinimumDelay, minimum).With(options => options.ObservationMaximumDelay, maximum);

    /// <summary>Selects a trusted code-registered source resilience policy.</summary>
    /// <param name="alias">The registered source resilience alias.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>loading.UseResilience("catalog.database");</code></example>
    public CacheLoadingBuilder UseResilience(string alias) => this.With(options => options.SourceResilience, CacheIdentityNormalizer.Name(alias));
}