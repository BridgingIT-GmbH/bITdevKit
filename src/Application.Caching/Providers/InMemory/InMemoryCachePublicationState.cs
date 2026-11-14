// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;

/// <summary>Owns one declared collection's publication authority, contract definitions, indexes and resource hierarchy.</summary>
/// <example><code>lock (state.KeyGate(identity.Key)) { var valid = state.IsFenceCurrent(identity, fence); }</code></example>
public sealed class InMemoryCachePublicationState : IDisposable
{
    private readonly IDisposable charge;

    /// <summary>Owns one declared collection's publication authority, contract definitions, indexes and resource hierarchy.</summary>
    /// <param name="definition">The declared contract and native collection policies.</param>
    /// <param name="resident">The resident capacity hierarchy.</param>
    /// <param name="metadata">The immutable acquired publication metadata.</param>
    /// <param name="storage">The native stored-byte budget.</param>
    /// <param name="count">The entry-count budget.</param>
    /// <param name="readers">Protected-reader admission.</param>
    /// <param name="stages">Write-staging admission.</param>
    /// <param name="charge">Retained state bookkeeping capacity.</param>
    public InMemoryCachePublicationState(CacheProviderCollection definition, ResourceBudget resident,
        ResourceBudget metadata, ResourceBudget storage, ResourceBudget count,
        CacheAdmission readers, CacheAdmission stages, IDisposable charge)
    {
        this.Namespace = definition.Namespace;
        this.Collection = definition.Collection;
        this.Policies = definition.Policies;
        this.Resident = resident;
        this.Metadata = metadata;
        this.Storage = storage;
        this.Count = count;
        this.Readers = readers;
        this.Stages = stages;
        this.charge = charge;
        this.Index = new(metadata, resident);
        this.Definitions[definition.ContractVersion] = definition;
    }

    /// <summary>Coordinates collection-wide publication and invalidation boundaries with per-key work.</summary>
    public readonly ReaderWriterLockSlim Publication = new(LockRecursionPolicy.NoRecursion);

    /// <summary>Provides the fixed 256 canonical-key authority gates.</summary>
    public readonly Lock[] Keys = Enumerable.Range(0, 256).Select(_ => new Lock()).ToArray();

    /// <summary>Contains current immutable generations by canonical contract-isolated identity.</summary>
    public readonly ConcurrentDictionary<CacheEntryIdentity, InMemoryCacheEntry> Entries = [];

    /// <summary>Retains bounded key fence captures and publication high-water marks needed by active work.</summary>
    public readonly ConcurrentDictionary<string, InMemoryCacheKeyState> KeyStates = new(StringComparer.Ordinal);

    /// <summary>Maps active opaque tokens to exact captured authority and reservations.</summary>
    public readonly ConcurrentDictionary<string, InMemoryCacheFenceLease> Fences = new(StringComparer.Ordinal);

    /// <summary>Contains immutable startup contracts indexed by canonical contract version.</summary>
    public readonly ConcurrentDictionary<string, CacheProviderCollection> Definitions = new(StringComparer.Ordinal);

    /// <summary>Owns lazy sharded metadata indexes for this collection.</summary>
    public readonly InMemoryCacheIndex Index;

    /// <summary>Enforces resident capacity within provider/process parents.</summary>
    public readonly ResourceBudget Resident;

    /// <summary>Enforces finite metadata bookkeeping capacity.</summary>
    public readonly ResourceBudget Metadata;

    /// <summary>Enforces current, staging and reader-retained payload storage capacity.</summary>
    public readonly ResourceBudget Storage;

    /// <summary>Enforces current-entry count capacity.</summary>
    public readonly ResourceBudget Count;

    /// <summary>Admits protected reader snapshots within parent ceilings.</summary>
    public readonly CacheAdmission Readers;

    /// <summary>Admits bounded write staging within parent ceilings.</summary>
    public readonly CacheAdmission Stages;

    /// <summary>Stores the owning canonical application namespace.</summary>
    public readonly string Namespace;

