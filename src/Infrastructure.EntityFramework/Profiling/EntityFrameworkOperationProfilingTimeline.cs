// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;

public sealed partial class EntityFrameworkProfilingStorageProvider<TContext>
{
    /// <inheritdoc />
    public Task<IResult<OperationProfilingTimeline>> TimelineAsync(OperationProfilingQuery query, int bucketCount = 72, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query?.StartedWithin != true || query.Cursor is not null || bucketCount is < 12 or > 256)
        {
            return Failure<OperationProfilingTimeline>(new ProfilingValidationError("Timeline requires start-time selection, no cursor and 12 to 256 buckets."));
        }

        return this.unitOfWork.ReadAsync(async (context, token) =>
        {
            var prepared = await this.PrepareQueryAsync(context, query, "timeline", token).ConfigureAwait(false);
            if (prepared.IsFailure) { return Result<OperationProfilingTimeline>.Failure(prepared); }

            var selection = prepared.Value;
            var from = selection.Query.FromUtc.Value;
            var to = selection.Query.ToUtc.Value;
            var width = OperationProfilingTimeline.Width(from, to, bucketCount);
            if (selection.Empty) { return Result<OperationProfilingTimeline>.Success(new() { FromUtc = from, ToUtc = to, BucketWidth = TimeSpan.FromTicks(width) }); }

            // Aggregate portable numeric UTC projections and canonical binary kinds in SQL.
            // Full immutable operation graphs are loaded only when a user opens execution details.
            var rows = await selection.Roots.GroupBy(root => new { root.KindBytes, Tick = root.StartedUtcTicks / width })
                .Select(group => new
                {
                    group.Key.KindBytes, group.Key.Tick, Kind = group.Min(root => root.Kind), Count = group.LongCount(),
                    FailedCount = group.LongCount(root => root.Outcome == OperationProfilingOutcome.Failed),
                    CanceledCount = group.LongCount(root => root.Outcome == OperationProfilingOutcome.Canceled),
                    AbortedCount = group.LongCount(root => root.Outcome == OperationProfilingOutcome.Aborted),
                    IncompleteCount = group.LongCount(root => root.Outcome == OperationProfilingOutcome.Incomplete),
                })
                .OrderBy(row => row.KindBytes).ThenBy(row => row.Tick)
                .Take(OperationProfilingTimeline.MaximumKinds * (bucketCount + 2) + 1).ToArrayAsync(token).ConfigureAwait(false);
            if (rows.Length > OperationProfilingTimeline.MaximumKinds * (bucketCount + 2)
                || rows.Select(row => Convert.ToHexString(row.KindBytes)).Distinct(StringComparer.Ordinal).Count() > OperationProfilingTimeline.MaximumKinds)
            {
                return Result<OperationProfilingTimeline>.Failure(new ProfilingQueryLimitError());
            }

            var nodeIds = EntityFrameworkOperationProfilingFilters.Apply(context, selection.Query with { NodeId = null }, selection.Boundary.CommitWatermark)
                .Select(root => root.NodeId).Distinct();
            var nodes = await context.Set<ProfilingNodeEntity>().AsNoTracking().Where(node => nodeIds.Contains(node.Id))
                .OrderBy(node => node.HostName).ThenBy(node => node.Key).Take(this.queryOptions.MaximumNodeChoices + 1).ToArrayAsync(token).ConfigureAwait(false);
            if (!await ValidRevisionAsync(context, selection.Boundary, token).ConfigureAwait(false)) { return Result<OperationProfilingTimeline>.Failure(new ProfilingQueryBoundaryError()); }

            var buckets = rows.Select(row => new OperationProfilingTimelineBucket
            {
                Kind = row.Kind, FromUtc = new(Math.Max(from.UtcTicks, row.Tick * width), TimeSpan.Zero),
                ToUtc = new(Math.Min(to.UtcTicks, (row.Tick + 1) * width), TimeSpan.Zero), Count = row.Count,
                FailedCount = row.FailedCount, CanceledCount = row.CanceledCount, AbortedCount = row.AbortedCount, IncompleteCount = row.IncompleteCount,
            }).ToArray();
            return Result<OperationProfilingTimeline>.Success(new()
            {
                FromUtc = from, ToUtc = to, BucketWidth = TimeSpan.FromTicks(width), TotalCount = rows.Sum(row => row.Count),
                Buckets = Array.AsReadOnly(buckets), Nodes = Array.AsReadOnly(nodes.Take(this.queryOptions.MaximumNodeChoices).Select(ProfilingEntityMapper.ToModel).ToArray()),
                NodesTruncated = nodes.Length > this.queryOptions.MaximumNodeChoices,
            });
        }, cancellationToken);
    }
}
