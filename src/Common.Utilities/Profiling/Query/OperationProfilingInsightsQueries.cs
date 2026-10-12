// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by the LICENSE file.

namespace BridgingIT.DevKit.Common;

public sealed partial class OperationProfilingQueryService
{
    private async Task<IResult<OperationProfilingInsights>> InsightsCoreAsync(OperationProfilingQuery query, CancellationToken token)
    {
        var valid = this.validator.Validate(query);
        if (valid.IsFailure) { return Result<OperationProfilingInsights>.Failure(valid); }

        var selection = await this.store.SelectAnalysisAsync(query, token).ConfigureAwait(false);
        if (selection.IsFailure) { return Result<OperationProfilingInsights>.Failure(selection); }

        if (selection.Value.Records.Count > Math.Min(query.MaximumAnalysisCount, this.options.Queries.MaximumAnalysisRecords))
        { return Result<OperationProfilingInsights>.Failure(new ProfilingQueryLimitError()); }

        return Result<OperationProfilingInsights>.Success(OperationProfilingInsightsAnalysis.Create(selection.Value, query, token));
    }
}

/// <summary>Builds operation and HTTP-method hotspots from exact retained records without reconstructing execution order.</summary>
/// <example><code>var insights = OperationProfilingInsightsAnalysis.Create(selection, query);</code></example>
public static class OperationProfilingInsightsAnalysis
{
    /// <summary>Computes outcome counts and completed latency and segment distributions under the supplied record bound.</summary>
    /// <example><code>var insights = OperationProfilingInsightsAnalysis.Create(selection, new() { Outcomes = [] }, token);</code></example>
    public static OperationProfilingInsights Create(OperationProfilingAnalysisSelection selection, OperationProfilingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(query);
        if (selection.Records.Count > query.MaximumAnalysisCount) { throw new ArgumentException("The exact selection exceeds its analysis bound.", nameof(selection)); }

        var groups = new List<OperationProfilingInsightGroup>();
        foreach (var group in selection.Records.GroupBy(record => (Kind: ProfilingKeyComparer.Canonicalize(record.Kind),
            Key: ProfilingKeyComparer.Canonicalize(record.Key), Method: ProfilingKeyComparer.Canonicalize(record.Http?.Method ?? ""))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var records = group.ToArray();
            var completed = records.Where(record => record.Outcome == OperationProfilingOutcome.Completed
                && !record.Quality.ActualCompletionUnobserved && record.Duration >= TimeSpan.Zero).ToArray();
            var analysis = OperationProfilingAnalysis.Create(new() { Records = completed, Boundary = selection.Boundary },
                query with { Outcomes = [OperationProfilingOutcome.Completed] }, cancellationToken);
            var representative = (completed.Length > 0 ? completed : records).OrderByDescending(record => record.Duration)
                .ThenBy(record => record.Id).First();
            groups.Add(new()
            {
                Key = representative.Key, Kind = representative.Kind, HttpMethod = representative.Http?.Method,
                Count = records.LongLength, Outcomes = Outcomes(records), CompletedLatency = analysis with { Segments = [], Dimensions = [], Measurements = [], Sampling = [] },
                CompletedElapsedMilliseconds = (double)(completed.Sum(record => (decimal)record.Duration.Ticks) / TimeSpan.TicksPerMillisecond),
                PartialCount = records.LongCount(IsPartial), RepresentativeId = representative.Id, Segments = analysis.Segments,
                SegmentFailureCount = records.LongCount(record => record.Segments.Any(segment => segment.Outcomes.Any(outcome => outcome.Outcome == ProfilingSegmentOutcome.Failed && outcome.Statistics.Count > 0))),
            });
        }

        return new()
        {
            Boundary = selection.Boundary, Count = selection.Records.Count, Outcomes = Outcomes(selection.Records),
            PartialCount = selection.Records.LongCount(IsPartial), Groups = groups.AsReadOnly(),
        };
    }

    private static IReadOnlyDictionary<OperationProfilingOutcome, long> Outcomes(IEnumerable<OperationProfilingRecord> records) =>
        records.GroupBy(record => record.Outcome).ToDictionary(group => group.Key, group => group.LongCount());

    private static bool IsPartial(OperationProfilingRecord record) => record.Outcome == OperationProfilingOutcome.Incomplete
        || record.Quality.PartialCoverage || record.Quality.Truncated || record.Quality.ActualCompletionUnobserved || record.Quality.IncompleteSegmentCount > 0;
}
