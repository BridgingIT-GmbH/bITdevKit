// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Security.Cryptography;

/// <summary>Stores bounded Runtime and Operation history in one process-local provider scope.</summary>
/// <example><code>IProfilingStorageProvider provider = new InMemoryProfilingStorageProvider();</code></example>
public sealed partial class InMemoryProfilingStorageProvider : IProfilingStorageProvider, IOperationProfilingStore
{
    private readonly object sync = new();
    private readonly TimeProvider clock;
    private readonly OperationProfilingOptions options;
    private readonly ProfilingStorageOptions storageOptions;
    private readonly ProfilingQueryOptions queryOptions;
    private readonly ProfilingOperationQueryCodec queryCodec;
    private readonly InMemoryRuntimeProfilingStore runtime;
    private readonly ProfilingWriterRegistry writers;
    private readonly ProfilingClearCoordinator clears;
    private readonly Guid epoch = Guid.NewGuid();
    private readonly byte[] querySecret = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<Guid, StoredOperation> records = [];
    private readonly Dictionary<(Guid Writer, long Sequence), Guid> sequences = [];
    private readonly SortedSet<StoredOperation> completionOrder = new(Comparer<StoredOperation>.Create((left, right) =>
    {
        var comparison = left.Envelope.Record.CompletedUtc.CompareTo(right.Envelope.Record.CompletedUtc);
        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left.CanonicalId, right.CanonicalId);
    }));
    private readonly Dictionary<Guid, int> nodeOwners = [];
    private long retainedBytes;
    private long watermark;
    private long deletionRevision;
    private long capacityEvictionsToReport;

    /// <summary>Creates an isolated ephemeral provider with immutable bounded options.</summary>
    /// <example><code>var provider = new InMemoryProfilingStorageProvider(options, TimeProvider.System);</code></example>
    public InMemoryProfilingStorageProvider(ProfilingOptions options = null, TimeProvider clock = null)
    {
        options ??= new ProfilingOptions();
        options.Validate();
        this.options = options.Operations.Snapshot();
        this.options.Enabled = true;
        this.options.Validate();
        this.storageOptions = options.Storage.Snapshot();
        this.storageOptions.Validate();
        this.queryOptions = new ProfilingQueryOptions
        {
            PageSize = options.Queries.PageSize, MaximumPageSize = options.Queries.MaximumPageSize, MaximumConcurrentQueries = options.Queries.MaximumConcurrentQueries,
            MaximumNodeChoices = options.Queries.MaximumNodeChoices,
            MaximumAnalysisRecords = options.Queries.MaximumAnalysisRecords, MaximumDimensionPredicates = options.Queries.MaximumDimensionPredicates,
            MaximumGroupingDimensions = options.Queries.MaximumGroupingDimensions, MaximumOverlaySnapshots = options.Queries.MaximumOverlaySnapshots,
            Timeout = options.Queries.Timeout, BoundaryLifetime = options.Queries.BoundaryLifetime,
        };
        this.queryOptions.Validate();
        this.clock = clock ?? TimeProvider.System;
        this.queryCodec = new(this.options, this.queryOptions, this.clock, this.querySecret);
        this.Capabilities = new() { Name = "InMemory", Scope = $"process:{Environment.ProcessId}:{this.epoch:N}", Shared = false, OperationRetention = new() { MaximumCount = this.options.MaximumRetainedOperations, MaximumBytes = this.options.MaximumRetainedBytes, MaximumAge = this.options.MaximumOperationAge } };
        this.runtime = new InMemoryRuntimeProfilingStore(this.sync);
        this.runtime.SharedClear = ct => this.ClearAsync(new ProfilingClearRequest { DataSet = ProfilingDataSet.Runtime }, ct);
        this.writers = new(this.epoch, this.storageOptions.MaximumWriterLeases, nodeId => this.runtime.ReleaseSharedNode(nodeId, this.nodeOwners.ContainsKey(nodeId)));
        this.clears = new(this.storageOptions, this.Capabilities.Scope);
        this.runtime.OperationNodeReferenced = nodeId => this.nodeOwners.ContainsKey(nodeId) || this.writers.ReferencesNode(nodeId, this.clock.GetUtcNow());
        this.runtime.RootDeleted = () => this.deletionRevision = checked(this.deletionRevision + 1);
    }

    /// <inheritdoc />
    public ProfilingProviderCapabilities Capabilities { get; }
    /// <inheritdoc />
    public IRuntimeProfilingStore Runtime => this.runtime;
    /// <inheritdoc />
    public IOperationProfilingStore Operations => this;

    /// <inheritdoc />
    public Task<IResult<ProfilingWriterLease>> OpenWriterAsync(ProfilingOpenWriterRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return Completed(this.writers.Open(request, this.clock.GetUtcNow(), this.options.WriterLeaseDuration));
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingWriterSynchronizationResult>> SynchronizeWriterAsync(ProfilingWriterSynchronizationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request?.Lease is null || request.SettledThrough < 0 || request.LeaseDuration <= TimeSpan.Zero || request.LeaseDuration > this.options.WriterLeaseDuration)
        {
            return Failure<ProfilingWriterSynchronizationResult>(new ProfilingValidationError("Synchronization requires an original lease, nonnegative settlement and bounded renewal."));
        }

        lock (this.sync)
        {
            var utc = this.clock.GetUtcNow();
            var result = this.writers.Synchronize(request, utc, this.clears.Pending(request.Lease, utc));
            this.clears.Prune(this.writers, utc);
            this.clears.Current?.Pulse();
            return Success(result);
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingClearAcknowledgement>> AcknowledgeClearAsync(ProfilingClearAcknowledgement request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return Completed(this.clears.Acknowledge(request, this.writers, this.clock.GetUtcNow()));
        }
    }

    /// <inheritdoc />
    public Task<IResult> CloseWriterAsync(ProfilingWriterLease lease, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            this.writers.Close(lease, this.clock.GetUtcNow());
            this.writers.Prune(this.clock.GetUtcNow());
            this.clears.Current?.Pulse();
            return Task.FromResult<IResult>(Result.Success());
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingBatchWriteResult>> AppendAsync(IReadOnlyList<ProfilingWriteEnvelope> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = records?.Count ?? -1;
        if (count < 0 || count > this.options.BatchSize)
        {
            return Failure<ProfilingBatchWriteResult>(new ProfilingValidationError("The profiling batch exceeds its record bound."));
        }

        var results = new ProfilingRecordWriteResult[count];
        long batchBytes = 0;
        for (var index = 0; index < results.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (this.sync)
            {
                results[index] = this.AppendOne(records[index], ref batchBytes);
            }
        }

        return Success(new ProfilingBatchWriteResult { Records = Array.AsReadOnly(results) });
    }

    private ProfilingRecordWriteResult AppendOne(ProfilingWriteEnvelope envelope, ref long batchBytes)
    {
        var disposition = new ProfilingRecordWriteResult { OperationId = envelope?.Record?.Id ?? Guid.Empty, CompletionSequence = envelope?.CompletionSequence ?? 0 };
        if (envelope?.Record is null || envelope.CompletionSequence <= 0 || envelope.Record.NodeId != envelope.Lease?.NodeId)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "InvalidEnvelope" };
        }

        var utc = this.clock.GetUtcNow();
        var writer = this.writers.Find(envelope.Lease, utc);
        if (writer is null)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.StaleWriter };
        }

        if (this.clears.Fenced(envelope))
        {
            return disposition with { Outcome = ProfilingWriteOutcome.Cleared };
        }

        if (this.records.TryGetValue(envelope.Record.Id, out var stored))
        {
            return stored.Envelope.Lease.WriterId == envelope.Lease.WriterId && stored.Envelope.CompletionSequence == envelope.CompletionSequence
                ? disposition with { Outcome = ProfilingWriteOutcome.AlreadyStored, CommitWatermark = stored.CommitWatermark }
                : disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "OperationIdentityConflict" };
        }

        if (envelope.CompletionSequence <= writer.Lease.SettledThrough)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.AlreadySettled };
        }

        if (this.sequences.ContainsKey((envelope.Lease.WriterId, envelope.CompletionSequence)))
        {
            return disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "SequenceIdentityConflict" };
        }

        OperationProfilingRecord record;
        try
        {
            record = ProfilingRecordSnapshot.Copy(envelope.Record, this.options);
        }
        catch (Exception)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "InvalidRecord" };
        }

        if (record.EstimatedPayloadBytes > this.options.BatchBytes - batchBytes)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.CapacityRejected, SafeCode = "BatchPayload" };
        }

        batchBytes += record.EstimatedPayloadBytes;
        if (record.CompletedUtc < utc.Subtract(this.options.MaximumOperationAge))
        {
            return disposition with { Outcome = ProfilingWriteOutcome.RetentionRejected, SafeCode = "RetentionAge" };
        }

        if (this.runtime.RegisterOperationNode(record.Node, validateOnly: true).IsFailure)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "NodeIdentityConflict" };
        }

        if (!this.MakeRoom(record.EstimatedPayloadBytes, utc))
        {
            return disposition with { Outcome = ProfilingWriteOutcome.CapacityRejected, SafeCode = "RetentionCapacity" };
        }

        var sharedNode = this.runtime.RegisterOperationNode(record.Node);
        if (sharedNode.IsFailure)
        {
            return disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "NodeIdentityConflict" };
        }

        record = record with { Node = sharedNode.Value };
        var owned = envelope with { Record = record };
        var position = checked(++this.watermark);
        var storedRoot = new StoredOperation(owned, position);
        this.records.Add(record.Id, storedRoot);
        this.completionOrder.Add(storedRoot);
        this.sequences.Add((envelope.Lease.WriterId, envelope.CompletionSequence), record.Id);
        this.retainedBytes += record.EstimatedPayloadBytes;
        this.nodeOwners[record.NodeId] = this.nodeOwners.GetValueOrDefault(record.NodeId) + 1;
        return disposition with { Outcome = ProfilingWriteOutcome.Accepted, CommitWatermark = position };
    }

    /// <inheritdoc />
    public Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            return Success(this.records.TryGetValue(id, out var stored) ? stored.Envelope.Record : null);
        }
    }

    /// <inheritdoc />
    public async Task<IResult<ProfilingClearResult>> ClearAsync(ProfilingClearRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProfilingClearCoordinator.Clear clear;
        lock (this.sync)
        {
            var prepared = this.clears.Prepare(request, this.writers, this.clock.GetUtcNow(), this.runtime.HasActiveSession);
            if (prepared.IsFailure)
            {
                return Result<ProfilingClearResult>.Failure(prepared);
            }

            clear = prepared.Value;
            if (request.DataSet != ProfilingDataSet.Operations && !this.runtime.ReserveMaintenance(clear.Id))
            {
                clear.State = ProfilingClearState.Failed;
                this.clears.Prune(this.writers, this.clock.GetUtcNow());
                return Result<ProfilingClearResult>.Failure(new ProfilingBusyError("Runtime maintenance is already reserved."));
            }
        }

        try
        {
            while (true)
            {
                Task changed;
                lock (this.sync)
                {
                    var utc = this.clock.GetUtcNow();
                    if (clear.State == ProfilingClearState.Preparing && clear.DeadlineUtc <= utc)
                    {
                        clear.State = ProfilingClearState.Failed;
                    }

                    if (clear.State == ProfilingClearState.Failed)
                    {
                        this.runtime.ReleaseMaintenance(clear.Id);
                        return Result<ProfilingClearResult>.Failure(this.clears.Result(clear)).WithError(new ProfilingBusyError("A writer did not acknowledge before the clear preparation deadline."));
                    }

                    if (this.clears.CanSeal(clear, this.writers, utc))
                    {
                        this.clears.Seal(clear, utc);
                    }

                    if (clear.SealedUtc.HasValue)
                    {
                        this.ApplyClear(clear, this.storageOptions.MaximumMaintenanceRoots, this.storageOptions.MaintenanceTimeBudget, cancellationToken);
                        return Result<ProfilingClearResult>.Success(this.clears.Result(clear));
                    }

                    changed = clear.Changed;
                }

                try
                {
                    await changed.WaitAsync(TimeSpan.FromMilliseconds(50), this.clock, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // The provider clock rechecks preparation and lease expiry without polling application work.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (this.sync)
            {
                if (clear.SealedUtc is null)
                {
                    clear.State = ProfilingClearState.Failed;
                    this.runtime.ReleaseMaintenance(clear.Id);
                }

                return Result<ProfilingClearResult>.Failure(this.clears.Result(clear)).WithError(new ProfilingUnavailableError("Profiling clear was canceled; known progress is retained."));
            }
        }
    }

    /// <inheritdoc />
    public Task<IResult<ProfilingMaintenanceResult>> ResumeMaintenanceAsync(ProfilingMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || request.MaximumRoots <= 0 || request.MaximumRoots > this.storageOptions.MaximumMaintenanceRoots
            || request.Budget <= TimeSpan.Zero || request.Budget > this.storageOptions.MaintenanceTimeBudget || request.MaximumOperationCount <= 0
            || request.MaximumOperationBytes <= 0 || request.MaximumOperationAge <= TimeSpan.Zero || request.MaximumOperationAge == TimeSpan.MaxValue
            || request.MaximumRuntimeSessions <= 0 || request.MaximumRuntimeSessionAge <= TimeSpan.Zero || request.MaximumRuntimeSessionAge == TimeSpan.MaxValue)
        {
            return Failure<ProfilingMaintenanceResult>(new ProfilingValidationError("Profiling maintenance requires finite limits within the provider scheduling bounds."));
        }

        lock (this.sync)
        {
            var started = this.clock.GetTimestamp();
            var utc = this.clock.GetUtcNow();
            var clear = this.clears.Current;
            long operationsRemoved = 0;
            long runtimeRemoved = 0;
            if (clear?.State == ProfilingClearState.Preparing && clear.DeadlineUtc <= utc)
            {
                clear.State = ProfilingClearState.Failed;
                this.runtime.ReleaseMaintenance(clear.Id);
                clear.Pulse();
            }
            else if (clear?.State == ProfilingClearState.Applying)
            {
                var operationsBefore = clear.RemovedOperations;
                var runtimeBefore = clear.RemovedRuntimeSessions;
                this.ApplyClear(clear, request.MaximumRoots, request.Budget, cancellationToken);
                operationsRemoved = clear.RemovedOperations - operationsBefore;
                runtimeRemoved = clear.RemovedRuntimeSessions - runtimeBefore;
            }

            long retentionRemoved = 0;
            var remaining = request.MaximumRoots - operationsRemoved - runtimeRemoved;
            if (remaining > 0 && this.clock.GetElapsedTime(started) < request.Budget)
            {
                runtimeRemoved += this.runtime.DeleteForRetention(request, (int)remaining, utc, this.clock, started, cancellationToken);
                remaining = request.MaximumRoots - operationsRemoved - runtimeRemoved;
            }

            if (remaining > 0 && this.clock.GetElapsedTime(started) < request.Budget)
            {
                retentionRemoved = this.Retain(request, (int)remaining, started, cancellationToken);
                operationsRemoved += retentionRemoved;
            }

            this.clears.Prune(this.writers, utc);
            this.writers.Prune(utc);
            var capacityEvictions = this.capacityEvictionsToReport;
            this.capacityEvictionsToReport = 0;
            return Success(new ProfilingMaintenanceResult
            {
                RetentionRemovedOperations = retentionRemoved, CapacityEvictedOperations = capacityEvictions,
                RemovedOperations = operationsRemoved, RemovedRuntimeSessions = runtimeRemoved, DeletionRevision = this.deletionRevision,
                RemainingClears = this.clears.Clears.LongCount(c => c.State is ProfilingClearState.Preparing or ProfilingClearState.Applying),
            });
        }
    }

    private void ApplyClear(ProfilingClearCoordinator.Clear clear, int maximum, TimeSpan budget, CancellationToken token)
    {
        var started = this.clock.GetTimestamp();
        var removed = 0;
        foreach (var record in this.records.Values.Where(r => clear.Matches(r.Envelope)).Take(maximum).ToArray())
        {
            token.ThrowIfCancellationRequested();
            if (this.clock.GetElapsedTime(started) >= budget)
            {
                break;
            }

            this.Remove(record);
            clear.RemovedOperations++;
            clear.RemovedSummaries += record.Envelope.Record.Segments.Count;
            removed++;
        }

        var operationsRemain = this.records.Values.Any(r => clear.Matches(r.Envelope));
        var runtimeRemain = clear.Selection.DataSet != ProfilingDataSet.Operations;
        if (runtimeRemain && removed < maximum && this.clock.GetElapsedTime(started) < budget)
        {
            token.ThrowIfCancellationRequested();
            var result = this.runtime.DeleteForClear(clear.Id, clear.Selection, maximum - removed);
            clear.RemovedRuntimeSessions += result.Sessions;
            clear.RemovedSnapshots += result.Snapshots;
            runtimeRemain = result.Remaining;
        }

        if (!operationsRemain && !runtimeRemain)
        {
            clear.State = ProfilingClearState.Completed;
            clear.CompletedUtc = this.clock.GetUtcNow();
            this.runtime.ReleaseMaintenance(clear.Id);
            clear.Pulse();
        }
    }

    private bool MakeRoom(long incoming, DateTimeOffset utc)
    {
        if (incoming > this.options.MaximumRetainedBytes)
        {
            return false;
        }

        if (this.records.Count < this.options.MaximumRetainedOperations && this.retainedBytes <= this.options.MaximumRetainedBytes - incoming)
        {
            return true;
        }

        var started = this.clock.GetTimestamp();
        var candidates = this.RetentionCandidates(utc, this.storageOptions.MaximumMaintenanceRoots, started, this.storageOptions.MaintenanceTimeBudget);
        foreach (var candidate in candidates)
        {
            if (this.records.Count < this.options.MaximumRetainedOperations && this.retainedBytes <= this.options.MaximumRetainedBytes - incoming)
            {
                return true;
            }

            this.Remove(candidate);
            this.capacityEvictionsToReport++;
        }

        return this.records.Count < this.options.MaximumRetainedOperations && this.retainedBytes <= this.options.MaximumRetainedBytes - incoming;
    }

    private long Retain(ProfilingMaintenanceRequest request, int maximum, long started, CancellationToken token)
    {
        var utc = this.clock.GetUtcNow();
        var threshold = utc.Subtract(request.MaximumOperationAge);
        long removed = 0;
        foreach (var candidate in this.RetentionCandidates(utc, maximum, started, request.Budget))
        {
            if (removed >= maximum || this.clock.GetElapsedTime(started) >= request.Budget)
            {
                break;
            }

            token.ThrowIfCancellationRequested();
            if ((candidate.Envelope.Record.CompletedUtc < threshold || this.records.Count > request.MaximumOperationCount
                    || request.MaximumOperationBytes.HasValue && this.retainedBytes > request.MaximumOperationBytes.Value)
                && !this.writers.ReplayProtected(candidate.Envelope, utc))
            {
                this.Remove(candidate);
                removed++;
            }
        }

        return removed;
    }

    private IReadOnlyList<StoredOperation> RetentionCandidates(DateTimeOffset utc, int maximum, long started, TimeSpan budget)
    {
        var candidates = new List<StoredOperation>(Math.Min(maximum, this.records.Count));
        foreach (var record in this.completionOrder)
        {
            if (candidates.Count >= maximum || this.clock.GetElapsedTime(started) >= budget)
            {
                break;
            }

            if (!this.writers.ReplayProtected(record.Envelope, utc))
            {
                candidates.Add(record);
            }
        }

        return candidates;
    }

    private void Remove(StoredOperation record)
    {
        this.records.Remove(record.Envelope.Record.Id);
        this.completionOrder.Remove(record);
        this.sequences.Remove((record.Envelope.Lease.WriterId, record.Envelope.CompletionSequence));
        this.retainedBytes -= record.Envelope.Record.EstimatedPayloadBytes;
        this.deletionRevision = checked(this.deletionRevision + 1);
        var nodeId = record.Envelope.Record.NodeId;
        var owners = this.nodeOwners[nodeId] - 1;
        if (owners == 0)
        {
            this.nodeOwners.Remove(nodeId);
        }
        else
        {
            this.nodeOwners[nodeId] = owners;
        }

        this.runtime.ReleaseSharedNode(nodeId, owners > 0 || this.writers.ReferencesNode(nodeId, this.clock.GetUtcNow()));
    }

    private static Task<IResult<T>> Completed<T>(Result<T> result) => Task.FromResult<IResult<T>>(result);
    private static Task<IResult<T>> Success<T>(T value) => Completed(Result<T>.Success(value));
    private static Task<IResult<T>> Failure<T>(IResultError error) => Completed(Result<T>.Failure(error));
    private sealed record StoredOperation(ProfilingWriteEnvelope Envelope, long CommitWatermark)
    {
        public string CanonicalId { get; } = Envelope.Record.Id.ToString("N");
    }
}
