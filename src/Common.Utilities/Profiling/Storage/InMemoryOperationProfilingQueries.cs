// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

public sealed partial class InMemoryProfilingStorageProvider
{
    /// <inheritdoc />
    public Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            var selection = this.Select(query, "records");
            if (selection.IsFailure)
            {
                return Completed(Result<OperationProfilingPage>.Failure(selection));
            }

            var (normalized, boundary, cursor, matching) = selection.Value;
            var ordered = Order(matching, normalized.View);
            var page = ordered.Where(r => After(r, normalized.View, cursor)).Take(normalized.PageSize + 1).ToArray();
            var hasMore = page.Length > normalized.PageSize;
            var values = page.Take(normalized.PageSize).ToArray();
            var next = hasMore ? this.Cursor(values[^1], normalized.View, "records", boundary) : null;
            return Success(new OperationProfilingPage
            {
                Records = Array.AsReadOnly(values), TotalCount = matching.LongLength, Boundary = boundary, HasMore = hasMore, NextCursor = next,
            });
        }
    }

    /// <inheritdoc />
    public Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            var selection = this.Select(query, "groups");
            if (selection.IsFailure)
            {
                return Completed(Result<OperationProfilingGroupPage>.Failure(selection));
            }

            var (normalized, boundary, cursor, matching) = selection.Value;
            var allGroups = matching.GroupBy(r => GroupKey(r, normalized), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            OperationProfilingGroup[] groups;
            bool hasMore;
            string next;
            if (normalized.View == OperationProfilingView.ByCount)
            {
                var ordered = allGroups.OrderByDescending(g => g.Value.LongLength).ThenBy(g => g.Key, StringComparer.Ordinal)
                    .Where(g => cursor is null || g.Value.LongLength < cursor.SortValue || g.Value.LongLength == cursor.SortValue && StringComparer.Ordinal.Compare(g.Key, cursor.Identity) > 0)
                    .Take(normalized.PageSize + 1).ToArray();
                hasMore = ordered.Length > normalized.PageSize;
                groups = ordered.Take(normalized.PageSize).Select(g => Group(g.Key, g.Value, [], normalized)).ToArray();
                next = hasMore ? this.queryCodec.Encode(new ProfilingPageCursor(groups[^1].Count, groups[^1].ComparisonKey, "groups", boundary)) : null;
            }
            else
            {
                var ordered = Order(matching, normalized.View).Where(r => After(r, normalized.View, cursor)).Take(normalized.PageSize + 1).ToArray();
                hasMore = ordered.Length > normalized.PageSize;
                var visible = ordered.Take(normalized.PageSize).ToArray();
                groups = visible.GroupBy(r => GroupKey(r, normalized), StringComparer.Ordinal)
                    .Select(g => Group(g.Key, allGroups[g.Key], g.ToArray(), normalized)).ToArray();
                next = hasMore ? this.Cursor(visible[^1], normalized.View, "groups", boundary) : null;
            }

            return Success(new OperationProfilingGroupPage
            {
                Groups = Array.AsReadOnly(groups), TotalGroupCount = allGroups.Count, TotalOperationCount = matching.LongLength,
                Boundary = boundary, HasMore = hasMore, NextCursor = next,
            });
        }
    }

    /// <inheritdoc />
    public Task<IResult<OperationProfilingAnalysisSelection>> SelectAnalysisAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.sync)
        {
            var selection = this.Select(query, "analysis");
            if (selection.IsFailure)
            {
                return Completed(Result<OperationProfilingAnalysisSelection>.Failure(selection));
            }

            if (selection.Value.Records.LongLength > selection.Value.Query.MaximumAnalysisCount)
            {
                return Failure<OperationProfilingAnalysisSelection>(new ProfilingQueryLimitError());
            }

            return Success(new OperationProfilingAnalysisSelection { Records = Array.AsReadOnly(selection.Value.Records), Boundary = selection.Value.Boundary });
        }
    }

    private Result<QuerySelection> Select(OperationProfilingQuery query, string target)
    {
        try
        {
            var cursor = query?.Cursor is not null ? this.queryCodec.Decode(query.Cursor) : null;
            if (cursor is not null && cursor.Target != target || query?.Boundary is not null && cursor is not null && query.Boundary != cursor.Boundary)
            {
                return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
            }

            var normalized = this.queryCodec.Normalize(query, cursor?.Boundary ?? query?.Boundary);
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized with { Boundary = null, Cursor = null, PageSize = 0, MaximumAnalysisCount = 0 }))));
            var boundary = cursor?.Boundary ?? query.Boundary;
            if (boundary is null)
            {
                boundary = new ProfilingQueryBoundary
                {
                    StoreEpoch = this.epoch, CommitWatermark = this.watermark, DeletionRevision = this.deletionRevision, FilterFingerprint = fingerprint,
                    FromUtc = normalized.FromUtc, ToUtc = normalized.ToUtc, ExpiresUtc = this.clock.GetUtcNow().Add(this.queryOptions.BoundaryLifetime),
                };
                boundary = boundary with { Signature = this.queryCodec.SignBoundary(boundary) };
            }
            else if (boundary.StoreEpoch != this.epoch || boundary.CommitWatermark > this.watermark || boundary.CommitWatermark < 0
                || boundary.DeletionRevision != this.deletionRevision || boundary.ExpiresUtc <= this.clock.GetUtcNow()
                || boundary.FilterFingerprint != fingerprint || !ProfilingOperationQueryCodec.TagsEqual(boundary.Signature, this.queryCodec.SignBoundary(boundary with { Signature = null })))
            {
                return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
            }

            var matching = this.records.Values.Where(r => r.CommitWatermark <= boundary.CommitWatermark && Matches(r.Envelope.Record, normalized)).Select(r => r.Envelope.Record).ToArray();
            return Result<QuerySelection>.Success(new(normalized, boundary, cursor, matching));
        }
        catch (ArgumentException)
        {
            return Result<QuerySelection>.Failure(new ProfilingValidationError("Profiling filters require bounded keys, typed values, UTC bounds and supported selectors."));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return Result<QuerySelection>.Failure(new ProfilingQueryBoundaryError());
        }
    }

    private static bool Matches(OperationProfilingRecord record, OperationProfilingQuery query)
    {
        var interval = query.IntervalOverlap ? record.StartedUtc < query.ToUtc && (record.CompletedUtc > query.FromUtc || record.StartedUtc == record.CompletedUtc && record.StartedUtc >= query.FromUtc)
            : record.CompletedUtc >= query.FromUtc && record.CompletedUtc < query.ToUtc;
        if (!interval || query.Id.HasValue && record.Id != query.Id.Value || query.ParentOperationId.HasValue && record.ParentOperationId != query.ParentOperationId
            || query.NodeId.HasValue && record.NodeId != query.NodeId || query.Key is not null && ProfilingKeyComparer.Canonicalize(record.Key) != query.Key
            || query.Kind is not null && ProfilingKeyComparer.Canonicalize(record.Kind) != query.Kind
            || query.CorrelationId is not null && record.CorrelationId != query.CorrelationId || query.ApplicationVersion is not null && record.Node?.ApplicationVersion != query.ApplicationVersion
            || query.Outcomes.Count > 0 && !query.Outcomes.Contains(record.Outcome)
            || !query.Dimensions.Concat(query.GroupDimensions).All(p => MatchDimension(record.Dimensions.FirstOrDefault(d => ProfilingKeyComparer.Canonicalize(d.Key) == p.Key)?.Value, p, false, false)))
        {
            return false;
        }

        var failedSegments = record.Segments.Any(s => s.Outcomes.Any(o => o.Outcome == ProfilingSegmentOutcome.Failed && o.Statistics.Count > 0));
        if (query.HasSegmentFailures.HasValue && failedSegments != query.HasSegmentFailures.Value)
        {
            return false;
        }

        var http = record.Http;
        if (query.HttpMethod is not null && ProfilingKeyComparer.Canonicalize(http?.Method) != query.HttpMethod
            || query.Route is not null && ProfilingKeyComparer.Canonicalize(http?.Route) != query.Route || query.HttpStatusCode.HasValue && http?.StatusCode != query.HttpStatusCode
            || query.ApplicationRequestId is not null && http?.ApplicationRequestId != query.ApplicationRequestId
            || query.SamplingStrategyKey is not null && ProfilingKeyComparer.Canonicalize(http?.SamplingStrategyKey) != query.SamplingStrategyKey
            || query.SamplingConfigurationKey is not null && ProfilingKeyComparer.Canonicalize(http?.SamplingConfigurationKey) != query.SamplingConfigurationKey)
        {
            return false;
        }

        if (query.SegmentKey is null && query.SegmentPath is null && query.SegmentDimensions.Count == 0 && query.SegmentOutcomes.Count == 0)
        {
            return true;
        }

        return record.Segments.Any(summary => (query.SegmentKey is null || ProfilingKeyComparer.Canonicalize(summary.Key) == query.SegmentKey)
            && (query.SegmentPath is null || summary.Path.Equals(query.SegmentPath))
            && (query.SegmentOutcomes.Count == 0 || summary.Outcomes.Any(o => query.SegmentOutcomes.Contains(o.Outcome) && o.Statistics.Count > 0))
            && query.SegmentDimensions.All(predicate =>
            {
                var dimension = summary.Dimensions.FirstOrDefault(d => ProfilingKeyComparer.Canonicalize(d.Key) == predicate.Key);
                return MatchDimension(dimension?.Value, predicate, dimension?.Mixed == true, dimension?.Partial == true);
            }));
    }

    private static bool MatchDimension(ProfilingValue value, ProfilingDimensionPredicate predicate, bool mixed, bool partial) => predicate.Operator switch
    {
        ProfilingDimensionOperator.Equal => !mixed && !partial && value == predicate.Value,
        ProfilingDimensionOperator.Present => value is not null || mixed,
        ProfilingDimensionOperator.Missing => value is null && !mixed,
        ProfilingDimensionOperator.Mixed => mixed,
        _ => false,
    };

    private static IOrderedEnumerable<OperationProfilingRecord> Order(IEnumerable<OperationProfilingRecord> records, OperationProfilingView view) =>
        view == OperationProfilingView.Recent ? records.OrderByDescending(r => r.CompletedUtc).ThenByDescending(r => Id(r.Id), StringComparer.Ordinal)
            : records.OrderByDescending(r => r.Duration).ThenBy(r => Id(r.Id), StringComparer.Ordinal);

    private static bool After(OperationProfilingRecord record, OperationProfilingView view, ProfilingPageCursor cursor)
    {
        if (cursor is null)
        {
            return true;
        }

        var value = view == OperationProfilingView.Recent ? record.CompletedUtc.UtcTicks : record.Duration.Ticks;
        var comparison = StringComparer.Ordinal.Compare(Id(record.Id), cursor.Identity);
        return value < cursor.SortValue || value == cursor.SortValue && (view == OperationProfilingView.Recent ? comparison < 0 : comparison > 0);
    }

    private static string GroupKey(OperationProfilingRecord record, OperationProfilingQuery query)
    {
        var result = new StringBuilder();
        Part(ProfilingKeyComparer.Canonicalize(record.Kind));
        Part(ProfilingKeyComparer.Canonicalize(record.Key));
        if (query.GroupByHttpMethod)
        {
            Part(record.Http?.Method is null ? "missing" : "present:" + ProfilingKeyComparer.Canonicalize(record.Http.Method));
        }

        foreach (var key in query.GroupingDimensions)
        {
            Part(key);
            var dimension = record.Dimensions.FirstOrDefault(d => ProfilingKeyComparer.Canonicalize(d.Key) == key);
            Part(dimension is null ? "missing" : "present:" + dimension.Value.Type + ":" + dimension.Value.Scalar);
        }

        return result.ToString();
        void Part(string value) => result.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }

    private static OperationProfilingGroup Group(string key, OperationProfilingRecord[] all, OperationProfilingRecord[] visible, OperationProfilingQuery query)
    {
        var label = all.OrderBy(r => r.StartedUtc).ThenBy(r => Id(r.Id), StringComparer.Ordinal).First();
        var representative = visible.Length > 0 ? visible[0] : Order(all, OperationProfilingView.Slow).First();
        var dimensions = query.GroupingDimensions.Select(name => label.Dimensions.FirstOrDefault(d => ProfilingKeyComparer.Canonicalize(d.Key) == name))
            .Where(d => d is not null).ToArray();
        long total = 0;
        var unavailable = false;
        foreach (var record in all)
        {
            try { total = checked(total + record.Duration.Ticks); }
            catch (OverflowException) { unavailable = true; }
        }

        return new()
        {
            Key = label.Key, Kind = label.Kind, ComparisonKey = key, HttpMethod = query.GroupByHttpMethod ? label.Http?.Method : null,
            Dimensions = Array.AsReadOnly(dimensions), Count = all.LongLength, TotalDuration = unavailable ? TimeSpan.Zero : TimeSpan.FromTicks(total), DurationUnavailable = unavailable,
            MinimumDuration = all.Min(r => r.Duration), MaximumDuration = all.Max(r => r.Duration), LatestCompletedUtc = all.Max(r => r.CompletedUtc),
            Representative = representative, Occurrences = Array.AsReadOnly(visible),
        };
    }

    private string Cursor(OperationProfilingRecord record, OperationProfilingView view, string target, ProfilingQueryBoundary boundary) =>
        this.queryCodec.Encode(new ProfilingPageCursor(view == OperationProfilingView.Recent ? record.CompletedUtc.UtcTicks : record.Duration.Ticks, Id(record.Id), target, boundary));

    private static string Id(Guid id) => id.ToString("N");
    private sealed record QuerySelection(OperationProfilingQuery Query, ProfilingQueryBoundary Boundary, ProfilingPageCursor Cursor, OperationProfilingRecord[] Records);
}
