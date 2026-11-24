// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Runs bounded native cleanup and tracked invalidation continuation through one monitored maintenance worker.</summary>
/// <param name="catalog">The finalized startup catalog.</param>
/// <param name="access">The shared native readiness/admission boundary.</param>
/// <param name="runner">The cache-specific tracked operation runner.</param>
/// <param name="scopeFactory">The factory for owned readiness and bounded maintenance scopes.</param>
/// <param name="applicationLifetime">The host application lifecycle.</param>
/// <param name="timeProvider">The deterministic worker clock.</param>
/// <param name="history">Optional intrinsic history sampled by this existing worker without another hosted loop.</param>
/// <example><code>var sweep = await maintenance.SweepOnceAsync(cancellationToken);</code></example>
public sealed class CacheMaintenanceService(CacheCatalog catalog, CacheWorkerAccess access, CacheOperationRunner runner,
    IServiceScopeFactory scopeFactory, IHostApplicationLifetime applicationLifetime, TimeProvider timeProvider, CacheMetricHistory history = null)
    : CacheBackgroundService(catalog, CacheWorkerOptions.Maintenance(catalog), applicationLifetime, timeProvider)
{
    private DateTimeOffset nextMaintenance;

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<CacheWorkerOutcome>> ExecuteSweepAsync(CancellationToken cancellationToken)
    {
        history?.Sample();
        if (this.Catalog.Options.Diagnostics.HistoryEnabled && this.TimeProvider.GetUtcNow() < this.nextMaintenance) { return []; }

        var bindings = this.Catalog.Collections.Values.Where(c => c.Options.Enabled).GroupBy(c => c.Provider.Name).Select(g => g.First());
        var tasks = bindings.Select(async binding =>
        {
            var started = this.TimeProvider.GetUtcNow();
            var options = this.Catalog.Options.BackgroundServices;
            await using (var readinessScope = scopeFactory.CreateAsyncScope())
            {
                var ready = await access.EnsureReadyAsync(binding, readinessScope.ServiceProvider, cancellationToken).ConfigureAwait(false);
                if (ready.IsFailure) { return new CacheWorkerOutcome(binding.Provider.Name, false, started, started + options.MaintenanceInterval, Failures: 1); }
            }

            var deadline = this.TimeProvider.GetUtcNow() + options.NewWorkBudget;
            var reclaimed = 0L;
            var batches = 0;
            var failures = 0;
            for (; batches < options.MaximumBatchesPerCollection && this.TimeProvider.GetUtcNow() < deadline; batches++)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await access.CallAsync(binding, (provider, token) => provider.MaintainAsync(new(options.MaintenanceBatchSize,
                    this.TimeProvider.GetUtcNow(), deadline), token), cancellationToken).ConfigureAwait(false);
                if (result.IsFailure) { failures++; break; }

                reclaimed += result.Value.ReclaimedEntries;
                if (!result.Value.HasMore) { batches++; break; }
            }

            var continued = await runner.RunProviderAsync(binding, this.Catalog, options.MaximumBatchesPerCollection,
                deadline, this.TimeProvider, cancellationToken).ConfigureAwait(false);
            return new CacheWorkerOutcome(binding.Provider.Name, true, started, this.TimeProvider.GetUtcNow() + options.MaintenanceInterval,
                batches + continued, (int)Math.Min(int.MaxValue, reclaimed) + continued, Failures: failures,
                LastSuccessAt: failures == 0 ? this.TimeProvider.GetUtcNow() : null);
        }).ToArray();
        var outcomes = Array.AsReadOnly(await Task.WhenAll(tasks).ConfigureAwait(false));
        this.nextMaintenance = this.TimeProvider.GetUtcNow() + this.Catalog.Options.BackgroundServices.MaintenanceInterval;
        return outcomes;
    }
}