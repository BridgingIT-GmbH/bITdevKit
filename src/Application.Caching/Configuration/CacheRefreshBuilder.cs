// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit proactive refresh settings without starting source work.</summary>
/// <example><code>refresh.UseLoader("product-source").Ahead(TimeSpan.FromSeconds(30));</code></example>
public sealed class CacheRefreshBuilder : PatchOptionsBuilder<CacheRefreshOptions, CacheRefreshBuilder>
{
    /// <summary>Selects a trusted registered scoped source alias.</summary>
    /// <param name="alias">The code-registered refresh loader alias.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.UseLoader("product-source");</code></example>
    public CacheRefreshBuilder UseLoader(string alias) => this.With(options => options.Loader, CacheIdentityNormalizer.Name(alias));

    /// <summary>Sets maximum refresh lead, also bounded by actual lifetime.</summary>
    /// <param name="lead">The requested refresh lead.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.Ahead(TimeSpan.FromSeconds(30));</code></example>
    public CacheRefreshBuilder Ahead(TimeSpan lead) => this.With(options => options.RefreshAhead, lead);

    /// <summary>Explicitly enables or disables proactive refresh.</summary>
    /// <param name="enabled">Whether proactive refresh is enabled.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.Enabled(false);</code></example>
    public CacheRefreshBuilder Enabled(bool enabled = true) => this.With(options => options.Enabled, enabled);

    /// <summary>Sets delay after a completed refresh iteration.</summary>
    /// <param name="value">The iteration interval.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.Interval(TimeSpan.FromSeconds(20));</code></example>
    public CacheRefreshBuilder Interval(TimeSpan value) => this.With(options => options.Interval, value);

    /// <summary>Sets the foreground activity eligibility window.</summary>
    /// <param name="value">The foreground activity age eligible for refresh.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.ActivityWindow(TimeSpan.FromMinutes(10));</code></example>
    public CacheRefreshBuilder ActivityWindow(TimeSpan value) => this.With(options => options.ActivityWindow, value);

    /// <summary>Sets the indexed candidate count per collection sweep.</summary>
    /// <param name="value">The bounded candidate count.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.CandidateLimit(64);</code></example>
    public CacheRefreshBuilder CandidateLimit(int value) => this.With(options => options.CandidateLimit, value);

    /// <summary>Sets maximum new source admission time per refresh sweep.</summary>
    /// <param name="value">The bounded admission duration.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.NewWorkBudget(TimeSpan.FromSeconds(3));</code></example>
    public CacheRefreshBuilder NewWorkBudget(TimeSpan value) => this.With(options => options.NewWorkBudget, value);

    /// <summary>Sets initial source-failure backoff and optionally its maximum.</summary>
    /// <param name="initial">The initial backoff delay.</param>
    /// <param name="maximum">An optional explicit maximum; omission preserves its existing value.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.FailureBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));</code></example>
    public CacheRefreshBuilder FailureBackoff(TimeSpan initial, TimeSpan? maximum = null)
    {
        this.With(options => options.FailureBackoff, initial);
        if (maximum.HasValue) { this.With(options => options.MaximumFailureBackoff, maximum.Value); }

        return this;
    }

    /// <summary>Sets the consecutive-failure threshold and bounded per-version pause.</summary>
    /// <param name="count">The consecutive failures before pausing a version.</param>
    /// <param name="pause">The per-version pause duration.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>refresh.PauseAfterFailures(3, TimeSpan.FromMinutes(5));</code></example>
    public CacheRefreshBuilder PauseAfterFailures(int count, TimeSpan pause) => this.With(options => options.FailuresBeforePause, count).With(options => options.FailurePause, pause);

    /// <summary>Builds the recorded refresh policy without starting any source work.</summary>
    /// <returns>The recorded refresh policy snapshot.</returns>
    /// <example><code>var options = refresh.UseLoader("product-source").Apply();</code></example>
    public CacheRefreshOptions Apply() => this.Patch.Apply(new CacheRefreshOptions(), "Fluent", new Dictionary<string, string>());
}