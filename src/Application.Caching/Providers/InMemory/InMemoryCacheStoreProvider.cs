// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

/// <summary>Owns authoritative in-process payloads, metadata indexes, publication fences, reader pins, and finite quota.</summary>
/// <remarks>Restart empties the store. Shared-store source coordination and durable reconciliation require a capable persistent provider.</remarks>
/// <example><code>var provider = new InMemoryCacheStoreProvider("memory");</code></example>
public sealed class InMemoryCacheStoreProvider : ICacheStoreProvider
{
    private readonly Lock initialization = new();
    private readonly CacheProviderOptions options;
    private readonly CacheDiagnosticsOptions diagnostics;
    private readonly TimeProvider timeProvider;
    private readonly CacheRuntimeResources resources;
    private readonly ResourceBudget resident;
    private readonly ResourceBudget metadata;
    private readonly ResourceBudget storage;
    private readonly ResourceBudget count;
    private readonly ResourceBudget transient;
    private readonly CacheAdmission readers;
    private readonly CacheAdmission writes;
    private readonly CacheAdmission queries;
    private readonly ConcurrentDictionary<string, InMemoryCachePublicationState> states = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, InMemoryCacheWriteSession> sessions = new(StringComparer.Ordinal);
    private readonly byte[] continuationKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    private int ready;
    private int disposed;
    private long currentBytes;
    private string catalogHash;

    /// <summary>Creates finite on-demand resources; InitializeAsync establishes declared catalog compatibility without external I/O.</summary>
    public InMemoryCacheStoreProvider(string name = "memory", CacheProviderOptions options = null, TimeProvider timeProvider = null,
        CacheRuntimeResources resources = null, CacheDiagnosticsOptions diagnostics = null)
    {
        this.Name = CacheIdentityNormalizer.Name(name);
        this.options = options ?? new();
        this.diagnostics = diagnostics ?? new();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.resources = resources;
        this.resident = resources?.ForProvider(this.Name) ?? new(this.Name, this.options.MaxResidentBytes);
        this.metadata = new("metadata", this.options.MetadataCacheBytes);
        this.storage = new("storage", this.options.MaxStorageBytes);
        this.count = new("entries", this.options.MaxEntries);
        this.transient = resources?.Transient ?? new("transient", 64 * CacheDefaults.MiB);
        this.readers = new("protected-readers", 8, 0, timeProvider: this.timeProvider);
        this.writes = new("staging", 64, 0, timeProvider: this.timeProvider);
        this.queries = new("metadata-queries", 4, 0, timeProvider: this.timeProvider);
    }

    /// <summary>Gets the truthful native capabilities of the in-process store.</summary>
    public static CacheProviderCapabilities DefaultCapabilities { get; } = new(false, false, true, true, false, true);

    /// <summary>Gets the normalized registered provider name.</summary>
    public string Name { get; }

    /// <summary>Gets in-process native execution and access capabilities.</summary>
    public CacheProviderCapabilities Capabilities => DefaultCapabilities;

