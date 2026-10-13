// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Shares immutable operation matching between in-memory queries and selective clear fences.</summary>
/// <example><code>var selected = OperationProfilingQueryMatcher.Matches(record, normalizedQuery);</code></example>
public static class OperationProfilingQueryMatcher
{
    /// <summary>Matches an operation against a validated, normalized selection with explicit UTC bounds, independently of paging.</summary>
    /// <example><code>var selected = OperationProfilingQueryMatcher.Matches(record, normalizedQuery);</code></example>
    public static bool Matches(OperationProfilingRecord record, OperationProfilingQuery query)
    {
        var interval = query.StartedWithin ? record.StartedUtc >= query.FromUtc && record.StartedUtc < query.ToUtc
            : query.IntervalOverlap ? record.StartedUtc < query.ToUtc && (record.CompletedUtc > query.FromUtc || record.StartedUtc == record.CompletedUtc && record.StartedUtc >= query.FromUtc)
            : record.CompletedUtc >= query.FromUtc && record.CompletedUtc < query.ToUtc;
        if (!interval || query.Id.HasValue && record.Id != query.Id.Value || query.ParentOperationId.HasValue && record.ParentOperationId != query.ParentOperationId
            || query.NodeId.HasValue && record.NodeId != query.NodeId || query.Key is not null && ProfilingKeyComparer.Canonicalize(record.Key) != query.Key
            || query.KeyContains is not null && !ProfilingKeyComparer.Canonicalize(record.Key).Contains(query.KeyContains, StringComparison.Ordinal)
            || query.Kind is not null && ProfilingKeyComparer.Canonicalize(record.Kind) != query.Kind
            || query.CorrelationId is not null && record.CorrelationId != query.CorrelationId || query.ApplicationVersion is not null && record.Node?.ApplicationVersion != query.ApplicationVersion
            || query.Outcomes.Count > 0 && !query.Outcomes.Contains(record.Outcome)
            || !query.GroupingDimensions.All(key => record.Dimensions.Any(dimension => ProfilingKeyComparer.Canonicalize(dimension.Key) == key && dimension.Value is not null))
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

}
