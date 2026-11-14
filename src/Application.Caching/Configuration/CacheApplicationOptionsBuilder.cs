// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records explicit global process policies using nested presence-aware option builders.</summary>
/// <example><code>options.Namespace("catalog").Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)));</code></example>
public sealed class CacheApplicationOptionsBuilder : PatchOptionsBuilder<ApplicationCachingOptions, CacheApplicationOptionsBuilder>
{
    /// <summary>Sets the application namespace used to isolate cache identities.</summary>
    /// <param name="value">The bounded namespace, validated at finalization.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.Namespace("catalog");</code></example>
    public CacheApplicationOptionsBuilder Namespace(string value) => this.With(options => options.Namespace, value);

    /// <summary>Explicitly enables or disables advanced cache storage.</summary>
    /// <param name="enabled">Whether advanced cache storage is enabled.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.Enabled(false);</code></example>
    public CacheApplicationOptionsBuilder Enabled(bool enabled = true) => this.With(options => options.Enabled, enabled);

    /// <summary>Configures bounded intrinsic observations without enabling history unless requested.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.Diagnostics(diagnostics => diagnostics.HistoryRetention(TimeSpan.FromHours(1)));</code></example>
    public CacheApplicationOptionsBuilder Diagnostics(Action<CacheDiagnosticsBuilder> configure)
    {
        var builder = new CacheDiagnosticsBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Diagnostics", builder.Patch);
        return this;
    }

    /// <summary>Configures shared process resource ceilings.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.Limits(limits => limits.MaxActiveSources(16));</code></example>
    public CacheApplicationOptionsBuilder Limits(Action<CacheProcessLimitsBuilder> configure)
    {
        var builder = new CacheProcessLimitsBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("Limits", builder.Patch);
        return this;
    }

    /// <summary>Configures cache-owned worker schedules without enabling collection refresh or warm-up.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.BackgroundServices(background => background.StartupDelay(TimeSpan.Zero));</code></example>
    public CacheApplicationOptionsBuilder BackgroundServices(Action<CacheBackgroundServiceBuilder> configure)
    {
        var builder = new CacheBackgroundServiceBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("BackgroundServices", builder.Patch);
        return this;
    }

    /// <summary>Configures the shared hot budget without enabling collection hot retention.</summary>
    /// <param name="configure">Records explicit child policy fields; omitted siblings retain their existing precedence.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>options.HotEntryBudget(hot => hot.MaxResidentBytes(ByteSize.Megabytes(64)));</code></example>
    public CacheApplicationOptionsBuilder HotEntryBudget(Action<CacheHotEntryBudgetBuilder> configure)
    {
        var builder = new CacheHotEntryBudgetBuilder();
        configure?.Invoke(builder);
        this.Patch.Merge("HotEntryBudget", builder.Patch);
        return this;
    }
}