    /// <summary>Validates and binds declared contracts/schemas. It neither changes Common.Caching nor creates dynamic collections.</summary>
    public ValueTask<Result> InitializeAsync(IReadOnlyList<CacheProviderCollection> collections, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref this.disposed) != 0)
        {
            return ValueTask.FromResult(Result.Failure(Unavailable()));
        }

        try
        {
            var normalized = CacheBoundedSnapshot.Copy(collections ?? throw new ArgumentNullException(nameof(collections)), 1024, nameof(collections))
                .Select(Normalize).ToArray();
            var identities = normalized.Select(d => new CacheEntryIdentity(d.Namespace, d.Collection, d.ContractVersion, "catalog")).ToArray();
            if (identities.Distinct().Count() != normalized.Length)
            {
                return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation, "Duplicate native catalog identities are not permitted.")));
            }

            using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            using (var sink = new SelectionHashStream(hash, cancellationToken))
            {
                foreach (var definition in normalized.OrderBy(d => d.Namespace, StringComparer.Ordinal).ThenBy(d => d.Collection, StringComparer.Ordinal)
                    .ThenBy(d => d.ContractVersion, StringComparer.Ordinal))
                {
                    JsonSerializer.Serialize(sink, definition);
                }
            }

            var fingerprint = Convert.ToHexStringLower(hash.GetHashAndReset());
            lock (this.initialization)
            {
                if (Volatile.Read(ref this.ready) != 0)
                {
                    return ValueTask.FromResult(this.catalogHash == fingerprint ? Result.Success() : Result.Failure(new CacheError(
                        CacheErrorCode.Compatibility, "The initialized native collection catalog is immutable for this provider lifetime.")));
                }

                foreach (var definition in normalized)
                {
                    var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                    if (this.states.TryGetValue(identity.CollectionIdentity, out var state) &&
                        state.Definitions.TryGetValue(identity.ContractVersion, out var previous) && !Compatible(previous, definition))
                    {
                        return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Compatibility,
                            "The retained contract version has an incompatible value alias or dimension schema.")));
                    }
                }

                var created = new List<(string Identity, InMemoryCachePublicationState State)>();
                try
                {
                    foreach (var definition in normalized.Where(d => d.Policies.Enabled))
                    {
                        var identity = new CacheEntryIdentity(definition.Namespace, definition.Collection, definition.ContractVersion, "catalog");
                        if (this.states.TryGetValue(identity.CollectionIdentity, out var existing))
                        {
                            existing.Definitions.TryAdd(identity.ContractVersion, definition);
                            continue;
                        }

                        var resident = this.resources?.ForCollection(definition.Collection).Resident ??
                            new ResourceBudget(definition.Collection, definition.Policies.Limits.MaxResidentBytes, this.resident);
                        var metadata = new ResourceBudget(definition.Collection, this.options.MetadataCacheBytes, this.metadata);
                        var retainedDefinitions = normalized.Count(d => d.Namespace == definition.Namespace && d.Collection == definition.Collection);
                        if (!ResourceCharge.TryReserve(32768 + Math.Max(0, retainedDefinitions - 1) * 16384L, out var charge, resident, metadata))
                        {
                            throw new CacheCapacityException("Collection catalog metadata capacity was exhausted.");
                        }

                        var state = new InMemoryCachePublicationState(definition, resident, metadata,
                            new(definition.Collection, definition.Policies.Limits.MaxStorageBytes, this.storage),
                            new(definition.Collection, definition.Policies.Limits.MaxEntries, this.count),
                            new(definition.Collection, definition.Policies.Limits.MaxTransfers, 0, this.readers),
                            new(definition.Collection, definition.Policies.Limits.MaxActiveProviderOperations, 0, this.writes), charge);
                        this.states.AddOrUpdate(identity.CollectionIdentity, state, (_, _) => throw new InvalidOperationException("Duplicate catalog identity."));
                        created.Add((identity.CollectionIdentity, state));
                    }
                }
                catch
                {
                    foreach (var (identity, state) in created)
                    {
                        this.states.TryRemove(identity, out _);
                        state.Dispose();
                    }

                    throw;
                }

                this.catalogHash = fingerprint;
                Volatile.Write(ref this.ready, 1);
            }

            return ValueTask.FromResult(Result.Success());
        }
        catch (CacheCapacityException)
        {
            return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Capacity, "Native catalog capacity was exhausted.")));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Validation, "Invalid cache provider catalog.")));
        }
    }

    /// <summary>Acquires authoritative metadata or an independently positioned protected immutable payload.</summary>
    public ValueTask<Result<CacheReadValidation>> LookupAsync(CacheEntryIdentity identity, CacheReadRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (!this.TryState(identity, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheReadValidation>.Failure(error));
        }

        return request.OpenPayload ? this.LookupPayloadAsync(state, identity, request, cancellationToken) :
            ValueTask.FromResult(this.Read(state, identity, request, null));
    }

    /// <summary>Validates every hot reuse from current native metadata without reading payload content.</summary>
    public ValueTask<Result<CacheReadValidation>> ValidateReadAsync(CacheEntryIdentity identity, CacheEntryVersion expectedVersion,
        CacheReadRequest request, CancellationToken cancellationToken = default) =>
        this.LookupAsync(identity, request with { ExpectedVersion = expectedVersion }, cancellationToken);

    /// <summary>Captures bounded key/collection publication authority before source access or direct staging.</summary>
    public ValueTask<Result<CachePublicationFence>> CaptureFenceAsync(CacheEntryIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.TryState(identity, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CachePublicationFence>.Failure(error));
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(identity.Key))
            {
                if (!ResourceCharge.TryReserve(1536, out var charge, state.Resident, state.Metadata))
                {
                    return ValueTask.FromResult(Result<CachePublicationFence>.Failure(Capacity()));
                }

                var key = state.KeyStates.GetOrAdd(identity.Key, _ => new());
                state.Entries.TryGetValue(identity, out var previous);
                if (!key.LastPublications.TryGetValue(identity.ContractVersion, out var sequence))
                {
                    sequence = previous?.PublicationSequence ?? 0;
                    key.LastPublications[identity.ContractVersion] = sequence;
                }

                key.ContractCaptures.TryGetValue(identity.ContractVersion, out var captures);
                key.ContractCaptures[identity.ContractVersion] = captures + 1;
                var token = Guid.NewGuid().ToString("N");
                key.Captures++;
                if (!state.Fences.TryAdd(token, new(identity, state.Epoch, key.Epoch, key, charge, sequence, previous?.Metadata.Version)))
                {
                    key.Captures--;
                    charge.Dispose();
                    throw new InvalidOperationException("A unique source-fence nonce collided.");
                }

                return ValueTask.FromResult(Result<CachePublicationFence>.Success(new(token)));
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }
    }

    /// <summary>Releases captured source authority exactly once, including failed or unretained work.</summary>
    public ValueTask<Result> ReleaseFenceAsync(CacheEntryIdentity identity, CachePublicationFence fence, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref this.disposed) != 0 || fence.Value is null || !identity.IsValid ||
            !this.states.TryGetValue(identity.CollectionIdentity, out var state))
        {
            return ValueTask.FromResult(Result.Success());
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(identity.Key))
            {
                if (state.Fences.TryGetValue(fence.Value, out var lease) && lease.Identity == identity &&
                    state.Fences.TryRemove(fence.Value, out _))
                {
                    lease.KeyState.Captures--;
                    var remaining = lease.KeyState.ContractCaptures[identity.ContractVersion] - 1;
                    if (remaining == 0)
                    {
                        lease.KeyState.ContractCaptures.Remove(identity.ContractVersion);
                        lease.KeyState.LastPublications.Remove(identity.ContractVersion);
                    }
                    else
                    {
                        lease.KeyState.ContractCaptures[identity.ContractVersion] = remaining;
                    }

                    if (lease.KeyState.Captures == 0)
                    {
                        state.KeyStates.TryRemove(identity.Key, out _);
                    }

                    lease.Charge.Dispose();
                }
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }

        return ValueTask.FromResult(Result.Success());
    }

    private async ValueTask<Result<CacheReadValidation>> LookupPayloadAsync(InMemoryCachePublicationState state,
        CacheEntryIdentity identity, CacheReadRequest request, CancellationToken cancellationToken)
    {
        Result<CacheAdmissionLease> admission;
        if (state.Readers.TryAcquire(out var immediate))
        {
            admission = Result<CacheAdmissionLease>.Success(immediate);
        }
        else
        {
            var peek = this.Read(state, identity, request with { OpenPayload = false, ObserveAccess = false }, null);
            if (peek.IsFailure || peek.Value.State != CacheReadValidationState.Valid || peek.Value.Metadata.IsNotFound) { return peek; }

            admission = await state.Readers.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        }

        if (admission.IsFailure)
        {
            return Result<CacheReadValidation>.Failure(admission);
        }

        var result = this.Read(state, identity, request, admission.Value);
        if (result.IsFailure || result.Value.Handle is null)
        {
            admission.Value.Dispose();
        }

        return result;
    }

    private Result<CacheReadValidation> Read(InMemoryCachePublicationState state, CacheEntryIdentity identity,
        CacheReadRequest request, CacheAdmissionLease reader)
    {
        if (Volatile.Read(ref this.disposed) != 0)
        {
            return Result<CacheReadValidation>.Failure(Unavailable());
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(identity.Key))
            {
                var now = this.timeProvider.GetUtcNow();
                if (!state.Entries.TryGetValue(identity, out var entry) || !Valid(entry.Metadata, now))
                {
                    return Result<CacheReadValidation>.Success(new(CacheReadValidationState.Missing, null, default));
                }

                var metadata = entry.Metadata;
                if (request.ExpectedVersion is { } expected && metadata.Version != expected)
                {
                    return Result<CacheReadValidation>.Success(new(CacheReadValidationState.Changed, metadata, default));
                }

                if (!request.ObserveAccess && (entry.LastAccessTicks > metadata.LastAccessAt.UtcTicks || entry.HasForegroundAccess != metadata.HasForegroundAccess))
                {
                    metadata = metadata with { LastAccessAt = new(entry.LastAccessTicks, TimeSpan.Zero), HasForegroundAccess = entry.HasForegroundAccess };
                }

                if (request.ObserveAccess)
                {
                    entry.ObserveAccess(now);
                }

                if (request.ObserveAccess && metadata.SlidingIdleTimeout.HasValue)
                {
                    var expires = Min(metadata.AbsoluteExpiresAt, now.Add(metadata.SlidingIdleTimeout.Value));
                    var updated = metadata with { ExpiresAt = expires, LastAccessAt = now, HasForegroundAccess = true };
                    Interlocked.Increment(ref state.ActiveMutations);
                    try
                    {
                        state.Index.UpdateExpiry(entry, metadata.ExpiresAt, expires);
                        entry.UpdateMetadata(updated);
                        metadata = updated;
                        Interlocked.Increment(ref state.Revision);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref state.ActiveMutations);
                    }
                }

                ICacheProviderReadHandle handle = null;
                if (request.OpenPayload && !metadata.IsNotFound)
                {
                    entry.AddReference();
                    try
                    {
                        handle = new InMemoryCacheReadHandle(entry.OpenPayload(), metadata, entry, reader);
                    }
                    catch
                    {
                        entry.Release();
                        throw;
                    }
                }

                return Result<CacheReadValidation>.Success(new(CacheReadValidationState.Valid, metadata, default, handle));
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }
    }

    private bool TryState(CacheEntryIdentity identity, out InMemoryCachePublicationState state, out CacheError error)
    {
        state = null;
        error = null;
        if (Volatile.Read(ref this.disposed) != 0 || Volatile.Read(ref this.ready) == 0)
        {
            error = Unavailable();
        }
        else if (!identity.IsValid)
        {
            error = new(CacheErrorCode.Validation, "A constructed canonical identity is required.");
        }
        else if (!this.states.TryGetValue(identity.CollectionIdentity, out state) ||
            !state.Definitions.ContainsKey(identity.ContractVersion))
        {
            error = new(CacheErrorCode.Contract, "The collection/contract version was not bound during provider readiness.");
        }

        return error is null;
    }

    private static CacheProviderCollection Normalize(CacheProviderCollection definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Dimensions);
        var dimensions = CacheBoundedSnapshot.Copy(definition.Dimensions, 16, nameof(definition.Dimensions))
            .Select(d => d with { Name = CacheIdentityNormalizer.Name(d.Name) }).OrderBy(d => d.Name, StringComparer.Ordinal).ToArray();
        if (dimensions.Length > 16 || dimensions.Select(d => d.Name).Distinct(StringComparer.Ordinal).Count() != dimensions.Length ||
            dimensions.Any(d => !Enum.IsDefined(d.Kind) || !Enum.IsDefined(d.Form) ||
                (d.Form == CacheDimensionForm.Interval && (d.Kind is not (CacheDimensionKind.Integer or CacheDimensionKind.Timestamp) || d.AllowNull))))
        {
            throw new ArgumentException("Invalid native dimension schema.");
        }

        return new(CacheIdentityNormalizer.Namespace(definition.Namespace), CacheIdentityNormalizer.Name(definition.Collection),
            CacheIdentityNormalizer.ContractVersion(definition.ContractVersion), CacheIdentityNormalizer.Name(definition.ContractId),
            Array.AsReadOnly(dimensions), OptionsPatch.Clone(definition.Policies ?? new()));
    }

    private static bool Compatible(CacheProviderCollection left, CacheProviderCollection right) =>
        left.ContractId == right.ContractId && JsonSerializer.Serialize(left.Dimensions) == JsonSerializer.Serialize(right.Dimensions);

    private static bool Valid(CacheEntryMetadata metadata, DateTimeOffset now) =>
        metadata.ExpiresAt > now && metadata.AbsoluteExpiresAt > now;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

    private static CacheError Unavailable() => new(CacheErrorCode.ProviderUnavailable, "The cache provider is not ready or has stopped.");

    private static CacheError Capacity() => new(CacheErrorCode.Capacity, "A finite cache storage or resident limit rejected admission.");

    // Called only after complete size/format validation and conditional checks, with the writer's
    // publication read lock/key gate held. Never wait for another key gate while owning this one.
    private void ReclaimForAdmission(InMemoryCachePublicationState state, string excludedKey)
    {
        if (state.Policies.Eviction != CacheEvictionMode.Evict)
        {
            return;
        }

        state.UnderPressure = true;
        var candidates = new List<InMemoryCacheEntry>(256);
        var all = CacheSelection.Pattern("*");
        for (var i = 0; i < 256 && candidates.Count < 256; i++)
        {
            var shard = (Interlocked.Increment(ref state.EvictionShard) - 1) & 255;
            var slice = state.Index.Find(all, shard, null, null, 256 - candidates.Count);
            foreach (var candidate in slice.Candidates)
            {
                if (candidate.Entry.Metadata.Identity.Key != excludedKey && !candidate.Entry.HasReaders)
                {
                    candidates.Add(candidate.Entry);
                }
            }
        }

        var now = this.timeProvider.GetUtcNow();
        var entries = candidates.ToDictionary(e => e.Metadata.Version);
        var ranked = CacheEvictionPolicy.Select(candidates.Select(e => e.Metadata with
        {
            LastAccessAt = new(e.LastAccessTicks, TimeSpan.Zero)
        }).ToArray(), now, 256);
        foreach (var metadata in ranked)
        {
            var entry = entries[metadata.Version];
            var gate = state.KeyGate(metadata.Identity.Key);
            if (!gate.TryEnter())
            {
                continue;
            }

            try
            {
                if (!entry.HasReaders)
                {
                    this.RemoveCurrent(state, entry);
                }
            }
            finally
            {
                gate.Exit();
            }

            if (state.Resident.Used <= state.Resident.Maximum * 0.8 && state.Metadata.Used <= state.Metadata.Maximum * 0.8 &&
                state.Count.Used <= state.Count.Maximum * 0.8 && state.Storage.Used <= state.Storage.Maximum * 0.8)
            {
                state.UnderPressure = false;
                break;
            }
        }
    }

    /// <summary>Reports bounded storage reservations. Metadata/accounting estimates are explicitly not exact physical-heap measurements.</summary>
    public ValueTask<Result<CacheProviderQuota>> GetQuotaAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var total = this.storage.Used;
        var current = Math.Clamp(Interlocked.Read(ref this.currentBytes), 0, total);
        var staged = Math.Clamp(this.sessions.Values.Sum(s => s.StagingCharge), 0, total - current);
        return ValueTask.FromResult(Result<CacheProviderQuota>.Success(new(current, total - current - staged, staged,
            this.metadata.Used, this.count.Used, this.options.MaxStorageBytes, false)));
    }

    /// <summary>Reports that this synchronous in-process store cannot produce durable indeterminate-commit receipts.</summary>
    public ValueTask<Result<CacheWriteOutcome>> ReconcileAsync(string operationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<CacheWriteOutcome>.Failure(
            new CacheError(CacheErrorCode.Unsupported, "Durable operation reconciliation requires a persistent provider.")));
    }

    /// <summary>Rejects shared-store ownership; node-local source coordination is provided by the collection engine.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> AcquireOwnershipAsync(CacheOwnershipRequest request, CancellationToken cancellationToken = default) =>
        UnsupportedOwnership(cancellationToken);

    /// <summary>Rejects renewal of cross-node source authority that this provider cannot establish.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> RenewOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner,
        CancellationToken cancellationToken = default) => UnsupportedOwnership(cancellationToken);

    /// <summary>Rejects cross-node observation rather than silently downgrading source ownership.</summary>
    public ValueTask<Result<CacheOwnershipObservation>> ObserveOwnershipAsync(CacheOwnershipRequest request, string flightId,
        CancellationToken cancellationToken = default) => UnsupportedOwnership(cancellationToken);

    /// <summary>Rejects unsupported cross-node completion.</summary>
    public ValueTask<Result> CompleteOwnershipAsync(CacheEntryIdentity identity, CacheOwnerToken owner, CacheOwnershipCompletion completion,
        CancellationToken cancellationToken = default) => UnsupportedCompletion(cancellationToken);

    /// <summary>Rejects unsupported cross-node interest management.</summary>
    public ValueTask<Result> ReleaseInterestAsync(CacheEntryIdentity identity, string flightId, string interestId,
        CancellationToken cancellationToken = default) => UnsupportedCompletion(cancellationToken);

    /// <summary>Stops new operations. Occupied staging keeps its charges until its actual owner disposes it; acquired readers retain their generations.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            Volatile.Write(ref this.ready, 0);
            this.Cleanup();
        }

        return ValueTask.CompletedTask;
    }

    private void Cleanup()
    {
        lock (this.initialization)
        {
            if (!this.sessions.IsEmpty)
            {
                return;
            }

            foreach (var state in this.states.Values)
            {
                state.Dispose();
            }

            this.states.Clear();
            lock (this.operationGate)
            {
                foreach (var operation in this.operations.Values)
                {
                    operation.Charge.Dispose();
                }

                this.operations.Clear();
            }

            Interlocked.Exchange(ref this.currentBytes, 0);
        }
    }

    private static ValueTask<Result<CacheOwnershipObservation>> UnsupportedOwnership(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<CacheOwnershipObservation>.Failure(
            new CacheError(CacheErrorCode.Unsupported, "The in-process store cannot establish shared-store source authority.")));
    }

    private static ValueTask<Result> UnsupportedCompletion(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Unsupported,
            "The in-process store cannot establish shared-store source authority.")));
    }

    /// <summary>Reclaims a bounded indexed expiry batch. Open readers retain their old payload charges until they actually close.</summary>
    public ValueTask<Result<CacheMaintenanceOutcome>> MaintainAsync(CacheMaintenanceRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (Volatile.Read(ref this.disposed) != 0 || Volatile.Read(ref this.ready) == 0)
        {
            return ValueTask.FromResult(Result<CacheMaintenanceOutcome>.Failure(Unavailable()));
        }

        if (request.BatchSize is <= 0 or > 1024)
        {
            return ValueTask.FromResult(Result<CacheMaintenanceOutcome>.Failure(
                new CacheError(CacheErrorCode.Validation, "Maintenance batch bounds are invalid.")));
        }

        var reclaimed = 0L;
        var bytes = 0L;
        var more = false;
        foreach (var state in this.states.Values)
        {
            var now = this.timeProvider.GetUtcNow();
            if (reclaimed >= request.BatchSize || now >= request.NewWorkDeadline)
            {
                more = true;
                break;
            }

            var safeCutoff = Min(request.RetentionCutoff, now.Subtract(state.Policies.Expiration.RetentionGrace));
            for (var i = 0; i < 256 && reclaimed < request.BatchSize; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (this.timeProvider.GetUtcNow() >= request.NewWorkDeadline)
                {
                    more = true;
                    break;
                }

                var shard = (Interlocked.Increment(ref state.SweepShard) - 1) & 255;
                InMemoryCacheIndexSlice slice;
                state.Publication.EnterReadLock();
                try
                {
                    slice = state.Index.FindExpiring(shard, safeCutoff.UtcTicks + 1, null, null,
                        (int)Math.Min(256, request.BatchSize - reclaimed));
                }
                finally
                {
                    state.Publication.ExitReadLock();
                }

                foreach (var candidate in slice.Candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    state.Publication.EnterReadLock();
                    try
                    {
                        lock (state.KeyGate(candidate.Entry.Metadata.Identity.Key))
                        {
                            if (candidate.Entry.Metadata.ExpiresAt <= safeCutoff && this.RemoveCurrent(state, candidate.Entry))
                            {
                                reclaimed++;
                                bytes += candidate.Entry.Metadata.StoredSizeBytes;
                            }
                        }
                    }
                    finally
                    {
                        state.Publication.ExitReadLock();
                    }
                }

                more |= slice.HasMore;
            }
        }

        this.PurgeTerminalOperations(this.timeProvider.GetUtcNow());
        return ValueTask.FromResult(Result<CacheMaintenanceOutcome>.Success(new(reclaimed, bytes, more || reclaimed == request.BatchSize, 0)));
    }

    private readonly Lock operationGate = new();
    private readonly Dictionary<string, TrackedOperation> operations = new(StringComparer.Ordinal);

    /// <summary>Creates or conditionally transitions bounded provider-owned operation records before any background mutation begins.</summary>
    public ValueTask<Result<CacheProviderOperation>> SaveOperationAsync(CacheProviderOperation operation, string expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Request is null || !Guid.TryParseExact(operation.Id, "N", out _) || operation.Request.Continuation is not null ||
            operation.Origin?.Length > 128 || !Enum.IsDefined(operation.State))
        {
            return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(
                new CacheError(CacheErrorCode.Validation, "Invalid tracked operation metadata.")));
        }

        if (!this.TryCollection(operation.Request.Namespace, operation.Request.Collection, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(error));
        }

        var selection = operation.Request.Clear ? CacheSelection.Pattern("*") : operation.Request.Selection;
        var validation = ValidateQuery(state, new(state.Namespace, state.Collection, selection, operation.Cutoff, PageSize: 1));
        if (validation.IsFailure)
        {
            return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(validation));
        }

        var outcome = operation.Outcome is null ? null : operation.Outcome with
        {
            FailedCollections = CacheBoundedSnapshot.Copy(operation.Outcome.FailedCollections, 32, nameof(operation.Outcome.FailedCollections))
        };
        var normalized = operation with
        {
            Collection = state.Collection, Cutoff = operation.Cutoff.ToUniversalTime(), Outcome = outcome,
            Request = operation.Request with { Namespace = state.Namespace, Collection = state.Collection, OperationId = operation.Id }
        };
        using var encoded = new MemoryStream();
        try
        {
            using (var bounded = new OperationMetadataStream(encoded, 65536))
            {
                JsonSerializer.Serialize(bounded, normalized);
            }
        }
        catch (InvalidDataException)
        {
            return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(
                new CacheError(CacheErrorCode.Capacity, "Tracked operation metadata exceeds its fixed byte bound.")));
        }

        if (encoded.Length > 32768 && expectedRevision is null)
        {
            return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(
                new CacheError(CacheErrorCode.Capacity, "Initial operation metadata leaves insufficient space for bounded progress.")));
        }

        lock (this.operationGate)
        {
            this.PurgeTerminalOperationsLocked(this.timeProvider.GetUtcNow());
            if (this.operations.TryGetValue(operation.Id, out var existing))
            {
                if (expectedRevision is null || existing.Value.Revision != expectedRevision || !SameOperation(existing.Value, normalized) ||
                    !Transition(existing.Value.State, normalized.State))
                {
                    return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(Conflict()));
                }

                var changed = normalized with
                {
                    Revision = Guid.NewGuid().ToString("N"), CreatedAt = existing.Value.CreatedAt, UpdatedAt = this.timeProvider.GetUtcNow(),
                    Outcome = existing.Value.Outcome
                };
                existing.Value = changed;
                return ValueTask.FromResult(Result<CacheProviderOperation>.Success(changed));
            }

            if (expectedRevision is not null)
            {
                return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(
                    new CacheError(CacheErrorCode.NotFound, "The retained operation status is unavailable.")));
            }

            if (this.operations.Count >= this.diagnostics.MaximumOperationRecords)
            {
                var oldest = this.operations.Where(p => Terminal(p.Value.Value.State)).OrderBy(p => p.Value.Value.UpdatedAt).FirstOrDefault();
                if (oldest.Value is not null)
                {
                    oldest.Value.Charge.Dispose();
                    this.operations.Remove(oldest.Key);
                }
            }

            if (normalized.State != CacheOperationState.Queued || this.operations.Count >= this.diagnostics.MaximumOperationRecords ||
                !ResourceCharge.TryReserve(132 * CacheDefaults.KiB, out var charge, state.Resident, state.Metadata))
            {
                return ValueTask.FromResult(Result<CacheProviderOperation>.Failure(Capacity()));
            }

            var created = normalized with
            {
                Revision = Guid.NewGuid().ToString("N"), CreatedAt = this.timeProvider.GetUtcNow(), UpdatedAt = this.timeProvider.GetUtcNow()
            };
            this.operations.Add(created.Id, new(created, charge));
            return ValueTask.FromResult(Result<CacheProviderOperation>.Success(created));
        }
    }

    /// <summary>Gets retained operation state. Unknown or expired handles never implicitly start new work.</summary>
    public ValueTask<Result<CacheProviderOperation>> GetOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.operationGate)
        {
            this.PurgeTerminalOperationsLocked(this.timeProvider.GetUtcNow());
            return ValueTask.FromResult(this.operations.TryGetValue(operationId ?? "", out var operation)
                ? Result<CacheProviderOperation>.Success(operation.Value)
                : Result<CacheProviderOperation>.Failure(new CacheError(CacheErrorCode.NotFound, "The retained operation status is unavailable.")));
        }
    }

    /// <summary>Returns up to the requested recent records within a four-MiB metadata page, without payload reads.</summary>
    public ValueTask<Result<IReadOnlyList<CacheProviderOperation>>> GetOperationsAsync(int limit = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (limit is <= 0 or > 1000)
        {
            return ValueTask.FromResult(Result<IReadOnlyList<CacheProviderOperation>>.Failure(
                new CacheError(CacheErrorCode.Validation, "Operation page bounds are invalid.")));
        }

        lock (this.operationGate)
        {
            this.PurgeTerminalOperationsLocked(this.timeProvider.GetUtcNow());
            // Reserved maximums make the response bound independent of caller-supplied strings or continuation lengths.
            var records = this.operations.Values.OrderBy(o => Terminal(o.Value.State) ? 1 : 0).ThenBy(o => o.Value.UpdatedAt)
                .Take(Math.Min(limit, 4 * 1024 * 1024 / 65536)).Select(o => o.Value).ToArray();
            return ValueTask.FromResult(Result<IReadOnlyList<CacheProviderOperation>>.Success(Array.AsReadOnly(records)));
        }
    }

    private ValueTask<Result<CacheInvalidationOutcome>> InvalidateTrackedCore(CacheProviderInvalidation request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.operationGate)
        {
            if (!this.operations.TryGetValue(request.OperationId, out var tracked))
            {
                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(new CacheError(CacheErrorCode.NotFound, "Reserve tracked operation status before mutation.")));
            }

            var current = tracked.Value;
            if (!SameOperation(current, current with { Request = request, Cutoff = request.Cutoff, Collection = request.Collection }))
            {
                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(Conflict()));
            }

            if (current.State == CacheOperationState.Completed)
            {
                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(current.Outcome));
            }

            if (current.CancellationRequested || current.State == CacheOperationState.Cancelled)
            {
                tracked.Value = current with { State = CacheOperationState.Cancelled, Revision = Guid.NewGuid().ToString("N"), UpdatedAt = this.timeProvider.GetUtcNow() };
                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(current.Outcome ?? new() { OperationId = current.Id, Completed = false }));
            }

            if (current.State != CacheOperationState.Running || current.Outcome?.Continuation != request.Continuation)
            {
                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(Conflict()));
            }

            // This native in-process unit is synchronous and bounded. Once mutation begins, caller cancellation
            // cannot interrupt between effects and checkpoint. Other operation controls wait for the same gate.
            var result = this.InvalidateCore(request, CancellationToken.None).GetAwaiter().GetResult();
            if (result.IsSuccess)
            {
                tracked.Value = current with
                {
                    Outcome = result.Value, State = result.Value.Completed ? CacheOperationState.Completed : CacheOperationState.Pending,
                    Revision = Guid.NewGuid().ToString("N"), UpdatedAt = this.timeProvider.GetUtcNow()
                };
            }

            return ValueTask.FromResult(result);
        }
    }

    private void PurgeTerminalOperations(DateTimeOffset now)
    {
        lock (this.operationGate)
        {
            this.PurgeTerminalOperationsLocked(now);
        }
    }

    private void PurgeTerminalOperationsLocked(DateTimeOffset now)
    {
        foreach (var id in this.operations.Where(p => Terminal(p.Value.Value.State) &&
            now - p.Value.Value.UpdatedAt >= this.diagnostics.OperationRetention).Select(p => p.Key).ToArray())
        {
            this.operations[id].Charge.Dispose();
            this.operations.Remove(id);
        }
    }

    private static bool SameOperation(CacheProviderOperation left, CacheProviderOperation right) =>
        left.Collection == right.Collection && left.Cutoff == right.Cutoff &&
        left.Request.Namespace == right.Request.Namespace && left.Request.Collection == right.Request.Collection &&
        left.Request.Clear == right.Request.Clear &&
        (left.Request.Clear || SelectionHash(left.Request.Selection, default) == SelectionHash(right.Request.Selection, default));

    private static bool Transition(CacheOperationState previous, CacheOperationState next) =>
        previous == next || previous switch
        {
            CacheOperationState.Queued => next is CacheOperationState.Running or CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Failed,
            CacheOperationState.Running => next is CacheOperationState.Paused or CacheOperationState.Cancelled or CacheOperationState.Completed or CacheOperationState.Failed,
            CacheOperationState.Paused => next is CacheOperationState.Queued or CacheOperationState.Cancelled,
            CacheOperationState.Cancelled or CacheOperationState.Failed => next is CacheOperationState.Queued or CacheOperationState.Cancelled,
            _ => false
        };

    private static bool Terminal(CacheOperationState state) =>
        state is CacheOperationState.Completed or CacheOperationState.Cancelled or CacheOperationState.Failed;

    private sealed class TrackedOperation(CacheProviderOperation value, ResourceCharge charge)
    {
        /// <summary>Stores the current immutable tracked-operation record under the native operation gate.</summary>
        public CacheProviderOperation Value = value;

        /// <summary>Retains tracked-operation resident/metadata capacity until history reclamation.</summary>
        public readonly ResourceCharge Charge = charge;
    }

    private sealed class OperationMetadataStream(Stream inner, long maximum) : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => inner.Length;

        /// <inheritdoc />
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override void Flush() => inner.Flush();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximum - inner.Length)
            {
                throw new InvalidDataException("Operation metadata exceeds its fixed byte bound.");
            }

            inner.Write(buffer);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>Starts bounded owned staging while the previous visible version remains independently valid.</summary>
    public async ValueTask<Result<ICacheProviderWriteSession>> BeginWriteAsync(CacheProviderWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumStoredBytes <= 0 || (request.Condition.CreateOnly && request.Condition.ExpectedVersion.HasValue) ||
            (request.Condition.ExpectedVersion.HasValue && string.IsNullOrEmpty(request.Condition.ExpectedVersion.Value.Value)) ||
            (request.OperationId is not null && !Guid.TryParseExact(request.OperationId, "N", out _)))
        {
            return Result<ICacheProviderWriteSession>.Failure(new CacheError(CacheErrorCode.Validation, "Invalid bounded staging request."));
        }

        if (request.Owner.HasValue)
        {
            return Result<ICacheProviderWriteSession>.Failure(new CacheError(CacheErrorCode.Unsupported, "This provider has no cross-node source authority."));
        }

        if (!this.TryState(request.Identity, out var state, out var error))
        {
            return Result<ICacheProviderWriteSession>.Failure(error);
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(request.Identity.Key))
            {
                if (!state.IsFenceCurrent(request.Identity, request.Fence))
                {
                    return Result<ICacheProviderWriteSession>.Failure(Conflict());
                }
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }

        var admission = await state.Stages.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result<ICacheProviderWriteSession>.Failure(admission);
        }

        var hardStoredLimit = CacheSizePolicy.MaximumStoredBytes(state.Policies);
        if (Volatile.Read(ref this.disposed) != 0 ||
            !InMemoryCachePayload.TryCreate(state.Resident, state.Storage, this.transient,
                Math.Min(request.MaximumStoredBytes, hardStoredLimit), out var payload))
        {
            admission.Value.Dispose();
            return Result<ICacheProviderWriteSession>.Failure(Volatile.Read(ref this.disposed) != 0 ? Unavailable() : Capacity());
        }

        var session = new InMemoryCacheWriteSession(this, state, request, payload, admission.Value);
        if (!this.sessions.TryAdd(session.OperationId, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            return Result<ICacheProviderWriteSession>.Failure(Conflict());
        }

        return Result<ICacheProviderWriteSession>.Success(session);
    }

    /// <summary>Atomically renews a valid snapshot's activity and sliding expiry without changing its opaque payload version.</summary>
    public ValueTask<Result<CacheEntryMetadata>> TouchAsync(CacheEntryIdentity identity, CacheEntryVersion? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.TryState(identity, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheEntryMetadata>.Failure(error));
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(identity.Key))
            {
                var now = this.timeProvider.GetUtcNow();
                if (!state.Entries.TryGetValue(identity, out var entry) || !Valid(entry.Metadata, now))
                {
                    return ValueTask.FromResult(Result<CacheEntryMetadata>.Failure(
                        new CacheError(CacheErrorCode.NotFound, "No logically valid cache version exists.")));
                }

                var metadata = entry.Metadata;
                if (expectedVersion.HasValue && metadata.Version != expectedVersion.Value)
                {
                    return ValueTask.FromResult(Result<CacheEntryMetadata>.Failure(Conflict()));
                }

                var expires = metadata.SlidingIdleTimeout.HasValue ? Min(metadata.AbsoluteExpiresAt, now.Add(metadata.SlidingIdleTimeout.Value)) : metadata.ExpiresAt;
                var updated = metadata with { ExpiresAt = expires, LastAccessAt = now, HasForegroundAccess = true };
                Interlocked.Increment(ref state.ActiveMutations);
                try
                {
                    state.Index.UpdateExpiry(entry, metadata.ExpiresAt, expires);
                    entry.UpdateMetadata(updated);
                    entry.ObserveAccess(now);
                    Interlocked.Increment(ref state.Revision);
                }
                finally
                {
                    Interlocked.Decrement(ref state.ActiveMutations);
                }

                return ValueTask.FromResult(Result<CacheEntryMetadata>.Success(updated));
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }
    }

    /// <summary>Conditionally fences/removes a logical key across retained contract versions, including a currently absent key.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheEntryIdentity identity, CacheWriteCondition condition = default,
        CancellationToken cancellationToken = default) => this.RemoveAsync(new(identity, condition), cancellationToken);

    /// <summary>Conditionally removes an expected generation after confirming captured source authority and its original deadline.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> RemoveAsync(CacheProviderRemoval request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        var identity = request.Identity;
        var condition = request.Condition;
        if (!this.TryState(identity, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(error));
        }

        if (condition.CreateOnly || (request.OnlyExpectedVersion && !condition.ExpectedVersion.HasValue))
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(
                new CacheError(CacheErrorCode.Validation, "The removal condition does not identify a permitted exact mutation.")));
        }

        if (request.Owner.HasValue)
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(new CacheError(CacheErrorCode.Unsupported, "This provider has no shared-store source authority.")));
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(identity.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (request.Deadline <= this.timeProvider.GetUtcNow())
                {
                    return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The source removal deadline elapsed.")));
                }

                if (request.Fence.HasValue && !state.IsFenceCurrent(identity, request.Fence.Value))
                {
                    return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(Conflict()));
                }

                state.Entries.TryGetValue(identity, out var current);
                if (condition.ExpectedVersion.HasValue && (current is null || current.Metadata.Version != condition.ExpectedVersion.Value))
                {
                    return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(Conflict()));
                }

                state.FenceKey(identity.Key);
                if (request.OnlyExpectedVersion)
                {
                    return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(new()
                    {
                        Completed = true, AffectedCount = this.RemoveCurrent(state, current) ? 1 : 0
                    }));
                }

                var shard = InMemoryCacheIndex.ShardFor(identity.Key);
                string after = null;
                long removed = 0;
                while (true)
                {
                    var slice = state.Index.Find(CacheSelection.Key(identity.Key), shard, null, after, 256);
                    foreach (var candidate in slice.Candidates)
                    {
                        after = candidate.Entry.SortKey;
                        if (this.RemoveCurrent(state, candidate.Entry))
                        {
                            removed++;
                        }
                    }

                    if (!slice.HasMore)
                    {
                        break;
                    }
                }

                return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(new()
                {
                    Completed = true, AffectedCount = removed, CountIsExact = true
                }));
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }
    }

    /// <summary>Conditionally publishes a sealed session, updates indexes and transfers reservations under collection/key authority.</summary>
    public Result<CacheWriteOutcome> Commit(InMemoryCacheWriteSession session, CacheEntryMetadata supplied, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref this.disposed) != 0)
        {
            return Result<CacheWriteOutcome>.Failure(Unavailable());
        }

        var validation = this.PrepareMetadata(session, supplied);
        if (validation.IsFailure)
        {
            return Result<CacheWriteOutcome>.Failure(validation);
        }

        var metadata = validation.Value.Metadata;
        var state = session.State;
        var metadataBytes = validation.Value.Bytes;
        var indexCharge = checked(1024 + metadataBytes * 2 + metadata.Dimensions.Sum(d => 128L + Math.Max(1, d.Values.Count) * 256L) +
            session.Payload.ChunkCount * 128L);
        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(metadata.Identity.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (session.Request.Deadline <= this.timeProvider.GetUtcNow())
                {
                    return Result<CacheWriteOutcome>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The fixed publication deadline elapsed."));
                }

                if (Volatile.Read(ref this.disposed) != 0 || !state.IsFenceCurrent(metadata.Identity, session.Request.Fence))
                {
                    return Result<CacheWriteOutcome>.Failure(Conflict());
                }

                state.Entries.TryGetValue(metadata.Identity, out var previous);
                var fence = state.Fences[session.Request.Fence.Value];
                var unchanged = fence.PublicationSequence == fence.KeyState.LastPublications[metadata.Identity.ContractVersion];
                if ((session.Request.Condition.CreateOnly && (!unchanged || (previous is not null && !previous.Metadata.IsNotFound && Valid(previous.Metadata, this.timeProvider.GetUtcNow())))) ||
                    (session.Request.Condition.ExpectedVersion.HasValue &&
                        (!unchanged || (previous is null ? fence.CapturedVersion != session.Request.Condition.ExpectedVersion.Value :
                            previous.Metadata.Version != session.Request.Condition.ExpectedVersion.Value))))
                {
                    return Result<CacheWriteOutcome>.Failure(Conflict());
                }

                if (!ResourceCharge.TryReserve(indexCharge, out var charge, state.Resident, state.Metadata))
                {
                    this.ReclaimForAdmission(state, metadata.Identity.Key);
                    if (!ResourceCharge.TryReserve(indexCharge, out charge, state.Resident, state.Metadata))
                    {
                        return Result<CacheWriteOutcome>.Failure(Capacity());
                    }
                }

                ResourceReservation entryCount = null;
                if (previous is null && !state.Count.TryReserve(1, out entryCount))
                {
                    this.ReclaimForAdmission(state, metadata.Identity.Key);
                    if (!state.Count.TryReserve(1, out entryCount))
                    {
                        charge.Dispose();
                        return Result<CacheWriteOutcome>.Failure(Capacity());
                    }
                }

                var added = new InMemoryCacheEntry(metadata, metadata.IsNotFound ? null : session.Payload, charge,
                    entryCount, state.Epoch, metadataBytes + 128, Interlocked.Increment(ref state.PublicationSequence));
                Interlocked.Increment(ref state.ActiveMutations);
                try
                {
                    state.Index.Add(added);
                    state.Entries[metadata.Identity] = added;
                    fence.KeyState.LastPublications[metadata.Identity.ContractVersion] = added.PublicationSequence;
                    if (previous is not null)
                    {
                        added.AttachCount(previous.TakeCount());
                    }

                    Interlocked.Add(ref this.currentBytes, metadata.StoredSizeBytes - (previous?.Metadata.StoredSizeBytes ?? 0));
                    if (previous is not null)
                    {
                        state.Index.Remove(previous);
                        previous.Retire();
                    }

                    Interlocked.Increment(ref state.Revision);
                }
                catch
                {
                    if (!state.Entries.TryGetValue(metadata.Identity, out var visible) || !ReferenceEquals(visible, added))
                    {
                        state.Index.Remove(added);
                        entryCount?.Dispose();
                        charge.Dispose();
                    }

                    throw;
                }
                finally
                {
                    Interlocked.Decrement(ref state.ActiveMutations);
                }

                return Result<CacheWriteOutcome>.Success(new(CacheStorageOutcome.Stored, metadata.Version, metadata));
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }
    }

    /// <summary>Removes an actually disposed staging session from native ownership while preserving published payload ownership.</summary>
    public void SessionEnded(InMemoryCacheWriteSession session)
    {
        ((ICollection<KeyValuePair<string, InMemoryCacheWriteSession>>)this.sessions)
            .Remove(new(session.OperationId, session));
        if (Volatile.Read(ref this.disposed) != 0 && this.sessions.IsEmpty)
        {
            this.Cleanup();
        }
    }

    private Result<PreparedMetadata> PrepareMetadata(InMemoryCacheWriteSession session, CacheEntryMetadata supplied)
    {
        if (supplied is null || supplied.Identity != session.Request.Identity || supplied.Version != session.Version ||
            supplied.StoredSizeBytes != session.Payload.Length || supplied.LogicalSizeBytes < 0 || supplied.EncodedSizeBytes < 0 ||
            supplied.DecodedSizeBytes is < 0 || supplied.ItemCount is < 0 || supplied.Dimensions is null ||
            supplied.Dimensions.Count > 16 || !Enum.IsDefined(supplied.Priority) || supplied.EnvelopeVersion <= 0 ||
            supplied.CodecVersion <= 0)
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Invalid staged cache metadata or snapshot identity."));
        }

        var state = session.State;
        var limits = state.Policies.Limits;
        if (supplied.LogicalSizeBytes > limits.MaxLogicalBytes || supplied.EncodedSizeBytes > limits.MaxEncodedBytes ||
            supplied.DecodedSizeBytes > limits.MaxDecodedBytes)
        {
            return Result<PreparedMetadata>.Failure(Capacity());
        }

        var absolute = supplied.AbsoluteExpiresAt - supplied.PublishedAt;
        var initial = supplied.ExpiresAt - supplied.PublishedAt;
        var freshness = state.Policies.Expiration.SlidingEnabled ? state.Policies.Expiration.AbsoluteCap : state.Policies.Expiration.AbsoluteTtl;
        if (absolute <= TimeSpan.Zero || absolute > freshness || initial <= TimeSpan.Zero || initial > absolute ||
            (supplied.SlidingIdleTimeout.HasValue && (!state.Policies.Expiration.SlidingEnabled ||
                supplied.SlidingIdleTimeout.Value <= TimeSpan.Zero || supplied.SlidingIdleTimeout.Value > state.Policies.Expiration.SlidingIdleTimeout)) ||
            (supplied.IsNotFound && (!state.Policies.NegativeCaching.Enabled || absolute > state.Policies.NegativeCaching.Ttl ||
                supplied.StoredSizeBytes != 0 || supplied.EncodedSizeBytes != 0 || supplied.LogicalSizeBytes != 0)))
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Entry freshness or absence policy exceeds the collection contract."));
        }

        try
        {
            var definition = state.Definitions[supplied.Identity.ContractVersion];
            if (CacheIdentityNormalizer.Name(supplied.ContractId) != definition.ContractId ||
                CacheIdentityNormalizer.Key(supplied.DisplayKey ?? supplied.Identity.Key) != supplied.Identity.Key)
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Stored value alias or display identity does not match its binding."));
            }

            if (supplied.CodecId is not null)
            {
                _ = CacheIdentityNormalizer.Name(supplied.CodecId);
            }

            if (supplied.Compression is not null)
            {
                _ = CacheIdentityNormalizer.Name(supplied.Compression);
            }

            if ((supplied.IsNotFound && (supplied.PayloadHash is not null || supplied.StoredHash is not null)) ||
                (!supplied.IsNotFound && (!ValidHash(supplied.PayloadHash) || !ValidHash(supplied.StoredHash))))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Contract, "Invalid entry integrity metadata."));
            }

            var dimensions = CacheBoundedSnapshot.Copy(supplied.Dimensions, 16, nameof(supplied.Dimensions));
            var dimensionsValid = CacheSelectionValidator.ValidateDimensions(dimensions, definition.Dimensions);
            if (dimensionsValid.IsFailure)
            {
                return Result<PreparedMetadata>.Failure(dimensionsValid);
            }

            var characters = supplied.Identity.Key.Length + (supplied.DisplayKey?.Length ?? 0) +
                (supplied.ContentType?.Length ?? 0) + dimensions.Sum(d => d.Name.Length + d.Values.Sum(v => v.CanonicalValue?.Length ?? 0));
            if (characters > limits.MaxEntryMetadataBytes || supplied.ContentType?.Length > 256 ||
                (supplied.ContentType is not null && !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(supplied.ContentType, out _)))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Entry metadata exceeds its portable bounds."));
            }

            if (!supplied.IsNotFound && (supplied.StoredHash?.Length != 64 ||
                !StringComparer.OrdinalIgnoreCase.Equals(supplied.StoredHash, session.Payload.StoredHash())))
            {
                return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Corruption, "Staged representation integrity failed."));
            }

            var now = this.timeProvider.GetUtcNow();
            var metadata = supplied with
            {
                Dimensions = dimensions, ContractId = definition.ContractId,
                DisplayKey = supplied.DisplayKey ?? supplied.Identity.Key,
                PublishedAt = now, LastAccessAt = supplied.HasForegroundAccess ? Min(supplied.LastAccessAt, now) : now,
                AbsoluteExpiresAt = now.Add(absolute), ExpiresAt = now.Add(initial),
                Priority = supplied.IsNotFound ? CacheEvictionPriority.Low : supplied.Priority
            };
            if (!this.transient.TryReserve(1024 * 1024, out var scratch))
            {
                return Result<PreparedMetadata>.Failure(Capacity());
            }

            long serializedBytes;
            using (scratch)
            {
                serializedBytes = JsonSerializer.SerializeToUtf8Bytes(metadata).LongLength;
                if (serializedBytes + 128 > limits.MaxEntryMetadataBytes)
                {
                    return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Final serialized entry metadata exceeds its byte bound."));
                }
            }

            return Result<PreparedMetadata>.Success(new(metadata, serializedBytes));
        }
        catch (ArgumentException)
        {
            return Result<PreparedMetadata>.Failure(new CacheError(CacheErrorCode.Validation, "Entry metadata contains invalid identifiers or values."));
        }
    }

    // The caller owns the key gate and a publication read/write lock.
    private bool RemoveCurrent(InMemoryCachePublicationState state, InMemoryCacheEntry candidate)
    {
        var identity = candidate.Metadata.Identity;
        if (!state.Entries.TryGetValue(identity, out var current) || !ReferenceEquals(current, candidate))
        {
            return false;
        }

        Interlocked.Increment(ref state.ActiveMutations);
        try
        {
            state.Index.Remove(current);
            state.Entries.TryRemove(identity, out _);
            Interlocked.Add(ref this.currentBytes, -current.Metadata.StoredSizeBytes);
            current.Retire();
            Interlocked.Increment(ref state.Revision);
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref state.ActiveMutations);
        }
    }

    private readonly record struct PreparedMetadata(CacheEntryMetadata Metadata, long Bytes);

    private static bool ValidHash(string hash) => hash?.Length == 64 && hash.All(char.IsAsciiHexDigit);

    private static CacheError Conflict() => new(CacheErrorCode.Conflict, "The acquired version, publication fence, or ownership changed.");

    /// <summary>Returns one bounded payload-free metadata page across retained contract versions. Explicit continuation starts another bounded request.</summary>
    public ValueTask<Result<CacheMetadataPage>> QueryAsync(CacheMetadataQuery query, CancellationToken cancellationToken = default)
    {
        return this.WithMetadataAdmission(() => this.QueryCore(query, cancellationToken), cancellationToken);
    }

    private ValueTask<Result<CacheMetadataPage>> QueryCore(CacheMetadataQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (!this.TryCollection(query.Namespace, query.Collection, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheMetadataPage>.Failure(error));
        }

        var validation = ValidateQuery(state, query);
        if (validation.IsFailure)
        {
            return ValueTask.FromResult(Result<CacheMetadataPage>.Failure(validation));
        }

        try
        {
            var scope = Scope(state);
            var fingerprint = SelectionHash(query.Selection, cancellationToken);
            var cursor = query.Continuation is null ? NewCursor(state, query, scope, fingerprint, "query") :
                this.DecodeCursor(query.Continuation, scope, fingerprint, query.Cutoff, "query", null);
            var page = this.ReadPage(state, query, cursor, cancellationToken);
            var next = page.Page.Completed ? null : this.EncodeCursor(page.Cursor);
            return ValueTask.FromResult(Result<CacheMetadataPage>.Success(page.Page with { Continuation = next }));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or InvalidDataException)
        {
            return ValueTask.FromResult(Result<CacheMetadataPage>.Failure(
                new CacheError(CacheErrorCode.Validation, "The metadata query or continuation is invalid.")));
        }
    }

    /// <summary>Establishes source-publication fences, then conditionally invalidates one bounded batch with resumable progress.</summary>
    public ValueTask<Result<CacheInvalidationOutcome>> InvalidateAsync(CacheProviderInvalidation request,
        CancellationToken cancellationToken = default)
    {
        return this.WithMetadataAdmission(() => request?.OperationId is null ? this.InvalidateCore(request, cancellationToken) :
            this.InvalidateTrackedCore(request, cancellationToken), cancellationToken);
    }

    private ValueTask<Result<CacheInvalidationOutcome>> InvalidateCore(CacheProviderInvalidation request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (!this.TryCollection(request.Namespace, request.Collection, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(error));
        }

        var selection = request.Clear ? CacheSelection.Pattern("*") : request.Selection;
        var query = new CacheMetadataQuery(request.Namespace, request.Collection, selection, request.Cutoff,
            Math.Min(256, state.Policies.Limits.MaxMetadataPageSize), MaximumCandidates: state.Policies.Limits.MaxQueryCandidates);
        var validation = ValidateQuery(state, query);
        if (validation.IsFailure)
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(validation));
        }

        if (selection.IsEmpty)
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(new() { Completed = true, CountIsExact = true }));
        }

        try
        {
            var scope = Scope(state);
            var fingerprint = SelectionHash(selection, cancellationToken);
            var purpose = request.Clear ? "clear" : "invalidate";
            MetadataCursor cursor;
            if (request.Continuation is not null)
            {
                cursor = this.DecodeCursor(request.Continuation, scope, fingerprint, request.Cutoff, purpose, request.OperationId);
            }
            else
            {
                state.Publication.EnterWriteLock();
                try
                {
                    if (Volatile.Read(ref this.disposed) != 0)
                    {
                        return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(Unavailable()));
                    }

                    if (!request.Clear && selection.Kind == CacheSelectionKind.Key)
                    {
                        lock (state.KeyGate(selection.Text))
                        {
                            state.FenceKey(selection.Text);
                        }
                    }
                    else
                    {
                        state.Epoch = checked(state.Epoch + 1);
                    }

                    cursor = NewCursor(state, query, scope, fingerprint, purpose) with
                    {
                        MaximumPublicationSequence = Interlocked.Read(ref state.PublicationSequence),
                        OperationId = request.OperationId
                    };
                }
                finally
                {
                    state.Publication.ExitWriteLock();
                }
            }

            var page = this.ReadPage(state, query, cursor, cancellationToken);
            var removed = 0L;
            foreach (var metadata in page.Page.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.Publication.EnterReadLock();
                try
                {
                    lock (state.KeyGate(metadata.Identity.Key))
                    {
                        if (state.Entries.TryGetValue(metadata.Identity, out var entry) && entry.Metadata.Version == metadata.Version &&
                            entry.PublicationSequence <= cursor.MaximumPublicationSequence && this.RemoveCurrent(state, entry))
                        {
                            removed++;
                        }
                    }
                }
                finally
                {
                    state.Publication.ExitReadLock();
                }
            }

            cursor = page.Cursor with { Affected = cursor.Affected + removed };
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Success(new()
            {
                Completed = page.Page.Completed, AffectedCount = cursor.Affected, CountIsExact = true,
                Continuation = page.Page.Completed ? null : this.EncodeCursor(cursor),
                OperationId = request.OperationId
            }));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or InvalidDataException)
        {
            return ValueTask.FromResult(Result<CacheInvalidationOutcome>.Failure(
                new CacheError(CacheErrorCode.Validation, "The invalidation request or continuation is invalid.")));
        }
    }

    private NativePage ReadPage(InMemoryCachePublicationState state, CacheMetadataQuery query, MetadataCursor initial,
        CancellationToken cancellationToken)
    {
        var cursor = initial;
        var started = this.timeProvider.GetTimestamp();
        var budget = Math.Min(query.MaximumCandidates, state.Policies.Limits.MaxQueryCandidates);
        var maximum = Math.Min(query.PageSize, state.Policies.Limits.MaxMetadataPageSize);
        var entries = new List<CacheEntryMetadata>(maximum);
        var evaluator = new CacheSelectionEvaluator(query.Selection, state.Definitions.Values.First().Dimensions, query.Cutoff);
        var scanned = 0;
        var bytes = 0L;
        string reason = null;
        while (cursor.Shard < 256)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (scanned >= budget || this.timeProvider.GetElapsedTime(started) >= state.Policies.Limits.MaxQueryWork)
            {
                reason = scanned >= budget ? "CandidateLimit" : "WorkLimit";
                break;
            }

            InMemoryCacheIndexSlice slice;
            state.Publication.EnterReadLock();
            try
            {
                slice = state.Index.Find(query.Selection, cursor.Shard, cursor.AfterOrder, cursor.AfterKey, Math.Min(256, budget - scanned));
            }
            finally
            {
                state.Publication.ExitReadLock();
            }

            var allProcessed = true;
            foreach (var candidate in slice.Candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (this.timeProvider.GetElapsedTime(started) >= state.Policies.Limits.MaxQueryWork)
                {
                    reason = "WorkLimit";
                    allProcessed = false;
                    break;
                }

                var entry = candidate.Entry;
                var metadata = entry.Metadata;
                var selected = state.Entries.TryGetValue(metadata.Identity, out var current) && ReferenceEquals(current, entry) &&
                    (!cursor.MaximumPublicationSequence.HasValue || entry.PublicationSequence <= cursor.MaximumPublicationSequence.Value) &&
                    evaluator.Matches(metadata);
                if (selected && bytes + entry.MetadataBytes > state.Policies.Limits.MaxMetadataPageBytes)
                {
                    allProcessed = false;
                    break;
                }

                scanned++;
                cursor = cursor with { AfterKey = entry.SortKey, AfterOrder = candidate.OrderValue };
                if (selected)
                {
                    bytes += entry.MetadataBytes;
                    if (entry.LastAccessTicks > metadata.LastAccessAt.UtcTicks || entry.HasForegroundAccess != metadata.HasForegroundAccess)
                    {
                        metadata = metadata with { LastAccessAt = new(entry.LastAccessTicks, TimeSpan.Zero), HasForegroundAccess = entry.HasForegroundAccess };
                    }

                    entries.Add(metadata);
                    cursor = cursor with { Observed = cursor.Observed + 1 };
                    if (entries.Count == maximum)
                    {
                        allProcessed = ReferenceEquals(candidate.Entry, slice.Candidates[^1].Entry);
                        break;
                    }
                }
            }

            if (allProcessed && !slice.HasMore)
            {
                cursor = cursor with { Shard = query.Selection.Kind == CacheSelectionKind.Key ? 256 : cursor.Shard + 1, AfterKey = null, AfterOrder = null };
            }

            if (entries.Count == maximum || !allProcessed || reason is not null)
            {
                break;
            }
        }

        var completed = cursor.Shard >= 256 || query.Selection.IsEmpty;
        cursor = cursor with { Changed = cursor.Changed || cursor.Revision != Interlocked.Read(ref state.Revision) ||
            Volatile.Read(ref state.ActiveMutations) != 0 };
        return new(new(entries.AsReadOnly(), null, cursor.Observed, completed && !cursor.Changed, scanned, completed, reason), cursor);
    }

    private async ValueTask<Result<T>> WithMetadataAdmission<T>(Func<ValueTask<Result<T>>> action, CancellationToken cancellationToken)
    {
        var admission = await this.queries.AcquireAsync(this.options.AttemptTimeout, cancellationToken).ConfigureAwait(false);
        if (admission.IsFailure)
        {
            return Result<T>.Failure(admission);
        }

        using var lease = admission.Value;
        if (!this.transient.TryReserve(CacheDefaults.MiB, out var scratch))
        {
            return Result<T>.Failure(Capacity());
        }

        using (scratch)
        {
            return await action().ConfigureAwait(false);
        }
    }

    private static Result ValidateQuery(InMemoryCachePublicationState state, CacheMetadataQuery query)
    {
        if (query.Selection is null || query.PageSize <= 0 || query.PageSize > state.Policies.Limits.MaxMetadataPageSize ||
            query.MaximumCandidates <= 0 || query.MaximumCandidates > 100000)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Validation, "Metadata query bounds are invalid."));
        }

        foreach (var definition in state.Definitions.Values)
        {
            var validation = CacheSelectionValidator.Validate(query.Selection, definition.Dimensions);
            if (validation.IsFailure)
            {
                return Result.Failure(new CacheError(CacheErrorCode.Compatibility,
                    "The selection is invalid or incompatible with a retained contract schema."));
            }
        }

        return Result.Success();
    }

    private bool TryCollection(string @namespace, string collection, out InMemoryCachePublicationState state, out CacheError error)
    {
        state = null;
        error = null;
        if (Volatile.Read(ref this.disposed) != 0 || Volatile.Read(ref this.ready) == 0)
        {
            error = Unavailable();
            return false;
        }

        try
        {
            var identity = new CacheEntryIdentity(@namespace, collection, "catalog", "catalog");
            if (!this.states.TryGetValue(identity.CollectionIdentity, out state))
            {
                error = new(CacheErrorCode.Contract, "The logical collection was not bound during readiness.");
            }
        }
        catch (ArgumentException)
        {
            error = new(CacheErrorCode.Validation, "The collection scope is invalid.");
        }

        return error is null;
    }

    private static MetadataCursor NewCursor(InMemoryCachePublicationState state, CacheMetadataQuery query,
        string scope, string fingerprint, string purpose) => new(1, scope, fingerprint, query.Cutoff.UtcTicks,
            purpose, query.Selection.Kind == CacheSelectionKind.Key ? InMemoryCacheIndex.ShardFor(query.Selection.Text) : 0,
            null, null, 0, 0, Interlocked.Read(ref state.Revision), Volatile.Read(ref state.ActiveMutations) != 0,
            null, null);

    private const string CursorPurpose = "BDK.ApplicationCaching.InMemory.Cursor.V1";

    private string EncodeCursor(MetadataCursor cursor)
    {
        var strings = new[] { cursor.Scope, cursor.SelectionHash, cursor.Purpose, cursor.AfterKey, cursor.OperationId };
        var length = 43L + (cursor.AfterOrder.HasValue ? 8 : 0) + (cursor.MaximumPublicationSequence.HasValue ? 8 : 0) +
            strings.Sum(value => 4L + (value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value)));
        if (length > AesGcmContinuationTokenProtector.MaximumPlaintextBytes) { throw new InvalidDataException("Native continuation bound exceeded."); }

        using var body = new MemoryStream((int)length);
        using var writer = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(cursor.Format);
        WriteCursorString(writer, cursor.Scope);
        WriteCursorString(writer, cursor.SelectionHash);
        writer.Write(cursor.Cutoff);
        WriteCursorString(writer, cursor.Purpose);
        writer.Write(cursor.Shard);
        WriteCursorString(writer, cursor.AfterKey);
        WriteCursorNumber(writer, cursor.AfterOrder);
        writer.Write(cursor.Observed);
        writer.Write(cursor.Affected);
        writer.Write(cursor.Revision);
        writer.Write(cursor.Changed);
        WriteCursorNumber(writer, cursor.MaximumPublicationSequence);
        WriteCursorString(writer, cursor.OperationId);
        writer.Flush();
        var plaintext = body.GetBuffer().AsSpan(0, checked((int)body.Length));
        try { return Base64(AesGcmContinuationTokenProtector.Protect(plaintext, this.continuationKey, CursorPurpose)); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private MetadataCursor DecodeCursor(string value, string scope, string fingerprint, DateTimeOffset cutoff,
        string purpose, string operationId)
    {
        if (value is null || value.Length > (AesGcmContinuationTokenProtector.MaximumProtectedBytes + 2) / 3 * 4)
        {
            throw new InvalidDataException("Native continuation bound exceeded.");
        }

        byte[] body;
        try { body = AesGcmContinuationTokenProtector.Unprotect(Unbase64(value), this.continuationKey, CursorPurpose); }
        catch (CryptographicException exception) { throw new InvalidDataException("Native continuation authentication failed.", exception); }

        try
        {
            using var input = new MemoryStream(body, writable: false);
            using var reader = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
            var cursor = new MetadataCursor(reader.ReadInt32(), ReadCursorString(reader), ReadCursorString(reader), reader.ReadInt64(),
                ReadCursorString(reader), reader.ReadInt32(), ReadCursorString(reader), ReadCursorNumber(reader), reader.ReadInt64(),
                reader.ReadInt64(), reader.ReadInt64(), reader.ReadBoolean(), ReadCursorNumber(reader), ReadCursorString(reader));
            if (input.Position != input.Length || cursor.Format != 1 || cursor.Scope != scope || cursor.SelectionHash != fingerprint || cursor.Cutoff != cutoff.UtcTicks ||
                cursor.Purpose != purpose || cursor.OperationId != operationId || cursor.Shard is < 0 or > 256)
            {
                throw new InvalidDataException("Native continuation does not match the current operation.");
            }

            return cursor;
        }
        catch (EndOfStreamException exception) { throw new InvalidDataException("Native continuation is incomplete.", exception); }
        finally { CryptographicOperations.ZeroMemory(body); }
    }

    private static void WriteCursorString(BinaryWriter writer, string value)
    {
        if (value is null) { writer.Write(-1); return; }

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadCursorString(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if (length == -1) { return null; }

        if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) { throw new InvalidDataException("Invalid native continuation field."); }

        return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    private static void WriteCursorNumber(BinaryWriter writer, long? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue) { writer.Write(value.Value); }
    }

    private static long? ReadCursorNumber(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadInt64() : null;

    private static string Scope(InMemoryCachePublicationState state) =>
        new CacheEntryIdentity(state.Namespace, state.Collection, "catalog", "catalog").CollectionIdentity;

    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Unbase64(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') +
        new string('=', (4 - value.Length % 4) % 4));

    private static string SelectionHash(CacheSelection selection, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var sink = new SelectionHashStream(hash, cancellationToken);
        JsonSerializer.Serialize(sink, selection);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private sealed record MetadataCursor(int Format, string Scope, string SelectionHash, long Cutoff, string Purpose,
        int Shard, string AfterKey, long? AfterOrder, long Observed, long Affected, long Revision, bool Changed,
        long? MaximumPublicationSequence, string OperationId);

    private readonly record struct NativePage(CacheMetadataPage Page, MetadataCursor Cursor);

    private sealed class SelectionHashStream(IncrementalHash hash, CancellationToken cancellationToken) : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        /// <inheritdoc />
        public override void Flush() { }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => this.Write(buffer.AsSpan(offset, count));

        /// <inheritdoc />
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public ValueTask<Result<CacheRefreshPage>> QueryRefreshAsync(CacheRefreshQuery query, CancellationToken cancellationToken = default) =>
        this.WithMetadataAdmission(() => this.QueryRefreshCore(query, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result> RecordRefreshAttemptAsync(CacheRefreshAttempt attempt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(attempt);
        if (!this.TryState(attempt.Identity, out var state, out var error))
        {
            return ValueTask.FromResult(Result.Failure(error));
        }

        state.Publication.EnterReadLock();
        try
        {
            lock (state.KeyGate(attempt.Identity.Key))
            {
                if (state.Entries.TryGetValue(attempt.Identity, out var entry) && entry.Metadata.Version == attempt.Version)
                {
                    var options = state.Definitions[attempt.Identity.ContractVersion].Policies.Refresh;
                    var failures = attempt.SourceFailed ? Math.Min(options.FailuresBeforePause, entry.Metadata.RefreshFailureCount + 1) : 0;
                    entry.UpdateMetadata(entry.Metadata with
                    {
                        RefreshFailureCount = failures,
                        RefreshNotBefore = attempt.SourceFailed ? CacheRefreshPolicy.NextAttempt(options, failures, attempt.CompletedAt) : null
                    });
                }
            }
        }
        finally
        {
            state.Publication.ExitReadLock();
        }

        return ValueTask.FromResult(Result.Success());
    }

    private ValueTask<Result<CacheRefreshPage>> QueryRefreshCore(CacheRefreshQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        if (!this.TryCollection(query.Namespace, query.Collection, out var state, out var error))
        {
            return ValueTask.FromResult(Result<CacheRefreshPage>.Failure(error));
        }

        if (query.Limit is <= 0 or > 1000 || query.RefreshAhead <= TimeSpan.Zero || query.ActivityWindow <= TimeSpan.Zero ||
            !state.Definitions.ContainsKey(CacheIdentityNormalizer.ContractVersion(query.ContractVersion)))
        {
            return ValueTask.FromResult(Result<CacheRefreshPage>.Failure(new CacheError(CacheErrorCode.Validation, "Invalid refresh query bounds or contract.")));
        }

        try
        {
            var scope = Scope(state);
            var fingerprint = "refresh:" + CacheIdentityNormalizer.ContractVersion(query.ContractVersion) + ":" + query.RefreshAhead.Ticks + ":" + query.ActivityWindow.Ticks;
            var cursor = query.Continuation is null ? new MetadataCursor(1, scope, fingerprint, query.Now.UtcTicks, "refresh", 0, null, null, 0, 0, 0, false, null, null) :
                this.DecodeCursor(query.Continuation, scope, fingerprint, query.Now, "refresh", null);
            var entries = new List<CacheEntryMetadata>();
            var examined = 0;
            var bytes = 0L;
            var started = this.timeProvider.GetTimestamp();
            var limit = Math.Min(query.Limit, state.Policies.Limits.MaxQueryCandidates);
            var upper = query.Now.UtcTicks > DateTimeOffset.MaxValue.UtcTicks - query.RefreshAhead.Ticks ? DateTimeOffset.MaxValue.UtcTicks : query.Now.UtcTicks + query.RefreshAhead.Ticks;
            while (cursor.Shard < 256 && examined < limit && this.timeProvider.GetElapsedTime(started) < state.Policies.Limits.MaxQueryWork)
            {
                cancellationToken.ThrowIfCancellationRequested();
                InMemoryCacheIndexSlice slice;
                state.Publication.EnterReadLock();
                try
                {
                    slice = state.Index.FindRefresh(cursor.Shard, query.Now.UtcTicks + 1, upper, cursor.AfterOrder, cursor.AfterKey, limit - examined);
                }
                finally
                {
                    state.Publication.ExitReadLock();
                }

                var full = false;
                foreach (var candidate in slice.Candidates)
                {
                    var entry = candidate.Entry;
                    if (bytes + entry.MetadataBytes > state.Policies.Limits.MaxMetadataPageBytes)
                    {
                        full = true;
                        break;
                    }

                    examined++;
                    cursor = cursor with { AfterOrder = candidate.OrderValue, AfterKey = entry.SortKey };
                    var metadata = entry.Metadata with { LastAccessAt = new(entry.LastAccessTicks, TimeSpan.Zero), HasForegroundAccess = entry.HasForegroundAccess };
                    if (state.Entries.TryGetValue(metadata.Identity, out var current) && ReferenceEquals(current, entry) && CacheRefreshPolicy.IsEligible(metadata, query))
                    {
                        entries.Add(metadata);
                        bytes += entry.MetadataBytes;
                    }
                }

                if (full) { break; }

                if (!slice.HasMore) { cursor = cursor with { Shard = cursor.Shard + 1, AfterOrder = null, AfterKey = null }; }
            }

            return ValueTask.FromResult(Result<CacheRefreshPage>.Success(new(entries.AsReadOnly(), cursor.Shard == 256 ? null : this.EncodeCursor(cursor), examined)));
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or JsonException or InvalidDataException)
        {
            return ValueTask.FromResult(Result<CacheRefreshPage>.Failure(new CacheError(CacheErrorCode.Validation, "The refresh query continuation is invalid.")));
        }
    }
}