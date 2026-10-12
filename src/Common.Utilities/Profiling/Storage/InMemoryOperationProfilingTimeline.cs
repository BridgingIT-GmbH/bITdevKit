// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

public sealed partial class InMemoryProfilingStorageProvider
{
    /// <inheritdoc />
    public Task<IResult<OperationProfilingTimeline>> TimelineAsync(OperationProfilingQuery query, int bucketCount = 72, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query?.StartedWithin != true || query.Cursor is not null || bucketCount is < 12 or > 256)
        {
            return Failure<OperationProfilingTimeline>(new ProfilingValidationError("Timeline requires start-time selection, no cursor and 12 to 256 buckets."));
        }

        lock (this.sync)
        {
            var selection = this.Select(query, "timeline");
            if (selection.IsFailure) { return Completed(Result<OperationProfilingTimeline>.Failure(selection)); }

            var (normalized, boundary, _, matching) = selection.Value;
            var from = normalized.FromUtc.Value;
            var to = normalized.ToUtc.Value;
            var width = OperationProfilingTimeline.Width(from, to, bucketCount);
            var groups = matching.GroupBy(record => (Kind: ProfilingKeyComparer.Canonicalize(record.Kind), Tick: record.StartedUtc.UtcTicks / width));
            var buckets = groups.Select(group => new OperationProfilingTimelineBucket
            {
                Kind = group.First().Kind,
                FromUtc = new(Math.Max(from.UtcTicks, group.Key.Tick * width), TimeSpan.Zero),
                ToUtc = new(Math.Min(to.UtcTicks, (group.Key.Tick + 1) * width), TimeSpan.Zero),
                Count = group.LongCount(), FailedCount = group.LongCount(record => record.Outcome == OperationProfilingOutcome.Failed),
                CanceledCount = group.LongCount(record => record.Outcome == OperationProfilingOutcome.Canceled),
                AbortedCount = group.LongCount(record => record.Outcome == OperationProfilingOutcome.Aborted),
                IncompleteCount = group.LongCount(record => record.Outcome == OperationProfilingOutcome.Incomplete),
            }).OrderBy(bucket => ProfilingKeyComparer.Canonicalize(bucket.Kind), StringComparer.Ordinal).ThenBy(bucket => bucket.FromUtc).ToArray();
            if (buckets.Select(bucket => bucket.Kind).Distinct(ProfilingKeyComparer.Instance).Count() > OperationProfilingTimeline.MaximumKinds)
            {
                return Failure<OperationProfilingTimeline>(new ProfilingQueryLimitError());
            }

            var nodes = this.records.Values.Where(value => value.CommitWatermark <= boundary.CommitWatermark && Matches(value.Envelope.Record, normalized with { NodeId = null }))
                .Select(value => value.Envelope.Record.Node).DistinctBy(node => node.Identity.Id)
                .OrderBy(node => node.HostName, StringComparer.Ordinal).ThenBy(node => node.Identity.Key, StringComparer.Ordinal)
                .Take(this.queryOptions.MaximumNodeChoices + 1).ToArray();
            return Success(new OperationProfilingTimeline
            {
                FromUtc = from, ToUtc = to, BucketWidth = TimeSpan.FromTicks(width), TotalCount = matching.LongLength,
                Buckets = Array.AsReadOnly(buckets), Nodes = Array.AsReadOnly(nodes.Take(this.queryOptions.MaximumNodeChoices).ToArray()),
                NodesTruncated = nodes.Length > this.queryOptions.MaximumNodeChoices,
            });
        }
    }
}
