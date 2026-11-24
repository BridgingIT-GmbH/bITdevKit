// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Combines scoped redacted policy, execution, resource and separately scoped provider observations.</summary>
/// <param name="ObservedAt">The report observation timestamp.</param>
/// <param name="Scope">NodeLocal for process execution and memory observations.</param>
/// <param name="Collections">Declared collection execution snapshots.</param>
/// <param name="Providers">One observation per distinct selected provider, without summing shared-store counts.</param>
/// <param name="Footprint">Process-owned memory and native-work observations.</param>
/// <param name="Policies">Redacted effective collection policy summaries.</param>
/// <example><code>var report = await diagnostics.GetSnapshotAsync("cards", token);</code></example>
public sealed record CacheDiagnosticsReport(DateTimeOffset ObservedAt, string Scope, IReadOnlyList<CacheObservationSnapshot> Collections,
    IReadOnlyList<CacheProviderObservation> Providers, CacheProcessFootprintSnapshot Footprint, IReadOnlyList<CacheEffectivePolicySnapshot> Policies);

/// <summary>Reports provider storage/readiness in its actual store scope, independently of node-local request counters.</summary>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="Kind">The registered provider kind.</param>
/// <param name="Scope">NodeLocal, HostLocalSharedStore, or SharedStore according to native capabilities.</param>
/// <param name="Quota">The provider's actual accounting, or explicit unavailability without fabricated zero counts.</param>
/// <param name="Breaker">The optional provider-breaker state, or null when breaking is omitted.</param>
/// <example><code>if (provider.Quota.Availability == CacheObservationAvailability.Available) { Show(provider.Quota.Value.CurrentEntries); }</code></example>
public sealed record CacheProviderObservation(string Provider, string Kind, string Scope, CacheObservedValue<CacheProviderQuota> Quota,
    CacheProviderBreakerSnapshot Breaker);

/// <summary>Describes a named provider's bounded process-local circuit state without retaining invocation identities.</summary>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="State">Closed, Open, or HalfOpen.</param>
/// <param name="ConsecutiveFailures">Consecutive classified infrastructure failures.</param>
/// <param name="RemainingCooldown">The remaining open cooldown.</param>
/// <param name="RejectedCalls">Open/occupied-probe rejections since this process started.</param>
/// <param name="Probes">Nonmutating readiness probes since this process started.</param>
/// <param name="ObservedAt">The state observation timestamp.</param>
/// <example><code>var state = breaker.Snapshot();</code></example>
public sealed record CacheProviderBreakerSnapshot(string Provider, string State, int ConsecutiveFailures, TimeSpan RemainingCooldown,
    long RejectedCalls, long Probes, DateTimeOffset ObservedAt);

/// <summary>Separates owned RAM, optional representation estimates and process-wide memory from persisted provider bytes.</summary>
/// <param name="ResidentBytes">Conservatively accounted process-owned cache resident bytes.</param>
/// <param name="HotResidentBytes">The hot sublimit usage, already included in resident bytes.</param>
/// <param name="TransientBytes">Occupied transient reservations.</param>
/// <param name="HotBinaryBytes">Binary hot representation bytes, or unavailable when no source reports them.</param>
/// <param name="HotDecodedBytes">Conservative decoded hot charges, or unavailable.</param>
/// <param name="HotPinnedBytes">Pinned hot representation charges, or unavailable.</param>
/// <param name="ProcessWorkingSetBytes">The process working set, which includes memory outside caching.</param>
/// <param name="ActiveSources">Actually occupied source admissions.</param>
/// <param name="QueuedSources">Queued source admissions.</param>
/// <param name="ActiveProviderCalls">Actually occupied native calls, including timed-out work that has not settled.</param>
/// <param name="ActiveTransfers">Actually occupied transfer admissions.</param>
/// <example><code>ShowOwnedBytes(report.Footprint.ResidentBytes.Value);</code></example>
public sealed record CacheProcessFootprintSnapshot(CacheObservedValue<long> ResidentBytes, CacheObservedValue<long> HotResidentBytes,
    CacheObservedValue<long> TransientBytes, CacheObservedValue<long?> HotBinaryBytes, CacheObservedValue<long?> HotDecodedBytes,
    CacheObservedValue<long?> HotPinnedBytes, CacheObservedValue<long> ProcessWorkingSetBytes, int ActiveSources, int QueuedSources,
    int ActiveProviderCalls, int ActiveTransfers);

/// <summary>Exposes an immutable effective collection policy without credentials, physical paths, raw warm-up selections or extension values.</summary>
/// <param name="Collection">The declared collection alias.</param>
/// <param name="Provider">The declared provider alias.</param>
/// <param name="ProviderKind">The registered provider kind.</param>
/// <param name="Contract">The trusted contract alias.</param>
/// <param name="ContractVersion">The declared current contract version.</param>
/// <param name="Binary">Whether the collection is raw binary.</param>
/// <param name="Enabled">The effective global and collection enablement.</param>
/// <param name="Loading">Effective strategy, coordination and work limits.</param>
/// <param name="Expiration">Logical freshness and physical-retention policies.</param>
/// <param name="Limits">Finite collection resource limits.</param>
/// <param name="HotEntries">Local hot representation and admission policies.</param>
/// <param name="Refresh">Configured proactive-refresh policy and trusted alias.</param>
/// <param name="WarmupConfigured">Whether warm-up is configured.</param>
/// <param name="BackgroundPaused">The selected node's runtime pause state.</param>
/// <param name="Behaviors">The startup-composed behavior aliases in each stage.</param>
/// <example><code>ShowPolicy(report.Policies[0].Expiration);</code></example>
public sealed record CacheEffectivePolicySnapshot(string Collection, string Provider, string ProviderKind, string Contract, string ContractVersion,
    bool Binary, bool Enabled, CacheLoadingOptions Loading, CacheExpirationOptions Expiration, CacheCollectionLimits Limits,
    CacheHotEntryOptions HotEntries, CacheRefreshOptions Refresh, bool WarmupConfigured, bool BackgroundPaused, CacheBehaviorOptions Behaviors);