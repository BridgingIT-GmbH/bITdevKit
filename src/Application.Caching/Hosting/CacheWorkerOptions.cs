// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Maps the finalized cache schedule to the shared nonoverlapping worker lifecycle.</summary>
/// <param name="StartupDelay">The delay after application startup.</param>
/// <param name="Interval">The delay after a completed iteration.</param>
/// <param name="StopTimeout">The bounded graceful stop wait.</param>
/// <example><code>var schedule = CacheWorkerOptions.Maintenance(catalog);</code></example>
public sealed record CacheWorkerOptions(TimeSpan StartupDelay, TimeSpan Interval, TimeSpan StopTimeout)
{
    /// <summary>Builds the maintenance worker's startup schedule.</summary>
    /// <param name="catalog">The validated startup catalog.</param>
    /// <returns>The independent maintenance schedule.</returns>
    public static CacheWorkerOptions Maintenance(CacheCatalog catalog) => new(catalog.Options.BackgroundServices.StartupDelay,
        catalog.Options.Diagnostics.HistoryEnabled && catalog.Options.Diagnostics.SamplingInterval < catalog.Options.BackgroundServices.MaintenanceInterval ?
            catalog.Options.Diagnostics.SamplingInterval : catalog.Options.BackgroundServices.MaintenanceInterval,
        catalog.Options.BackgroundServices.StopTimeout);

    /// <summary>Builds one refresh-worker schedule for all opted-in refresh and warm-up collections.</summary>
    /// <param name="catalog">The validated startup catalog.</param>
    /// <returns>The shortest configured periodic delay, or the default idle delay when no periodic work is enabled.</returns>
    public static CacheWorkerOptions Refresh(CacheCatalog catalog) => new(catalog.Options.BackgroundServices.StartupDelay,
        catalog.Collections.Values.Where(c => c.Options.Enabled).SelectMany(c =>
            (c.Options.Refresh.Enabled ? new[] { c.Options.Refresh.Interval } : Array.Empty<TimeSpan>()).Concat(
                c.Options.Warmup.Enabled && c.Options.Warmup.Interval.HasValue ? [c.Options.Warmup.Interval.Value] : []))
            .DefaultIfEmpty(TimeSpan.FromSeconds(15)).Min(), catalog.Options.BackgroundServices.StopTimeout);

    /// <summary>Creates shared hosting options without altering the finalized cache policy.</summary>
    /// <returns>The scheduling options consumed by PeriodicBackgroundService.</returns>
    public PeriodicBackgroundServiceOptions ToPeriodicOptions() => new() { StartupDelay = this.StartupDelay, Interval = this.Interval, StopTimeout = this.StopTimeout };
}