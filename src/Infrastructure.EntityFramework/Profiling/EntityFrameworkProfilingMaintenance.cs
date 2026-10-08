// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Diagnostics;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

public sealed partial class EntityFrameworkProfilingStorageProvider<TContext>
{
    /// <summary>Prepares fixed writer cutoffs, seals a durable fence, and removes bounded root batches.</summary>
    /// <example><code>var progress = await provider.ClearAsync(new() { DataSet = ProfilingDataSet.Operations });</code></example>
    public async Task<IResult<ProfilingClearResult>> ClearAsync(ProfilingClearRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var error = ProfilingClearCoordinator.Validate(request);
        if (error is not null)
        {
            return Result<ProfilingClearResult>.Failure(error);
        }

        var includesRuntime = request.DataSet != ProfilingDataSet.Operations;
        var prepared = await this.unitOfWork.WriteAsync(async (context, frame, gate, utc, token) =>
        {
            var active = includesRuntime && await context.Set<RuntimeProfilingSessionEntity>().AnyAsync(session => session.State == RuntimeProfilingSessionState.Running, token).ConfigureAwait(false);
            var result = frame.Clears.Prepare(request, frame.Writers, utc, active);
            if (result.IsFailure)
            {
                return Result<ProfilingClearResult>.Failure(result);
            }

            if (gate?.MaintenanceClearId is not null)
            {
                return Result<ProfilingClearResult>.Failure(new ProfilingBusyError("Runtime maintenance is already reserved."));
            }

            if (gate is not null)
            {
                gate.MaintenanceClearId = result.Value.Id;
            }

            return Result<ProfilingClearResult>.Success(frame.Clears.Result(result.Value));
        }, cancellationToken, includesRuntime).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared;
        }

        var id = prepared.Value.ClearId;
        try
        {
            while (true)
            {
                var progress = await this.AdvanceClearAsync(id, cancellationToken, includesRuntime).ConfigureAwait(false);
                if (progress.IsFailure || progress.Value.State != ProfilingClearState.Preparing)
                {
                    return progress;
                }

                // The state is shared across processes. Polling is bounded and never holds a database lock.
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var canceled = await this.unitOfWork.WriteAsync((_, frame, gate, _, _) =>
            {
                var clear = frame.Clears.Clears.SingleOrDefault(clear => clear.Id == id);
                if (clear is null)
                {
                    return Task.FromResult(Result<ProfilingClearResult>.Success(prepared.Value));
                }

                if (clear.SealedUtc is null)
                {
                    clear.State = ProfilingClearState.Failed;
                    ReleaseGate(gate, clear.Id);
                }

                return Task.FromResult(Result<ProfilingClearResult>.Success(frame.Clears.Result(clear)));
            }, cleanup.Token, includesRuntime).ConfigureAwait(false);
            return Result<ProfilingClearResult>.Failure(canceled.IsSuccess ? canceled.Value : prepared.Value)
                .WithError(new ProfilingUnavailableError("Profiling clear was canceled; durable progress remains recoverable."));
        }
    }

    private async Task<IResult<ProfilingClearResult>> AdvanceClearAsync(Guid id, CancellationToken cancellationToken, bool includesRuntime)
    {
        var advanced = await this.unitOfWork.WriteAsync(async (context, frame, gate, utc, token) =>
        {
            var clear = frame.Clears.Clears.SingleOrDefault(clear => clear.Id == id);
            if (clear is null)
            {
                return Result<ProfilingClearResult>.Failure(new ProfilingUnavailableError("The clear preparation no longer exists."));
            }

            if (clear.State == ProfilingClearState.Preparing && clear.DeadlineUtc <= utc)
            {
                clear.State = ProfilingClearState.Failed;
                ReleaseGate(gate, clear.Id);
            }
            else if (frame.Clears.CanSeal(clear, frame.Writers, utc))
            {
                frame.Clears.Seal(clear, utc);
            }

            if (clear.State == ProfilingClearState.Applying)
            {
                await this.ApplyClearAsync(context, frame, gate, clear, utc, this.storageOptions.MaximumMaintenanceRoots,
                    Stopwatch.StartNew(), this.storageOptions.MaintenanceTimeBudget, token).ConfigureAwait(false);
            }

            // Failed preparation is committed before returning Busy; it must release the durable gate.
            return Result<ProfilingClearResult>.Success(frame.Clears.Result(clear));
        }, cancellationToken, includesRuntime).ConfigureAwait(false);
        return advanced.IsSuccess && advanced.Value.State == ProfilingClearState.Failed
            ? Result<ProfilingClearResult>.Failure(advanced.Value).WithError(new ProfilingBusyError("A writer did not acknowledge before the preparation deadline."))
            : advanced;
    }

