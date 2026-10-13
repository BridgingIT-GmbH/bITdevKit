// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

sealed partial class EntityFrameworkRuntimeProfilingStore<TContext>
{
    /// <inheritdoc />
    public async Task<IResult<RuntimeProfilingCorrelationSelection>> QueryCorrelationAsync(RuntimeProfilingCorrelationRequest request, CancellationToken cancellationToken = default)
    {
        var valid = OperationRuntimeCorrelationService.Validate(request);
        if (valid.IsFailure) { return Result<RuntimeProfilingCorrelationSelection>.Failure(valid); }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var node = await context.Set<ProfilingNodeEntity>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == request.Node.Identity.Id, cancellationToken).ConfigureAwait(false);
        if (node is null || !OperationRuntimeCorrelationService.MatchesNode(ProfilingEntityMapper.ToModel(node), request.Node))
        { return Result<RuntimeProfilingCorrelationSelection>.Success(new()); }

        var from = request.FromUtc.UtcTicks;
        var to = request.ToUtc.UtcTicks;
        var participations = await context.Set<RuntimeProfilingParticipationEntity>().AsNoTracking()
            .Where(value => value.NodeId == node.Id && value.JoinedUtcTicks <= to && (value.CompletedUtcTicks == null || value.CompletedUtcTicks >= from))
            .OrderBy(value => value.JoinedUtcTicks).Take(request.MaximumSnapshots + 1).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        if (participations.Length > request.MaximumSnapshots) { return Result<RuntimeProfilingCorrelationSelection>.Failure(new ProfilingQueryLimitError()); }

        var snapshots = new List<RuntimeProfilingSnapshot>();
        var sessions = new List<RuntimeProfilingSession>();
        var windows = new List<RuntimeProfilingNodeParticipation>();
        foreach (var participation in participations)
        {
            var session = await context.Set<RuntimeProfilingSessionEntity>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == participation.SessionId, cancellationToken).ConfigureAwait(false);
            if (session is null) { continue; }

            sessions.Add(ProfilingEntityMapper.ToModel(session));
            windows.Add(ProfilingEntityMapper.ToModel(participation, session.Key, node.Key));
            var source = context.Set<RuntimeProfilingSnapshotEntity>().AsNoTracking().Where(value => value.NodeId == node.Id && value.SessionId == session.Id
                && value.TimestampUtcTicks >= participation.JoinedUtcTicks && (participation.CompletedUtcTicks == null || value.TimestampUtcTicks <= participation.CompletedUtcTicks));
            var inside = await source.Where(value => value.TimestampUtcTicks >= from && (value.TimestampUtcTicks < to || from == to && value.TimestampUtcTicks == from))
                .OrderBy(value => value.TimestampUtcTicks).ThenBy(value => value.Sequence).Take(request.MaximumSnapshots - snapshots.Count + 1).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            snapshots.AddRange(inside.Select(value => ProfilingEntityMapper.ToModel(value, session.Key, node.Key)));
            if (snapshots.Count > request.MaximumSnapshots) { return Result<RuntimeProfilingCorrelationSelection>.Failure(new ProfilingQueryLimitError()); }

            var before = await source.Where(value => value.TimestampUtcTicks < from).OrderByDescending(value => value.TimestampUtcTicks).ThenByDescending(value => value.Sequence).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var after = await source.Where(value => from == to ? value.TimestampUtcTicks > to : value.TimestampUtcTicks >= to).OrderBy(value => value.TimestampUtcTicks).ThenBy(value => value.Sequence).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (before is not null) { snapshots.Add(ProfilingEntityMapper.ToModel(before, session.Key, node.Key)); }

            if (after is not null) { snapshots.Add(ProfilingEntityMapper.ToModel(after, session.Key, node.Key)); }

            if (snapshots.Count > request.MaximumSnapshots) { return Result<RuntimeProfilingCorrelationSelection>.Failure(new ProfilingQueryLimitError()); }
        }

        return Result<RuntimeProfilingCorrelationSelection>.Success(new() { Snapshots = snapshots.ToArray(), Sessions = sessions.ToArray(), Participations = windows.ToArray() });
    }
}
