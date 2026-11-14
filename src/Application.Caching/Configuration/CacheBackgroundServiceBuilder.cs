// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Records finite cache-owned worker schedules without starting hosted work.</summary>
/// <example><code>background.StartupDelay(TimeSpan.Zero).MaintenanceInterval(TimeSpan.FromMinutes(2));</code></example>
public sealed class CacheBackgroundServiceBuilder : PatchOptionsBuilder<CacheBackgroundServiceOptions, CacheBackgroundServiceBuilder>
{
    /// <summary>Sets delay before the host-start readiness gate.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.StartupDelay(TimeSpan.Zero);</code></example>
    public CacheBackgroundServiceBuilder StartupDelay(TimeSpan value) => this.With(options => options.StartupDelay, value);

    /// <summary>Sets the bounded provider-readiness wait.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.ReadinessTimeout(TimeSpan.FromSeconds(20));</code></example>
    public CacheBackgroundServiceBuilder ReadinessTimeout(TimeSpan value) => this.With(options => options.ReadinessTimeout, value);

    /// <summary>Sets the host-stop wait budget while unfinished work remains charged.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.StopTimeout(TimeSpan.FromSeconds(10));</code></example>
    public CacheBackgroundServiceBuilder StopTimeout(TimeSpan value) => this.With(options => options.StopTimeout, value);

    /// <summary>Sets delay after a completed maintenance iteration.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.MaintenanceInterval(TimeSpan.FromMinutes(2));</code></example>
    public CacheBackgroundServiceBuilder MaintenanceInterval(TimeSpan value) => this.With(options => options.MaintenanceInterval, value);

    /// <summary>Sets the entry target for one cleanup batch.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.MaintenanceBatchSize(128);</code></example>
    public CacheBackgroundServiceBuilder MaintenanceBatchSize(int value) => this.With(options => options.MaintenanceBatchSize, value);

    /// <summary>Sets the finite cleanup batch count per sweep.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.MaximumBatchesPerCollection(4);</code></example>
    public CacheBackgroundServiceBuilder MaximumBatchesPerCollection(int value) => this.With(options => options.MaximumBatchesPerCollection, value);

    /// <summary>Sets maximum new work admission time per sweep.</summary>
    /// <param name="value">The explicit setting, validated with the finalized catalog.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>background.NewWorkBudget(TimeSpan.FromSeconds(3));</code></example>
    public CacheBackgroundServiceBuilder NewWorkBudget(TimeSpan value) => this.With(options => options.NewWorkBudget, value);
}