    /// <summary>Resumes durable clears and performs bounded retention without a capture dependency.</summary>
    /// <example><code>await provider.ResumeMaintenanceAsync(new ProfilingMaintenanceRequest());</code></example>
    public Task<IResult<ProfilingMaintenanceResult>> ResumeMaintenanceAsync(ProfilingMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || request.MaximumRoots <= 0 || request.MaximumRoots > this.storageOptions.MaximumMaintenanceRoots
            || request.Budget <= TimeSpan.Zero || request.Budget > this.storageOptions.MaintenanceTimeBudget || request.MaximumOperationCount <= 0
            || request.MaximumOperationBytes <= 0 || request.MaximumOperationAge <= TimeSpan.Zero || request.MaximumOperationAge == TimeSpan.MaxValue)
        {
            return Failure<ProfilingMaintenanceResult>(new ProfilingValidationError("Profiling maintenance requires finite limits within provider scheduling bounds."));
        }

        return this.unitOfWork.WriteAsync(async (context, frame, gate, utc, token) =>
        {
            var budget = Stopwatch.StartNew();
            var clear = frame.Clears.Current;
            long removedOperations = 0;
            long removedRuntime = 0;
            if (clear?.State == ProfilingClearState.Preparing && clear.DeadlineUtc <= utc)
            {
                clear.State = ProfilingClearState.Failed;
                ReleaseGate(gate, clear.Id);
            }
            else if (clear?.State == ProfilingClearState.Applying)
            {
                var beforeOperations = clear.RemovedOperations;
                var beforeRuntime = clear.RemovedRuntimeSessions;
                await this.ApplyClearAsync(context, frame, gate, clear, utc, request.MaximumRoots, budget, request.Budget, token).ConfigureAwait(false);
                removedOperations = clear.RemovedOperations - beforeOperations;
                removedRuntime = clear.RemovedRuntimeSessions - beforeRuntime;
            }

            long retentionRemoved = 0;
            var remaining = request.MaximumRoots - removedOperations - removedRuntime;
            if (remaining > 0 && budget.Elapsed < request.Budget)
            {
                retentionRemoved = await this.RetainAsync(context, frame, utc, request.MaximumOperationCount, request.MaximumOperationBytes,
                    utc.Subtract(request.MaximumOperationAge).UtcTicks, (int)remaining, budget, request.Budget, token).ConfigureAwait(false);
                removedOperations += retentionRemoved;
            }

            frame.Clears.Prune(frame.Writers, utc);
            frame.Writers.Prune(utc);
            await PruneNodesAsync(context, request.MaximumRoots, token).ConfigureAwait(false);
            return Result<ProfilingMaintenanceResult>.Success(new()
            {
                RetentionRemovedOperations = retentionRemoved, RemovedOperations = removedOperations, RemovedRuntimeSessions = removedRuntime,
                DeletionRevision = checked(frame.State.DeletionRevision + gate.DeletionRevision),
                RemainingClears = frame.Clears.Clears.LongCount(clear => clear.State is ProfilingClearState.Preparing or ProfilingClearState.Applying),
            });
        }, cancellationToken, runtimeGate: true);
    }

    private async Task ApplyClearAsync(TContext context, EntityFrameworkProfilingCoordination<TContext>.Frame frame, ProfilingRuntimeGateEntity gate,
        ProfilingClearCoordinator.Clear clear, DateTimeOffset utc, int maximum, Stopwatch elapsed, TimeSpan budget, CancellationToken token)
    {
        var query = ClearRoots(context, clear);
        var removed = 0;
        if (clear.Selection.DataSet != ProfilingDataSet.Runtime && elapsed.Elapsed < budget)
        {
            var candidates = await query.OrderBy(root => root.CompletedUtcTicks).ThenBy(root => root.CanonicalIdBytes)
                .Select(root => new DeletionCandidate(root.Id, root.EstimatedPayloadBytes, root.Segments.Count))
                .Take(maximum).ToArrayAsync(token).ConfigureAwait(false);
            if (candidates.Length > 0)
            {
                await RemoveOperationsAsync(context, frame.State, candidates, token).ConfigureAwait(false);
                clear.RemovedOperations += candidates.Length;
                clear.RemovedSummaries += candidates.Sum(root => (long)root.SummaryCount);
                removed = candidates.Length;
            }
        }

        var operationsRemain = clear.Selection.DataSet != ProfilingDataSet.Runtime && await query.AnyAsync(token).ConfigureAwait(false);
        var runtimeRemain = false;
        if (clear.Selection.DataSet != ProfilingDataSet.Operations)
        {
            var sessions = context.Set<RuntimeProfilingSessionEntity>().Where(session => session.State != RuntimeProfilingSessionState.Running);
            if (clear.Selection.FromUtc.HasValue)
            {
                var lower = clear.Selection.FromUtc.Value.UtcTicks;
                var upper = clear.Selection.ToUtc.Value.UtcTicks;
                sessions = sessions.Where(session => session.CompletionUtcTicks >= lower && session.CompletionUtcTicks < upper);
            }

            if (removed < maximum && elapsed.Elapsed < budget)
            {
                var selected = await sessions.OrderBy(session => session.CompletionUtcTicks).Take(maximum - removed).ToArrayAsync(token).ConfigureAwait(false);
                if (selected.Length > 0)
                {
                    var ids = selected.Select(session => session.Id).ToArray();
                    var snapshots = await context.Set<RuntimeProfilingSnapshotEntity>().LongCountAsync(snapshot => ids.Contains(snapshot.SessionId), token).ConfigureAwait(false);
                    var known = await context.Set<RuntimeProfilingInvalidSessionEntity>().Where(session => ids.Contains(session.Id)).Select(session => session.Id).ToArrayAsync(token).ConfigureAwait(false);
                    context.Set<RuntimeProfilingInvalidSessionEntity>().AddRange(selected.Where(session => !known.Contains(session.Id)).Select(session => new RuntimeProfilingInvalidSessionEntity { Id = session.Id, Key = session.Key }));
                    context.Set<RuntimeProfilingSessionEntity>().RemoveRange(selected);
                    await context.SaveChangesAsync(token).ConfigureAwait(false);
                    clear.RemovedRuntimeSessions += selected.Length;
                    clear.RemovedSnapshots += snapshots;
                    gate.DeletionRevision = checked(gate.DeletionRevision + selected.Length);
                }
            }

            runtimeRemain = await sessions.AnyAsync(token).ConfigureAwait(false);
        }

        if (!operationsRemain && !runtimeRemain)
        {
            clear.State = ProfilingClearState.Completed;
            clear.CompletedUtc = utc;
            ReleaseGate(gate, clear.Id);
        }
    }

    private static IQueryable<OperationProfilingEntity> ClearRoots(TContext context, ProfilingClearCoordinator.Clear clear)
    {
        var query = context.Set<OperationProfilingEntity>().Where(root => root.WriterGeneration <= clear.Generation);
        if (clear.Selection.FromUtc.HasValue)
        {
            var lower = clear.Selection.FromUtc.Value.UtcTicks;
            var upper = clear.Selection.ToUtc.Value.UtcTicks;
            query = query.Where(root => root.CompletedUtcTicks >= lower && root.CompletedUtcTicks < upper);
        }

        foreach (var (writer, cutoff) in clear.Cutoffs)
        {
            var id = writer;
            var sequence = cutoff;
            query = query.Where(root => root.WriterId != id || root.CompletionSequence <= sequence);
        }

        return query;
    }

    private async Task<int> RetainAsync(TContext context, EntityFrameworkProfilingCoordination<TContext>.Frame frame, DateTimeOffset utc,
        long maximumCount, long? maximumBytes, long? ageThreshold, int maximum, Stopwatch elapsed, TimeSpan budget, CancellationToken token,
        Action<IReadOnlyList<Guid>> deleted = null)
    {
        if (maximum <= 0 || elapsed.Elapsed >= budget)
        {
            return 0;
        }

        var query = context.Set<OperationProfilingEntity>().AsNoTracking();
        foreach (var writer in frame.Writers.Snapshot().Where(writer => !writer.Retired && writer.Lease.ExpiresUtc > utc))
        {
            var id = writer.Lease.WriterId;
            var settled = writer.Lease.SettledThrough;
            query = query.Where(root => root.WriterId != id || root.CompletionSequence <= settled);
        }

        var candidates = await query.OrderBy(root => root.CompletedUtcTicks).ThenBy(root => root.CanonicalIdBytes)
            .Select(root => new DeletionCandidate(root.Id, root.EstimatedPayloadBytes, root.Segments.Count) { CompletedUtcTicks = root.CompletedUtcTicks })
            .Take(maximum).ToArrayAsync(token).ConfigureAwait(false);
        var selected = new List<DeletionCandidate>(candidates.Length);
        var count = frame.State.RetainedOperationCount;
        var bytes = frame.State.RetainedOperationBytes;
        foreach (var candidate in candidates)
        {
            if (count > maximumCount || maximumBytes.HasValue && bytes > maximumBytes.Value || ageThreshold.HasValue && candidate.CompletedUtcTicks < ageThreshold.Value)
            {
                selected.Add(candidate);
                count--;
                bytes -= candidate.Bytes;
            }
        }

        if (selected.Count > 0)
        {
            await RemoveOperationsAsync(context, frame.State, selected, token).ConfigureAwait(false);
            deleted?.Invoke(selected.Select(root => root.Id).ToArray());
        }

        return selected.Count;
    }

    private static async Task RemoveOperationsAsync(TContext context, ProfilingStoreStateEntity state, IReadOnlyList<DeletionCandidate> selected, CancellationToken token)
    {
        var ids = selected.Select(root => root.Id).ToArray();
        // Root-owned foreign keys delete the entire graph in the same transaction.
        var count = await context.Set<OperationProfilingEntity>().Where(root => ids.Contains(root.Id)).ExecuteDeleteAsync(token).ConfigureAwait(false);
        if (count != selected.Count)
        {
            throw new ArgumentException("The serialized profiling deletion count changed unexpectedly.");
        }

        state.RetainedOperationCount -= count;
        state.RetainedOperationBytes -= selected.Sum(root => root.Bytes);
        state.DeletionRevision = checked(state.DeletionRevision + count);
    }

    private static async Task PruneNodesAsync(TContext context, int maximum, CancellationToken token)
    {
        var unused = await context.Set<ProfilingNodeEntity>().Where(node => !context.Set<OperationProfilingEntity>().Any(root => root.NodeId == node.Id)
            && !context.Set<ProfilingWriterEntity>().Any(writer => writer.NodeId == node.Id)
            && !context.Set<RuntimeProfilingParticipationEntity>().Any(participation => participation.NodeId == node.Id)
            && !context.Set<RuntimeProfilingSnapshotEntity>().Any(snapshot => snapshot.NodeId == node.Id)
            && !context.Set<RuntimeProfilingMetricObservationEntity>().Any(observation => observation.NodeId == node.Id)
            && !context.Set<RuntimeProfilingSessionEntity>().Any(session => session.RuntimeContexts.Any(runtime => runtime.NodeId == node.Id)
                || session.Markers.Any(marker => marker.NodeId == node.Id) || session.Segments.Any(segment => segment.NodeId == node.Id)))
            .Select(node => node.Id).Take(maximum).ToArrayAsync(token).ConfigureAwait(false);
        if (unused.Length > 0)
        {
            await context.Set<ProfilingNodeEntity>().Where(node => unused.Contains(node.Id)).ExecuteDeleteAsync(token).ConfigureAwait(false);
        }
    }

    private static void ReleaseGate(ProfilingRuntimeGateEntity gate, Guid id)
    {
        if (gate?.MaintenanceClearId == id)
        {
            gate.MaintenanceClearId = null;
        }
    }

    private sealed record DeletionCandidate(Guid Id, long Bytes, int SummaryCount)
    {
        public long CompletedUtcTicks { get; init; }
    }
}
