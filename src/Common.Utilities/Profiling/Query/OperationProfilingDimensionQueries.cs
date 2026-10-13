// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Text.Json;

public sealed partial class OperationProfilingQueryService
{
    private async Task<IResult<OperationProfilingDimensionDiscovery>> DiscoverDimensionsCoreAsync(OperationProfilingQuery query,
        ProfilingDimensionScope scope, string dimensionKey, string nameSearch, string valueSearch, CancellationToken token)
    {
        var valid = this.validator.Validate(query);
        if (valid.IsFailure) { return Result<OperationProfilingDimensionDiscovery>.Failure(valid); }

        if (!Enum.IsDefined(scope) || scope == ProfilingDimensionScope.Segment && query.SegmentPath is null
            || dimensionKey is not null && !ProfilingValueValidator.IsKey(dimensionKey, this.options.Operations.MaxKeyLength)
            || new[] { nameSearch, valueSearch }.Any(text => text is not null && (text.Length > this.options.Operations.MaxStringLength || !ProfilingValueValidator.IsUnicode(text))))
        { return Result<OperationProfilingDimensionDiscovery>.Failure(new ProfilingValidationError("Dimension discovery requires bounded search text and an explicit path for segment metadata.")); }

        // Discovery uses all matching retained owners within the exact-analysis bound, never only a visible page.
        var selection = await this.store.SelectAnalysisAsync(query with { Cursor = null, Boundary = null, GroupingDimensions = [] }, token).ConfigureAwait(false);
        if (selection.IsFailure) { return Result<OperationProfilingDimensionDiscovery>.Failure(selection); }

        var records = selection.Value.Records;
        if (records.Count > Math.Min(query.MaximumAnalysisCount, this.options.Queries.MaximumAnalysisRecords))
        { return Result<OperationProfilingDimensionDiscovery>.Failure(new ProfilingQueryLimitError()); }

        var owners = new List<IReadOnlyList<ProfilingSegmentDimensionSummary>>(records.Count);
        foreach (var record in records)
        {
            token.ThrowIfCancellationRequested();
            if (scope == ProfilingDimensionScope.Operation)
            { owners.Add(record.Dimensions.Select(dimension => new ProfilingSegmentDimensionSummary { Key = dimension.Key, Value = dimension.Value }).ToArray()); }
            else if (record.Segments.FirstOrDefault(segment => segment.Path.Equals(query.SegmentPath) && OperationProfilingQueryValidator.MatchesSegment(segment, query)) is { } segment)
            { owners.Add(segment.Dimensions); }
        }

        var indexed = owners.SelectMany(owner => owner).Where(dimension => !ProfilingDimensionKeys.IsExecutionIdentifier(dimension.Key))
            .GroupBy(dimension => dimension.Key, ProfilingKeyComparer.Instance)
            .ToDictionary(group => group.Key, group => group.ToArray(), ProfilingKeyComparer.Instance);
        var names = ProfilingDimensionKeys.IsExecutionIdentifier(dimensionKey) ? []
            : dimensionKey is not null ? new[] { indexed.Keys.FirstOrDefault(key => ProfilingKeyComparer.Instance.Equals(key, dimensionKey)) ?? dimensionKey }
            : indexed.Keys.Where(key => string.IsNullOrEmpty(nameSearch) || key.Contains(nameSearch, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ThenBy(key => key, StringComparer.Ordinal).ToArray();
        var choices = new List<ProfilingDimensionChoice>();
        foreach (var name in names.Take(this.options.Queries.MaximumDimensionNames))
        {
            token.ThrowIfCancellationRequested();
            var observations = indexed.GetValueOrDefault(name) ?? [];
            var values = observations.Where(dimension => !dimension.Mixed && !dimension.Partial && dimension.Value is not null)
                .GroupBy(dimension => dimension.Value).Select(group => new ProfilingDimensionValueChoice { Value = group.Key, Count = group.LongCount() }).ToArray();
            var matching = values.Where(value => string.IsNullOrEmpty(valueSearch) || value.Value.Scalar.Contains(valueSearch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(value => value.Count).ThenBy(value => value.Value.Type).ThenBy(value => value.Value.Scalar, StringComparer.Ordinal).ToArray();
            var present = observations.LongCount(dimension => dimension.Value is not null || dimension.Mixed);
            choices.Add(new()
            {
                Key = name, PresentCount = present, MissingCount = owners.Count - present,
                MixedCount = observations.LongCount(dimension => dimension.Mixed), PartialCount = observations.LongCount(dimension => dimension.Partial),
                DistinctValueCount = values.Length, ValuesTruncated = matching.Length > this.options.Queries.MaximumDimensionValues,
                Values = Array.AsReadOnly(matching.Take(this.options.Queries.MaximumDimensionValues).ToArray()),
            });
        }

        var paths = records.SelectMany(record => record.Segments).GroupBy(segment => segment.Path.ComparisonKey, StringComparer.Ordinal)
            .Select(group => new ProfilingSegmentPathChoice { Path = group.First().Path, Count = group.LongCount() })
            .OrderByDescending(path => path.Count).ThenBy(path => path.Path.ComparisonKey, StringComparer.Ordinal).ToArray();
        return Result<OperationProfilingDimensionDiscovery>.Success(new()
        {
            SegmentPaths = Array.AsReadOnly(paths.Take(this.options.Queries.MaximumDimensionNames).ToArray()), PathsTruncated = paths.Length > this.options.Queries.MaximumDimensionNames,
            Scope = scope, SegmentPath = scope == ProfilingDimensionScope.Segment ? query.SegmentPath : null, OperationCount = owners.Count,
            MatchingNameCount = names.Length, NamesTruncated = names.Length > this.options.Queries.MaximumDimensionNames,
            Dimensions = Array.AsReadOnly(choices.ToArray()), Boundary = selection.Value.Boundary,
        });
    }

    private async Task<IResult<OperationProfilingGroupPage>> GroupCoreAsync(OperationProfilingQuery query, CancellationToken token)
    {
        var grouped = await this.store.GroupAsync(query, token).ConfigureAwait(false);
        if (grouped.IsFailure || query.GroupingDimensions.Count == 0 || grouped.Value.Groups.Count == 0) { return grouped; }

        // Reuse the group's publication boundary so aggregate statistics cannot drift from its counts.
        var selection = await this.store.SelectAnalysisAsync(query with { Cursor = null, Boundary = grouped.Value.Boundary }, token).ConfigureAwait(false);
        if (selection.IsFailure)
        {
            if (!selection.Errors.Any(error => error is ProfilingQueryLimitError)) { return Result<OperationProfilingGroupPage>.Failure(selection); }

            return Result<OperationProfilingGroupPage>.Success(grouped.Value with { StatisticsLimitation = "Exact duration statistics exceed the analysis limit. Narrow the operation or time range." });
        }

        if (selection.Value.Records.Count > Math.Min(query.MaximumAnalysisCount, this.options.Queries.MaximumAnalysisRecords))
        { return Result<OperationProfilingGroupPage>.Success(grouped.Value with { StatisticsLimitation = "Exact duration statistics exceed the analysis limit. Narrow the operation or time range." }); }

        var names = query.GroupingDimensions.OrderBy(ProfilingKeyComparer.Canonicalize, StringComparer.Ordinal).ToArray();
        string Identity(string key, string kind, string method, IReadOnlyList<ProfilingDimension> dimensions) => JsonSerializer.Serialize(new
        {
            Key = ProfilingKeyComparer.Canonicalize(key), Kind = ProfilingKeyComparer.Canonicalize(kind),
            Method = query.GroupByHttpMethod ? ProfilingKeyComparer.Canonicalize(method) : null,
            Values = names.Select(name => dimensions.FirstOrDefault(dimension => ProfilingKeyComparer.Instance.Equals(dimension.Key, name))?.Value),
        });
        var indexed = selection.Value.Records.GroupBy(record => Identity(record.Key, record.Kind, record.Http?.Method, record.Dimensions), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var groups = new List<OperationProfilingGroup>(grouped.Value.Groups.Count);
        foreach (var group in grouped.Value.Groups)
        {
            token.ThrowIfCancellationRequested();
            var records = indexed.GetValueOrDefault(Identity(group.Key, group.Kind, group.HttpMethod, group.Dimensions)) ?? [];
            if (records.LongLength != group.Count) { return Result<OperationProfilingGroupPage>.Failure(new ProfilingQueryBoundaryError()); }

            var distribution = OperationProfilingAnalysis.Distribution(records.Select(record => record.Duration), query.MaximumAnalysisCount) with
            {
                PartialOperationCount = records.LongCount(record => record.Outcome == OperationProfilingOutcome.Incomplete
                    || record.Quality.PartialCoverage || record.Quality.Truncated || record.Quality.ActualCompletionUnobserved || record.Quality.IncompleteSegmentCount > 0),
            };
            groups.Add(group with { Distribution = distribution });
        }

        return Result<OperationProfilingGroupPage>.Success(grouped.Value with { Groups = Array.AsReadOnly(groups.ToArray()) });
    }
}
