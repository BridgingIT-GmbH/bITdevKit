// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;

/// <summary>Owns preallocated fixed-cardinality diagnostics independently of cache correctness and provider persistence.</summary>
/// <remarks>Only declared collection/provider aliases create slots. Native secrets and cache identities never enter observation state.</remarks>
/// <example><code>var slot = diagnostics.ForCollection("cards"); slot.Record(CacheObservationKind.Miss);</code></example>
public sealed class CacheDiagnostics : IDisposable
{
    private readonly FrozenDictionary<string, CacheObservationSlot> collections;
    private readonly FrozenDictionary<string, CacheObservationSlot> providers;
    private readonly ResourceReservation reservation;
    private readonly CacheRuntimeResources resources;

    /// <summary>Captures declared aliases and conservatively reserves finite optional observation bookkeeping.</summary>
    /// <param name="catalog">The immutable startup catalog.</param>
    /// <param name="resources">The existing process resource hierarchy.</param>
    /// <param name="timeProvider">The process observation clock.</param>
    /// <param name="metrics">The optional existing DevKit metrics exporter.</param>
    public CacheDiagnostics(CacheCatalog catalog, CacheRuntimeResources resources, TimeProvider timeProvider, IMetricsService metrics = null)
    {
        this.resources = resources;
        var options = catalog.Options.Diagnostics;
        options.Validate();
        var slots = catalog.Collections.Count + catalog.Providers.Count;
        var bytes = checked(slots * (4096L + Enum.GetValues<CacheLatencyKind>().Length * options.LatencySampleCapacity * 32L + (options.DetailedKeyHistory ? options.LatencySampleCapacity * 256L : 0)));
        var availability = options.Enabled && resources.Resident.TryReserve(bytes, out this.reservation) ? CacheObservationAvailability.Available :
            options.Enabled ? CacheObservationAvailability.Capacity : CacheObservationAvailability.Disabled;
        var export = new CacheMetrics(metrics);
        this.collections = catalog.Collections.Values.ToFrozenDictionary(binding => binding.Name,
            binding => new CacheObservationSlot(binding.Name, binding.Provider.Name, binding.Options.Loading.CoordinationScope.ToString(), options, timeProvider, export, availability), StringComparer.OrdinalIgnoreCase);
        this.providers = catalog.Providers.Values.ToFrozenDictionary(binding => binding.Name,
            binding => new CacheObservationSlot(null, binding.Name, "Provider", options, timeProvider, export, availability), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets a startup-declared collection slot without allocating per-call labels or allowing dynamic cardinality.</summary>
    /// <param name="name">The declared collection alias.</param>
    /// <returns>The collection's process-local slot.</returns>
    /// <exception cref="ArgumentException">The alias was not declared.</exception>
    public CacheObservationSlot ForCollection(string name) => this.collections.TryGetValue(name, out var slot) ? slot :
        throw new ArgumentException("The diagnostic collection was not declared at startup.", nameof(name));

    /// <summary>Gets a startup-declared provider slot independently of per-collection observation totals.</summary>
    /// <param name="name">The declared provider alias.</param>
    /// <returns>The provider's process-local slot.</returns>
    public CacheObservationSlot ForProvider(string name) => this.providers.TryGetValue(name, out var slot) ? slot :
        throw new ArgumentException("The diagnostic provider was not declared at startup.", nameof(name));

    /// <summary>Snapshots finite declared collection observations without calling providers or opening payloads.</summary>
    /// <returns>One immutable node-local snapshot per declared collection.</returns>
    public IReadOnlyList<CacheObservationSnapshot> SnapshotCollections() => this.collections.Values.Select(slot =>
    {
        var snapshot = slot.Snapshot();
        if (snapshot.Availability != CacheObservationAvailability.Available) { return snapshot; }

        var active = snapshot.Active.ToDictionary(pair => pair.Key, pair => pair.Value);
        active[CacheActiveKind.Transfers] = this.resources.ForCollection(snapshot.Collection).Transfers.Active;
        return snapshot with { Active = active };
    }).ToArray();

    /// <summary>Releases optional observation-bookkeeping charges when the host is disposed.</summary>
    public void Dispose() => this.reservation?.Dispose();
}