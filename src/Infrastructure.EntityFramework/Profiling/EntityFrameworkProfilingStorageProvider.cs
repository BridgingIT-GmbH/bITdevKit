// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Stores independent Runtime and Operation histories in a shared relational provider scope.</summary>
/// <example><code>var provider = new EntityFrameworkProfilingStorageProvider&lt;ApplicationDbContext&gt;(scopeFactory, options);</code></example>
public sealed partial class EntityFrameworkProfilingStorageProvider<TContext>
    where TContext : DbContext, IProfilingDbContext
{
    private readonly OperationProfilingOptions options;
    private readonly ProfilingStorageOptions storageOptions;
    private readonly ProfilingQueryOptions queryOptions;
    private readonly TimeProvider clock;
    private readonly EntityFrameworkRuntimeProfilingStore<TContext> runtime;
    private readonly EntityFrameworkProfilingUnitOfWork<TContext> unitOfWork;

    /// <summary>Creates a singleton provider that resolves a fresh context for every database attempt.</summary>
    /// <example><code>var provider = new EntityFrameworkProfilingStorageProvider&lt;ApplicationDbContext&gt;(scopes, options, TimeProvider.System);</code></example>
    public EntityFrameworkProfilingStorageProvider(IServiceScopeFactory scopes, ProfilingOptions options = null, TimeProvider clock = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        options ??= new ProfilingOptions();
        options.Validate();
        this.options = options.Operations.SnapshotWithRetentionDefaults(100000, null, TimeSpan.FromDays(7));
        this.options.Enabled = true;
        this.options.Validate();
        this.storageOptions = options.Storage.Snapshot();
        this.queryOptions = new()
        {
            PageSize = options.Queries.PageSize, MaximumPageSize = options.Queries.MaximumPageSize, MaximumConcurrentQueries = options.Queries.MaximumConcurrentQueries,
            MaximumAnalysisRecords = options.Queries.MaximumAnalysisRecords, MaximumDimensionPredicates = options.Queries.MaximumDimensionPredicates,
            MaximumGroupingDimensions = options.Queries.MaximumGroupingDimensions, MaximumOverlaySnapshots = options.Queries.MaximumOverlaySnapshots,
            Timeout = options.Queries.Timeout, BoundaryLifetime = options.Queries.BoundaryLifetime,
        };
        this.clock = clock ?? TimeProvider.System;
        // Reading connection metadata does not open it, create tables, or migrate the host database.
        using var scope = scopes.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var connection = context.Database.GetDbConnection();
        var scopeId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{context.Database.ProviderName}\n{connection.DataSource}\n{connection.Database}")));
        this.Capabilities = new() { Name = "EntityFramework", Scope = "database:" + scopeId, Shared = true, OperationRetention = new() { MaximumCount = this.options.MaximumRetainedOperations, MaximumBytes = this.options.MaximumRetainedBytes == long.MaxValue ? null : this.options.MaximumRetainedBytes, MaximumAge = this.options.MaximumOperationAge } };
        this.runtime = new(scopes);
        this.runtime.SharedClear = token => this.ClearAsync(new() { DataSet = ProfilingDataSet.Runtime }, token);
        this.unitOfWork = new(scopes, new(this.storageOptions, this.Capabilities.Scope));
    }

    /// <summary>Gets the database provider scope without exposing connection credentials.</summary>
    /// <example><code>var scope = provider.Capabilities.Scope;</code></example>
    public ProfilingProviderCapabilities Capabilities { get; }

    /// <summary>Gets the independent Runtime lifecycle facet.</summary>
    /// <example><code>var sessions = await provider.Runtime.ListSessionsAsync();</code></example>
    public IRuntimeProfilingStore Runtime => this.runtime;

    /// <summary>Opens or returns the original bounded writer lease under one durable transaction.</summary>
    /// <example><code>var lease = await provider.OpenWriterAsync(new() { AttemptId = Guid.NewGuid(), Node = node });</code></example>
    public Task<IResult<ProfilingWriterLease>> OpenWriterAsync(ProfilingOpenWriterRequest request, CancellationToken cancellationToken = default) =>
        this.unitOfWork.WriteAsync(async (context, frame, _, utc, token) =>
        {
            var opened = frame.Writers.Open(request, utc, this.options.WriterLeaseDuration);
            if (opened.IsFailure)
            {
                return opened;
            }

            var node = await UpsertNodeAsync(context, request.Node, token).ConfigureAwait(false);
            return node.IsSuccess ? opened : Result<ProfilingWriterLease>.Failure(node);
        }, cancellationToken);

    /// <summary>Renews an original lease and atomically settles non-replayable sequence numbers.</summary>
    /// <example><code>var state = await provider.SynchronizeWriterAsync(new() { Lease = lease, SettledThrough = 42 });</code></example>
    public Task<IResult<ProfilingWriterSynchronizationResult>> SynchronizeWriterAsync(ProfilingWriterSynchronizationRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Lease is null || request.SettledThrough < 0 || request.LeaseDuration <= TimeSpan.Zero || request.LeaseDuration > this.options.WriterLeaseDuration)
        {
            return Failure<ProfilingWriterSynchronizationResult>(new ProfilingValidationError("Synchronization requires an original lease, nonnegative settlement and bounded renewal."));
        }

        return this.unitOfWork.WriteAsync((_, frame, _, utc, _) =>
        {
            var result = frame.Writers.Synchronize(request, utc, frame.Clears.Pending(request.Lease, utc));
            frame.Clears.Prune(frame.Writers, utc);
            return Task.FromResult(Result<ProfilingWriterSynchronizationResult>.Success(result));
        }, cancellationToken);
    }

    /// <summary>Persists the first acknowledgement and returns its unchanged cutoff on retries.</summary>
    /// <example><code>var cutoff = await provider.AcknowledgeClearAsync(new() { ClearId = id, Lease = lease, CompletionCutoff = 42 });</code></example>
    public Task<IResult<ProfilingClearAcknowledgement>> AcknowledgeClearAsync(ProfilingClearAcknowledgement request, CancellationToken cancellationToken = default) =>
        this.unitOfWork.WriteAsync((_, frame, _, utc, _) => Task.FromResult(frame.Clears.Acknowledge(request, frame.Writers, utc)), cancellationToken);

    /// <summary>Retires an original lease; expired or already retired identities remain unusable.</summary>
    /// <example><code>await provider.CloseWriterAsync(lease);</code></example>
    public async Task<IResult> CloseWriterAsync(ProfilingWriterLease lease, CancellationToken cancellationToken = default) =>
        await this.unitOfWork.WriteAsync((_, frame, _, utc, _) =>
        {
            frame.Writers.Close(lease, utc);
            frame.Writers.Prune(utc);
            return Task.FromResult(Result<bool>.Success(true));
        }, cancellationToken).ConfigureAwait(false);

    /// <summary>Commits bounded immutable root graphs with individual admission outcomes.</summary>
    /// <example><code>var result = await provider.AppendAsync(envelopes, cancellationToken);</code></example>
    public Task<IResult<ProfilingBatchWriteResult>> AppendAsync(IReadOnlyList<ProfilingWriteEnvelope> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (records is null || records.Count > this.options.BatchSize)
        {
            return Failure<ProfilingBatchWriteResult>(new ProfilingValidationError("The profiling batch exceeds its record bound."));
        }

        // Freeze outside the database transaction; invalid members do not prevent valid roots.
        var inputs = new BatchInput[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OperationProfilingRecord frozen = null;
            try { frozen = ProfilingRecordSnapshot.Copy(records[index]?.Record, this.options); }
            catch (Exception) { /* The original identity still participates in idempotency checks. */ }

            inputs[index] = new(records[index], frozen);
        }

        return this.unitOfWork.WriteAsync(async (context, frame, _, utc, token) =>
        {
            var ids = inputs.Where(input => input.Envelope?.Record is not null).Select(input => input.Envelope.Record.Id).Distinct().ToArray();
            var writerIds = inputs.Where(input => input.Envelope?.Lease is not null).Select(input => input.Envelope.Lease.WriterId).Distinct().ToArray();
            var sequenceIds = inputs.Where(input => input.Envelope is not null).Select(input => input.Envelope.CompletionSequence).Distinct().ToArray();
            var stored = await context.Set<OperationProfilingEntity>().AsNoTracking()
                .Where(root => ids.Contains(root.Id) || writerIds.Contains(root.WriterId) && sequenceIds.Contains(root.CompletionSequence))
                .Select(root => new StoredIdentity(root.Id, root.WriterId, root.CompletionSequence, root.CommitWatermark))
                .ToListAsync(token).ConfigureAwait(false);
            var identities = stored.ToDictionary(root => root.Id);
            var sequences = stored.ToDictionary(root => (root.WriterId, root.Sequence));
            var nodes = new Dictionary<Guid, ProfilingNodeEntity>();
            var nodeIds = inputs.Where(input => input.Frozen is not null).Select(input => input.Frozen.NodeId).Distinct().ToArray();
            var nodeKeys = inputs.Where(input => input.Frozen is not null).Select(input => input.Frozen.NodeKey).Distinct().ToArray();
            foreach (var node in await context.Set<ProfilingNodeEntity>().Where(node => nodeIds.Contains(node.Id) || nodeKeys.Contains(node.Key)).ToListAsync(token).ConfigureAwait(false))
            {
                nodes.Add(node.Id, node);
            }

            var results = new ProfilingRecordWriteResult[inputs.Length];
            long batchBytes = 0;
            var budget = Stopwatch.StartNew();
            var evictionRemaining = this.storageOptions.MaximumMaintenanceRoots;
            for (var index = 0; index < inputs.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var input = inputs[index];
                var envelope = input.Envelope;
                var disposition = new ProfilingRecordWriteResult { OperationId = envelope?.Record?.Id ?? Guid.Empty, CompletionSequence = envelope?.CompletionSequence ?? 0 };
                if (envelope?.Record is null || envelope.CompletionSequence <= 0 || envelope.Record.NodeId != envelope.Lease?.NodeId)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "InvalidEnvelope" };
                    continue;
                }

                var writer = frame.Writers.Find(envelope.Lease, utc);
                if (writer is null)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.StaleWriter };
                    continue;
                }

                if (frame.Clears.Fenced(envelope))
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.Cleared };
                    continue;
                }

                if (identities.TryGetValue(envelope.Record.Id, out var original))
                {
                    results[index] = original.WriterId == envelope.Lease.WriterId && original.Sequence == envelope.CompletionSequence
                        ? disposition with { Outcome = ProfilingWriteOutcome.AlreadyStored, CommitWatermark = original.Publication }
                        : disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "OperationIdentityConflict" };
                    continue;
                }

                if (envelope.CompletionSequence <= writer.Lease.SettledThrough)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.AlreadySettled };
                    continue;
                }

                if (sequences.ContainsKey((envelope.Lease.WriterId, envelope.CompletionSequence)))
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "SequenceIdentityConflict" };
                    continue;
                }

                var record = input.Frozen;
                if (record is null)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "InvalidRecord" };
                    continue;
                }

                if (record.EstimatedPayloadBytes > this.options.BatchBytes - batchBytes)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.CapacityRejected, SafeCode = "BatchPayload" };
                    continue;
                }

                batchBytes += record.EstimatedPayloadBytes;
                if (record.CompletedUtc < utc.Subtract(this.options.MaximumOperationAge))
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.RetentionRejected, SafeCode = "RetentionAge" };
                    continue;
                }

                var nodeResult = ResolveNode(record.Node, nodes.Values);
                if (nodeResult.IsFailure)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.PermanentFailure, SafeCode = "NodeIdentityConflict" };
                    continue;
                }

                if (record.EstimatedPayloadBytes > this.options.MaximumRetainedBytes)
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.CapacityRejected, SafeCode = "RetentionCapacity" };
                    continue;
                }

                if (NeedsRoom(frame.State, this.options.MaximumRetainedOperations - 1, this.options.MaximumRetainedBytes - record.EstimatedPayloadBytes))
                {
                    var removed = await this.RetainAsync(context, frame, utc, this.options.MaximumRetainedOperations - 1,
                        this.options.MaximumRetainedBytes - record.EstimatedPayloadBytes, null, evictionRemaining, budget, this.storageOptions.MaintenanceTimeBudget, token,
                        deleted =>
                        {
                            foreach (var id in deleted)
                            {
                                if (identities.Remove(id, out var identity))
                                {
                                    sequences.Remove((identity.WriterId, identity.Sequence));
                                }
                            }
                        }).ConfigureAwait(false);
                    evictionRemaining -= removed;
                }

                if (NeedsRoom(frame.State, this.options.MaximumRetainedOperations - 1, this.options.MaximumRetainedBytes - record.EstimatedPayloadBytes))
                {
                    results[index] = disposition with { Outcome = ProfilingWriteOutcome.CapacityRejected, SafeCode = "RetentionCapacity" };
                    continue;
                }

                var nodeEntity = nodeResult.Value;
                if (!nodes.ContainsKey(nodeEntity.Id))
                {
                    context.Set<ProfilingNodeEntity>().Add(nodeEntity);
                    nodes.Add(nodeEntity.Id, nodeEntity);
                }

                record = record with { Node = ProfilingEntityMapper.ToModel(nodeEntity) };
                var publication = checked(++frame.State.PublicationWatermark);
                var entity = ProfilingEntityMapper.ToOperationEntity(envelope with { Record = record }, publication);
                context.Set<OperationProfilingEntity>().Add(entity);
                frame.State.RetainedOperationCount++;
                frame.State.RetainedOperationBytes = checked(frame.State.RetainedOperationBytes + record.EstimatedPayloadBytes);
                var accepted = new StoredIdentity(record.Id, envelope.Lease.WriterId, envelope.CompletionSequence, publication);
                identities.Add(record.Id, accepted);
                sequences.Add((accepted.WriterId, accepted.Sequence), accepted);
                results[index] = disposition with { Outcome = ProfilingWriteOutcome.Accepted, CommitWatermark = publication };
            }

            return Result<ProfilingBatchWriteResult>.Success(new() { Records = Array.AsReadOnly(results) });
        }, cancellationToken);
    }

    /// <summary>Finds an occurrence independently of dashboard list filters and default outcomes.</summary>
    /// <example><code>var record = await provider.FindAsync(operationId);</code></example>
    public Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        this.unitOfWork.ReadAsync(async (context, token) =>
        {
            this.ConfigureQueryTimeout(context);
            var root = await context.Set<OperationProfilingEntity>().AsNoTracking().SingleOrDefaultAsync(root => root.Id == id, token).ConfigureAwait(false);
            return Result<OperationProfilingRecord>.Success(root is null ? null : ProfilingEntityMapper.ToOperationModel(root, this.options));
        }, cancellationToken);

    private static Task<IResult<T>> Failure<T>(IResultError error) => Task.FromResult<IResult<T>>(Result<T>.Failure(error));
    private void ConfigureQueryTimeout(TContext context) =>
        context.Database.SetCommandTimeout((int)Math.Clamp(Math.Ceiling(this.queryOptions.Timeout.TotalSeconds), 1, int.MaxValue));
    private static bool NeedsRoom(ProfilingStoreStateEntity state, long maximumCount, long? maximumBytes) =>
        state.RetainedOperationCount > maximumCount || maximumBytes.HasValue && state.RetainedOperationBytes > maximumBytes.Value;

    private static Result<ProfilingNodeEntity> ResolveNode(ProfilingNode node, IEnumerable<ProfilingNodeEntity> candidates)
    {
        if (node is null || node.Identity.Id == Guid.Empty || node.Identity.Key?.Length != 8
            || !node.Identity.Key.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9') || node.ProcessId <= 0
            || node.ProcessStartedUtc.Offset != TimeSpan.Zero || node.HostName?.Length > 128 || node.DisplayName?.Length > 128 || node.ApplicationVersion?.Length > 128)
        {
            return Result<ProfilingNodeEntity>.Failure(new ProfilingValidationError("A bounded cached process descriptor is required."));
        }

        var existing = candidates.FirstOrDefault(candidate => candidate.Id == node.Identity.Id || candidate.Key == node.Identity.Key);
        if (existing is not null && (existing.Id != node.Identity.Id || existing.Key != node.Identity.Key || existing.ProcessId != node.ProcessId
            || existing.ExecutionStartedUtcTicks != node.ProcessStartedUtc.UtcTicks || existing.HostName != node.HostName || existing.DisplayName != node.DisplayName || existing.ApplicationVersion != node.ApplicationVersion))
        {
            return Result<ProfilingNodeEntity>.Failure(new ProfilingValidationError("The profiling process identity conflicts with its stored descriptor."));
        }

        return Result<ProfilingNodeEntity>.Success(existing ?? ProfilingEntityMapper.ToEntity(null, node));
    }

    private static async Task<Result<ProfilingNodeEntity>> UpsertNodeAsync(TContext context, ProfilingNode node, CancellationToken token)
    {
        var candidates = await context.Set<ProfilingNodeEntity>().Where(candidate => candidate.Id == node.Identity.Id || candidate.Key == node.Identity.Key).ToListAsync(token).ConfigureAwait(false);
        var result = ResolveNode(node, candidates);
        if (result.IsSuccess && candidates.Count == 0)
        {
            context.Set<ProfilingNodeEntity>().Add(result.Value);
        }

        return result;
    }

    private sealed record BatchInput(ProfilingWriteEnvelope Envelope, OperationProfilingRecord Frozen);
    private sealed record StoredIdentity(Guid Id, Guid WriterId, long Sequence, long Publication);
}
