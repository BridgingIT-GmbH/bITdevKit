// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>Transitions complete finalized graphs with durable age/replay and extraction-availability metadata.</summary>
/// <example><code>var count = await provider.ArchiveBeforeAsync(cutoff);</code></example>
public sealed partial class EntityFrameworkAuditStorageProvider<TContext>
{
    /// <inheritdoc />
    public Task<Result<long>> ArchiveBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1_000) return Task.FromResult(Fail<long>("AuditRetentionInvalid"));
        return this.work.ExecuteAsync(async (context, session, token) =>
        {
            var rows = await context.AuditRecords.IgnoreQueryFilters().AsNoTracking().Where(row => row.StorageState == AuditStorageState.Active && row.CompletedUtcTicks < cutoff.UtcTicks)
                .OrderBy(row => row.CompletedUtcTicks).ThenBy(row => row.Position).Select(row => row.AuditId).Take(batchSize).ToArrayAsync(token).ConfigureAwait(false);
            session.State.ArchiveCompletedBeforeUtcTicks = Math.Max(session.State.ArchiveCompletedBeforeUtcTicks, cutoff.UtcTicks);
            if (rows.Length == 0) return Result<long>.Success(0);
            var now = this.time.GetUtcNow().UtcTicks;
            var changed = await context.AuditRecords.IgnoreQueryFilters().Where(row => rows.Contains(row.AuditId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.StorageState, AuditStorageState.Archived).SetProperty(row => row.ArchivedUtcTicks, now), token).ConfigureAwait(false);
            if (changed > 0) session.State.LifecycleGeneration = checked(session.State.LifecycleGeneration + 1);
            return Result<long>.Success(changed);
        }, true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<long>> RemoveArchivedBeforeAsync(DateTimeOffset cutoff, int batchSize = 100, CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 1_000) return Task.FromResult(Fail<long>("AuditRetentionInvalid"));
        return this.work.ExecuteAsync(async (context, session, token) =>
        {
            var rows = await context.AuditRecords.IgnoreQueryFilters().AsNoTracking().Where(row => row.StorageState == AuditStorageState.Archived && row.CompletedUtcTicks < cutoff.UtcTicks)
                .OrderBy(row => row.CompletedUtcTicks).ThenBy(row => row.Position).Select(row => new { row.AuditId, row.Position }).Take(batchSize).ToArrayAsync(token).ConfigureAwait(false);
            session.State.RejectCompletedBeforeUtcTicks = Math.Max(session.State.RejectCompletedBeforeUtcTicks, cutoff.UtcTicks);
            if (rows.Length == 0) return Result<long>.Success(0);
            var ids = rows.Select(row => row.AuditId).ToArray();
            var changed = await context.AuditRecords.IgnoreQueryFilters().Where(row => ids.Contains(row.AuditId)).ExecuteDeleteAsync(token).ConfigureAwait(false);
            if (changed > 0)
            {
                session.State.RemovedThroughPosition = Math.Max(session.State.RemovedThroughPosition, rows.Max(row => row.Position));
                session.State.LifecycleGeneration = checked(session.State.LifecycleGeneration + 1);
            }

            return Result<long>.Success(changed);
        }, true, cancellationToken);
    }
}
