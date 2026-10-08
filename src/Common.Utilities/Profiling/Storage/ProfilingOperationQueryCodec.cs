// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed class ProfilingOperationQueryCodec(OperationProfilingOptions options, ProfilingQueryOptions queryOptions, TimeProvider clock, byte[] secret)
{
    private readonly OperationProfilingOptions options = options;
    private readonly ProfilingQueryOptions queryOptions = queryOptions;
    private readonly TimeProvider clock = clock;
    private readonly byte[] querySecret = secret;

    internal OperationProfilingQuery Normalize(OperationProfilingQuery query, ProfilingQueryBoundary boundary)
    {
        if (query is null || !Enum.IsDefined(query.View) || query.PageSize <= 0 || query.PageSize > this.queryOptions.MaximumPageSize
            || query.MaximumAnalysisCount <= 0 || query.MaximumAnalysisCount > this.queryOptions.MaximumAnalysisRecords
            || query.Outcomes is null || query.Outcomes.Count > 5 || query.Outcomes.Any(o => !Enum.IsDefined(o))
            || query.SegmentOutcomes is null || query.SegmentOutcomes.Count > 4 || query.SegmentOutcomes.Any(o => !Enum.IsDefined(o))
            || query.Dimensions is null || query.SegmentDimensions is null || query.GroupingDimensions is null
            || query.Dimensions.Count + query.SegmentDimensions.Count > this.queryOptions.MaximumDimensionPredicates
            || query.GroupingDimensions.Count > this.queryOptions.MaximumGroupingDimensions || query.HttpStatusCode is < 100 or > 599
            || query.FromUtc.HasValue != query.ToUtc.HasValue || query.FromUtc?.Offset != null && query.FromUtc.Value.Offset != TimeSpan.Zero
            || query.ToUtc?.Offset != null && query.ToUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Invalid profiling query.");
        }

        var from = query.FromUtc ?? boundary?.FromUtc ?? this.clock.GetUtcNow().Subtract(TimeSpan.FromMinutes(15));
        var to = query.ToUtc ?? boundary?.ToUtc ?? this.clock.GetUtcNow();
        if (from >= to || query.SegmentPath is { } path && (path.Components.Count > this.options.MaxSegmentDepth || path.Components.Any(c => !ProfilingValueValidator.IsKey(c, this.options.MaxKeyLength))))
        {
            throw new ArgumentException("Invalid profiling interval or path.");
        }

        string Key(string text, int? maximum = null)
        {
            if (text is not null && !ProfilingValueValidator.IsKey(text, maximum ?? this.options.MaxKeyLength))
            {
                throw new ArgumentException("Invalid profiling filter key.");
            }

            return ProfilingKeyComparer.Canonicalize(text);
        }

        string Value(string text)
        {
            if (text is not null && (text.Length > this.options.MaxStringLength || !ProfilingValueValidator.IsUnicode(text)))
            {
                throw new ArgumentException("Invalid profiling lookup value.");
            }

            return text;
        }

        IReadOnlyList<ProfilingDimensionPredicate> Predicates(IReadOnlyList<ProfilingDimensionPredicate> predicates, bool segment)
        {
            var result = predicates.Select(predicate =>
            {
                if (predicate is null || !Enum.IsDefined(predicate.Operator) || !segment && predicate.Operator == ProfilingDimensionOperator.Mixed
                    || predicate.Operator == ProfilingDimensionOperator.Equal && predicate.Value is null
                    || predicate.Operator != ProfilingDimensionOperator.Equal && predicate.Value is not null
                    || predicate.Value?.Type == ProfilingValueType.String && (predicate.Value.Scalar.Length > this.options.MaxStringLength || !ProfilingValueValidator.IsUnicode(predicate.Value.Scalar)))
                {
                    throw new ArgumentException("Invalid profiling dimension predicate.");
                }

                return predicate with { Key = Key(predicate.Key) ?? throw new ArgumentException("A dimension name is required.") };
            }).OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Operator).ThenBy(p => p.Value?.Type).ThenBy(p => p.Value?.Scalar, StringComparer.Ordinal).ToArray();
            return Array.AsReadOnly(result);
        }

        var grouping = query.GroupingDimensions.Select(d => Key(d) ?? throw new ArgumentException("A grouping dimension name is required.")).Order(StringComparer.Ordinal).ToArray();
        if (grouping.Distinct(StringComparer.Ordinal).Count() != grouping.Length)
        {
            throw new ArgumentException("Duplicate profiling grouping dimensions.");
        }

        return query with
        {
            FromUtc = from, ToUtc = to, Kind = Key(query.Kind), Key = Key(query.Key), SegmentKey = Key(query.SegmentKey), HttpMethod = Key(query.HttpMethod), Route = Key(query.Route, this.options.MaxStringLength),
            SamplingStrategyKey = Key(query.SamplingStrategyKey), SamplingConfigurationKey = Key(query.SamplingConfigurationKey),
            CorrelationId = Value(query.CorrelationId), ApplicationRequestId = Value(query.ApplicationRequestId), ApplicationVersion = Value(query.ApplicationVersion),
            SegmentPath = query.SegmentPath is null ? null : new ProfilingSegmentPath(query.SegmentPath.Components.Select(c => Key(c)).ToArray()),
            Dimensions = Predicates(query.Dimensions, false), SegmentDimensions = Predicates(query.SegmentDimensions, true), GroupingDimensions = Array.AsReadOnly(grouping),
            Outcomes = Array.AsReadOnly(query.Outcomes.Distinct().Order().ToArray()), SegmentOutcomes = Array.AsReadOnly(query.SegmentOutcomes.Distinct().Order().ToArray()),
        };
    }

    internal string Encode(ProfilingPageCursor cursor)
    {
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor));
        return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + Convert.ToHexStringLower(HMACSHA256.HashData(this.querySecret, data));
    }

    internal ProfilingPageCursor Decode(string value)
    {
        if (value.Length > 8192)
        {
            throw new FormatException("Oversized cursor.");
        }

        var parts = value.Split('.');
        if (parts.Length != 2)
        {
            throw new FormatException("Invalid cursor.");
        }

        var text = parts[0].Replace('-', '+').Replace('_', '/');
        text += new string('=', (4 - text.Length % 4) % 4);
        var data = Convert.FromBase64String(text);
        if (!TagsEqual(parts[1], Convert.ToHexStringLower(HMACSHA256.HashData(this.querySecret, data))))
        {
            throw new FormatException("Invalid cursor authentication.");
        }

        var cursor = JsonSerializer.Deserialize<ProfilingPageCursor>(data);
        if (cursor?.Boundary is null || cursor.Identity is null || cursor.SortValue < 0)
        {
            throw new FormatException("Invalid continuation key.");
        }

        return cursor;
    }

    internal string SignBoundary(ProfilingQueryBoundary boundary) => Convert.ToHexStringLower(HMACSHA256.HashData(this.querySecret, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(boundary))));
    internal static bool TagsEqual(string left, string right) => left?.Length == 64 && right?.Length == 64
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
}

internal sealed record ProfilingPageCursor(long SortValue, string Identity, string Target, ProfilingQueryBoundary Boundary);
