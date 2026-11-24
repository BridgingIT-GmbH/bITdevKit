// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Reads bounded redacted diagnostics without creating providers, initializing stores, scanning entries or invoking sources.</summary>
/// <param name="catalog">The immutable startup catalog.</param>
/// <param name="diagnostics">The preallocated execution observations.</param>
/// <param name="resources">The exact process resource accounts.</param>
/// <param name="providers">Existing native provider lifetimes.</param>
/// <param name="background">Node-local background controls.</param>
/// <param name="tasks">The actual native-tail lifetime owner.</param>
/// <param name="timeProvider">The process observation clock.</param>
/// <param name="services">The host services used for existing bounded provider access.</param>
/// <param name="queryGate">The shared no-queue chart-query allowance.</param>
/// <remarks>Management request handlers must authorize collection scope before invoking this service.</remarks>
/// <example><code>var report = await diagnostics.GetSnapshotAsync("cards", token);</code></example>
public sealed class CacheDiagnosticsQueryService(CacheCatalog catalog, CacheDiagnostics diagnostics, CacheRuntimeResources resources,
    CacheProviderRegistry providers, CacheBackgroundControl background, CacheRuntimeTasks tasks, TimeProvider timeProvider, IServiceProvider services,
    CacheDiagnosticQueryGate queryGate)
{
    private readonly ConcurrentDictionary<string, CacheObservedValue<CacheProviderQuota>> lastProviderObservations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Queries selected collection policies, process observations and once-per-provider native quota/readiness.</summary>
    /// <param name="collection">An authorized declared collection alias, or null for the finite declared catalog.</param>
    /// <param name="cancellationToken">The cancellation token for bounded metadata reads.</param>
    /// <returns>A scoped report or an explicit validation/capacity failure.</returns>
    public async ValueTask<Result<CacheDiagnosticsReport>> GetSnapshotAsync(string collection = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!queryGate.TryEnter(out var queryLease))
        {
            return Result<CacheDiagnosticsReport>.Failure(new CacheError(CacheErrorCode.Capacity, "The bounded diagnostic-query allowance is occupied."));
        }

        try
        {
            CacheCollectionBinding[] selected;
            if (collection is null) { selected = catalog.Collections.Values.ToArray(); }
            else if (catalog.Collections.TryGetValue(CacheIdentityNormalizer.Name(collection), out var binding)) { selected = [binding]; }
            else { return Result<CacheDiagnosticsReport>.Failure(new CacheError(CacheErrorCode.Contract, "The diagnostic collection was not declared.")); }

            var bytes = checked(Math.Max(1, selected.Length) * (16384L + Enum.GetValues<CacheLatencyKind>().Length * catalog.Options.Diagnostics.LatencySampleCapacity * 64L));
            if (!resources.Transient.TryReserve(bytes, out var reservation))
            {
                return Result<CacheDiagnosticsReport>.Failure(new CacheError(CacheErrorCode.Capacity, "The bounded diagnostic-snapshot memory allowance is exhausted."));
            }

            using (reservation)
            {
                var observedAt = timeProvider.GetUtcNow();
                var observations = selected.Select(item => diagnostics.ForCollection(item.Name).Snapshot()).ToArray();
                var health = new List<CacheProviderObservation>();
                foreach (var item in selected.DistinctBy(item => item.Provider.Name))
                {
                    health.Add(await this.ProviderAsync(item, cancellationToken).ConfigureAwait(false));
                }

                var policies = selected.Select(item => new CacheEffectivePolicySnapshot(item.Name, item.Provider.Name, item.Provider.Kind.Alias,
                    item.Contract.Alias, item.Options.ContractVersion, item.Contract.IsBinary, catalog.Options.Enabled && item.Options.Enabled,
                    item.Options.Loading, item.Options.Expiration, item.Options.Limits, item.Options.HotEntries, item.Options.Refresh,
                    item.Options.Warmup.Enabled, background.GetState(item.Name).Paused, item.Options.Behaviors)).ToArray();
                return Result<CacheDiagnosticsReport>.Success(new(observedAt, "NodeLocal", observations, health.AsReadOnly(), this.Footprint(observedAt), policies));
            }
        }
        catch (ArgumentException)
        {
            return Result<CacheDiagnosticsReport>.Failure(new CacheError(CacheErrorCode.Validation, "The diagnostic selection is invalid."));
        }
        finally { queryLease.Dispose(); }
    }

    private async ValueTask<CacheProviderObservation> ProviderAsync(CacheCollectionBinding binding, CancellationToken token)
    {
        var provider = binding.Provider;
        var scope = !provider.Capabilities.Persistent ? "NodeLocal" : provider.Capabilities.LocalHostOnly ? "HostLocalSharedStore" : "SharedStore";
        CacheObservedValue<CacheProviderQuota> quota;
        CacheProviderBreakerSnapshot circuit = null;
        if (!catalog.Options.Enabled)
        {
            quota = this.Unavailable(provider.Name, scope, CacheObservationAvailability.Disabled, "FeatureDisabled");
        }
        else if (!providers.TryGetCreated(provider.Name, out var existing))
        {
            quota = this.Unavailable(provider.Name, scope, CacheObservationAvailability.Unavailable, "ProviderNotStarted");
        }
        else
        {
            circuit = (existing as ICacheProviderCircuitState)?.Snapshot();
            var access = new CacheProviderAccess(binding, existing, resources, services.GetRequiredService<CacheProviderInitialization>(), tasks, timeProvider);
            var result = await access.CallAsync(ct => existing.GetQuotaAsync(ct), token, initialize: false).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                quota = new(result.Value, CacheObservationAvailability.Available, result.Value.IsExact ? CacheObservationAccuracy.Exact : CacheObservationAccuracy.Estimated,
                    scope, timeProvider.GetUtcNow());
                this.lastProviderObservations[provider.Name] = quota;
            }
            else
            {
                quota = this.Unavailable(provider.Name, scope, CacheObservationAvailability.Unavailable,
                    result.Errors.OfType<CacheError>().FirstOrDefault()?.Code.ToString() ?? "ProviderUnavailable");
            }
        }

        return new(provider.Name, provider.Kind.Alias, scope, quota, circuit);
    }

    private CacheObservedValue<CacheProviderQuota> Unavailable(string provider, string scope, CacheObservationAvailability availability, string reason) =>
        this.lastProviderObservations.TryGetValue(provider, out var last) ? last with { Availability = availability, Reason = reason } :
            new(null, availability, CacheObservationAccuracy.Unavailable, scope, null, reason);

    private CacheProcessFootprintSnapshot Footprint(DateTimeOffset at)
    {
        var snapshots = diagnostics.SnapshotCollections();
        var supported = snapshots.All(snapshot => snapshot.HotFootprint is not null);
        CacheObservedValue<long?> Capacity(Func<CacheHotFootprintSnapshot, long> select, CacheObservationAccuracy accuracy) => supported ?
            new(snapshots.Sum(snapshot => select(snapshot.HotFootprint)), CacheObservationAvailability.Available, accuracy, "NodeLocal", at) :
            new(null, catalog.Options.Diagnostics.Enabled ? CacheObservationAvailability.Capacity : CacheObservationAvailability.Disabled,
                CacheObservationAccuracy.Unavailable, "NodeLocal", null, "ObservationCapacityUnavailable");
        return new(Exact(resources.Resident.Used, at), Exact(resources.HotResident.Used, at), Exact(resources.Transient.Used, at),
            Capacity(snapshot => snapshot.BinaryBytes, CacheObservationAccuracy.Exact), Capacity(snapshot => snapshot.DecodedBytes, CacheObservationAccuracy.Estimated),
            Capacity(snapshot => snapshot.PinnedBytes, CacheObservationAccuracy.Estimated),
            new(Environment.WorkingSet, CacheObservationAvailability.Available, CacheObservationAccuracy.Sampled, "NodeLocal", at), resources.Sources.Active,
            resources.Sources.Queued, resources.ProviderIO.Active, resources.Transfers.Active);
    }

    private static CacheObservedValue<long> Exact(long value, DateTimeOffset at) => new(value, CacheObservationAvailability.Available, CacheObservationAccuracy.Exact, "NodeLocal", at);
}