    /// <summary>Stores the declared canonical collection alias.</summary>
    public readonly string Collection;

    /// <summary>Stores immutable effective native collection policies.</summary>
    public readonly CacheCollectionOptions Policies;

    /// <summary>Stores the collection-wide invalidation fence epoch.</summary>
    public long Epoch = 0;

    /// <summary>Stores the optimistic metadata revision advanced by collection mutations.</summary>
    public long Revision;

    /// <summary>Stores the monotonic publication sequence for fixed-cutoff invalidation.</summary>
    public long PublicationSequence;

    /// <summary>Counts admitted mutations that must finish before authority reclamation.</summary>
    public int ActiveMutations;

    /// <summary>Retains the next expiry-cleanup shard across bounded maintenance passes.</summary>
    public int SweepShard = 0;

    /// <summary>Retains the next pressure-eviction shard across bounded passes.</summary>
    public int EvictionShard = 0;

    /// <summary>Retains pressure hysteresis state between reclamation passes.</summary>
    public bool UnderPressure;

    /// <summary>Returns a fixed canonical-key gate without allocating a dynamic per-key lock.</summary>
    public Lock KeyGate(string key) => this.Keys[InMemoryCacheIndex.ShardFor(key)];

    /// <summary>Checks exact identity and both captured epochs against the retained opaque lease.</summary>
    public bool IsFenceCurrent(CacheEntryIdentity identity, CachePublicationFence fence) =>
        fence.Value is not null && this.Fences.TryGetValue(fence.Value, out var lease) && lease.Identity == identity &&
        lease.CollectionEpoch == this.Epoch && lease.KeyEpoch == lease.KeyState.Epoch;

    /// <summary>Advances an already captured key epoch to reject older publication authority.</summary>
    public void FenceKey(string key)
    {
        if (this.KeyStates.TryGetValue(key, out var state))
        {
            state.Epoch = checked(state.Epoch + 1);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.Publication.EnterWriteLock();
        try
        {
            foreach (var entry in this.Entries.Values)
            {
                entry.Retire();
            }

            this.Entries.Clear();
            foreach (var fence in this.Fences.Values)
            {
                fence.Charge.Dispose();
            }

            this.Fences.Clear();
            this.KeyStates.Clear();
            this.Index.Dispose();
            this.charge.Dispose();
        }
        finally
        {
            this.Publication.ExitWriteLock();
        }
    }
}

/// <summary>Retains active key-epoch captures and contract-specific publication high-water marks.</summary>
/// <example><code>var captures = keyState.Captures;</code></example>
public sealed class InMemoryCacheKeyState
{
    /// <summary>Stores the collection-wide invalidation fence epoch.</summary>
    public long Epoch;

    /// <summary>Counts active publication fence captures for this canonical key.</summary>
    public int Captures;

    /// <summary>Counts retained captures by contract version.</summary>
    public readonly Dictionary<string, int> ContractCaptures = new(StringComparer.Ordinal);

    /// <summary>Retains latest publication sequences per captured contract for race detection.</summary>
    public readonly Dictionary<string, long> LastPublications = new(StringComparer.Ordinal);
}

/// <summary>Captures collection/key publication authority and observed generation with its finite metadata reservation.</summary>
/// <param name="Identity">The exact canonical identity.</param>
/// <param name="CollectionEpoch">The captured collection epoch.</param>
/// <param name="KeyEpoch">The captured key epoch.</param>
/// <param name="KeyState">The retained bounded key authority.</param>
/// <param name="Charge">The metadata reservation.</param>
/// <param name="PublicationSequence">The sequence observed at capture.</param>
/// <param name="CapturedVersion">The observed valid generation, or null for absence.</param>
/// <example><code>var captured = lease.CapturedVersion;</code></example>
public sealed record InMemoryCacheFenceLease(CacheEntryIdentity Identity, long CollectionEpoch, long KeyEpoch,
    InMemoryCacheKeyState KeyState, IDisposable Charge, long PublicationSequence, CacheEntryVersion? CapturedVersion);