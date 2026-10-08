// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Security.Cryptography;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

// Every mutation is made inside an operation-owned relational transaction after acquiring
// the singleton row. Its database lock, rather than a process-local semaphore, is the scope boundary.
internal sealed class EntityFrameworkProfilingCoordination<TContext>(ProfilingStorageOptions options, string providerScope)
    where TContext : DbContext, IProfilingDbContext
{
    internal async Task<Frame> AcquireAsync(TContext context, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Profiling coordination requires an existing relational transaction.");
        }

        var version = Guid.NewGuid();
        var changed = await context.Set<ProfilingStoreStateEntity>().Where(state => state.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.ConcurrencyVersion, version), cancellationToken).ConfigureAwait(false);
        if (changed == 0)
        {
            if (await context.Set<OperationProfilingEntity>().AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ArgumentException("An existing operation dataset requires its original durable coordination state.");
            }

            context.Set<ProfilingStoreStateEntity>().Add(new() { Id = 1, StoreEpoch = Guid.NewGuid(), QuerySecret = RandomNumberGenerator.GetBytes(32), ConcurrencyVersion = version });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var state = await context.Set<ProfilingStoreStateEntity>().SingleAsync(entity => entity.Id == 1, cancellationToken).ConfigureAwait(false);
        if (state.StoreEpoch == Guid.Empty || state.QuerySecret?.Length != 32 || state.PublicationWatermark < 0 || state.DeletionRevision < 0
            || state.RetainedOperationCount < 0 || state.RetainedOperationBytes < 0)
        {
            throw new ArgumentException("The durable profiling store state is invalid.");
        }

        var writerRows = await context.Set<ProfilingWriterEntity>().Take(options.MaximumWriterLeases == int.MaxValue ? int.MaxValue : options.MaximumWriterLeases + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        var registry = new ProfilingWriterRegistry(state.StoreEpoch, options.MaximumWriterLeases);
        registry.Restore(state.RegistrationGeneration, writerRows.Select(row => new ProfilingWriterRegistrationState(row.AttemptId, Lease(row), row.Retired)).ToArray());
        var clearRows = await context.Set<ProfilingClearEntity>().Take(options.MaximumClearFences == int.MaxValue ? int.MaxValue : options.MaximumClearFences + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        var clears = new ProfilingClearCoordinator(options, providerScope);
        clears.Restore(clearRows.Select(row => ReadClear(row, options)).ToArray());
        return new(state, registry, clears, writerRows, clearRows);
    }

    internal static async Task SaveAsync(TContext context, Frame frame, CancellationToken cancellationToken)
    {
        frame.State.RegistrationGeneration = frame.Writers.Generation;
        var writers = frame.Writers.Snapshot();
        var ids = writers.Select(writer => writer.Lease.WriterId).ToHashSet();
        context.Set<ProfilingWriterEntity>().RemoveRange(frame.WriterRows.Where(row => !ids.Contains(row.Id)));
        foreach (var writer in writers)
        {
            var row = frame.WriterRows.SingleOrDefault(entity => entity.Id == writer.Lease.WriterId);
            if (row is null)
            {
                row = new() { Id = writer.Lease.WriterId };
                context.Set<ProfilingWriterEntity>().Add(row);
            }

            row.AttemptId = writer.AttemptId;
            row.Token = writer.Lease.Token;
            row.StoreEpoch = writer.Lease.StoreEpoch;
            row.NodeId = writer.Lease.NodeId;
            row.Generation = writer.Lease.Generation;
            row.ExpiresUtcTicks = writer.Lease.ExpiresUtc.UtcTicks;
            row.SettledThrough = writer.Lease.SettledThrough;
            row.Retired = writer.Retired;
        }

        var clearIds = frame.Clears.Clears.Select(clear => clear.Id).ToHashSet();
        context.Set<ProfilingClearEntity>().RemoveRange(frame.ClearRows.Where(row => !clearIds.Contains(row.Id)));
        foreach (var clear in frame.Clears.Clears)
        {
            var row = frame.ClearRows.SingleOrDefault(entity => entity.Id == clear.Id);
            if (row is null)
            {
                row = new() { Id = clear.Id };
                context.Set<ProfilingClearEntity>().Add(row);
            }

            row.DataSet = clear.Selection.DataSet;
            row.FromUtcTicks = clear.Selection.FromUtc?.UtcTicks;
            row.ToUtcTicks = clear.Selection.ToUtc?.UtcTicks;
            row.PreparedUtcTicks = clear.PreparedUtc.UtcTicks;
            row.DeadlineUtcTicks = clear.DeadlineUtc.UtcTicks;
            row.SealedUtcTicks = clear.SealedUtc?.UtcTicks;
            row.CompletedUtcTicks = clear.CompletedUtc?.UtcTicks;
            row.GenerationBoundary = clear.Generation;
            row.State = clear.State;
            row.EligibleWritersJson = JsonSerializer.Serialize(clear.Eligible.Values);
            row.AcknowledgementsJson = JsonSerializer.Serialize(clear.Cutoffs);
            row.RemovedOperationCount = clear.RemovedOperations;
            row.RemovedSummaryCount = clear.RemovedSummaries;
            row.RemovedRuntimeSessionCount = clear.RemovedRuntimeSessions;
            row.RemovedSnapshotCount = clear.RemovedSnapshots;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static ProfilingWriterLease Lease(ProfilingWriterEntity entity) => new()
    {
        WriterId = entity.Id, Token = entity.Token, StoreEpoch = entity.StoreEpoch, Generation = entity.Generation,
        NodeId = entity.NodeId, ExpiresUtc = Utc(entity.ExpiresUtcTicks), SettledThrough = entity.SettledThrough,
    };

    internal static DateTimeOffset Utc(long ticks) => new(ticks, TimeSpan.Zero);
    private static DateTimeOffset? Utc(long? ticks) => ticks.HasValue ? Utc(ticks.Value) : null;

    private static ProfilingClearCoordinator.Clear ReadClear(ProfilingClearEntity entity, ProfilingStorageOptions limits)
    {
        // Coordination documents have explicit entry bounds before they enter the reusable state machine.
        if (entity.EligibleWritersJson?.Length > (long)limits.MaximumWriterLeases * 1024 || entity.AcknowledgementsJson?.Length > (long)limits.MaximumWriterLeases * 256)
        {
            throw new ArgumentException("The stored profiling clear exceeds its bounded coordination document size.");
        }

        var eligible = JsonSerializer.Deserialize<ProfilingWriterLease[]>(entity.EligibleWritersJson)
            ?? throw new ArgumentException("Stored clear eligibility is absent.");
        var acknowledgements = JsonSerializer.Deserialize<Dictionary<Guid, long>>(entity.AcknowledgementsJson)
            ?? throw new ArgumentException("Stored clear acknowledgements are absent.");
        if (eligible.Length > limits.MaximumWriterLeases || acknowledgements.Count > eligible.Length || eligible.Any(lease => lease is null))
        {
            throw new ArgumentException("The stored clear exceeds its original writer entry bound.");
        }

        var clear = new ProfilingClearCoordinator.Clear(entity.Id, new()
        {
            DataSet = entity.DataSet, FromUtc = Utc(entity.FromUtcTicks), ToUtc = Utc(entity.ToUtcTicks),
        }, Utc(entity.PreparedUtcTicks), Utc(entity.DeadlineUtcTicks), entity.GenerationBoundary, eligible)
        {
            State = entity.State, SealedUtc = Utc(entity.SealedUtcTicks), CompletedUtc = Utc(entity.CompletedUtcTicks),
            RemovedOperations = entity.RemovedOperationCount, RemovedSummaries = entity.RemovedSummaryCount,
            RemovedRuntimeSessions = entity.RemovedRuntimeSessionCount, RemovedSnapshots = entity.RemovedSnapshotCount,
        };
        foreach (var entry in acknowledgements)
        {
            clear.Cutoffs.Add(entry.Key, entry.Value);
        }

        return clear;
    }

    internal sealed record Frame(ProfilingStoreStateEntity State, ProfilingWriterRegistry Writers, ProfilingClearCoordinator Clears,
        IReadOnlyList<ProfilingWriterEntity> WriterRows, IReadOnlyList<ProfilingClearEntity> ClearRows);
}
