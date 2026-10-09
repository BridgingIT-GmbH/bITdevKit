// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Computes exact bounded distributions from retained roots and aggregate segment summaries.</summary>
/// <example><code>var analysis = OperationProfilingAnalysis.Create(selection, query, token);</code></example>
public static class OperationProfilingAnalysis
{
    /// <summary>Computes root observations, compatible reductions and per-owner segment distributions without extrapolation.</summary>
    /// <example><code>var median = OperationProfilingAnalysis.Create(selection, query).P50Milliseconds;</code></example>
    public static OperationProfilingDistribution Create(OperationProfilingAnalysisSelection selection, OperationProfilingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(query);
        var records = selection.Records;
        if (records.Count > query.MaximumAnalysisCount) { throw new ArgumentException("The exact selection exceeds its analysis bound.", nameof(selection)); }

        var paths = new Dictionary<(string Kind, string Key, string Path), List<SegmentOwner>>();
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var segment in record.Segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!OperationProfilingQueryValidator.MatchesSegment(segment, query)) { continue; }

                var statistics = SelectStatistics(segment, query.SegmentOutcomes);
                if (statistics.Count == 0 && !statistics.Unavailable) { continue; }

                var key = (ProfilingKeyComparer.Canonicalize(record.Kind), ProfilingKeyComparer.Canonicalize(record.Key), segment.Path.ComparisonKey);
                if (!paths.TryGetValue(key, out var owners)) { paths.Add(key, owners = []); }

                owners.Add(new(record, segment, statistics));
            }
        }

        var segments = paths.OrderBy(pair => pair.Key.Kind, StringComparer.Ordinal).ThenBy(pair => pair.Key.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Path, StringComparer.Ordinal).Select(pair => AnalyzeSegment(pair.Value, query.SegmentOutcomes, query.MaximumAnalysisCount, cancellationToken)).ToArray();
        return Distribution(records.Select(record => record.Duration), query.MaximumAnalysisCount) with
        {
            Boundary = selection.Boundary, Segments = Array.AsReadOnly(segments),
            OutcomeFilter = Array.AsReadOnly(query.Outcomes.ToArray()), SegmentOutcomeFilter = Array.AsReadOnly(query.SegmentOutcomes.ToArray()),
            Outcomes = records.GroupBy(record => record.Outcome).ToDictionary(group => group.Key, group => group.LongCount()),
            PartialOperationCount = records.LongCount(record => record.Outcome == OperationProfilingOutcome.Incomplete
                || record.Quality.PartialCoverage || record.Quality.Truncated || record.Quality.ActualCompletionUnobserved || record.Quality.IncompleteSegmentCount > 0),
            Dimensions = ReduceDimensions(records.Select(record => record.Dimensions.Select(dimension => new ProfilingSegmentDimensionSummary
            {
                Key = dimension.Key, Value = dimension.Value, SampleCount = 1,
            }).ToArray()).ToArray()),
            Measurements = ReduceMeasurements(records.SelectMany(record => record.Measurements.Select(measurement => new MeasurementOwner(record.Id, new()
            {
                Key = measurement.Key, Unit = measurement.Unit, Aggregation = measurement.Aggregation, Value = measurement.Value,
                Sum = measurement.Aggregation == MeasurementAggregation.Average ? measurement.Value : null,
                SampleCount = 1, LastCompletedUtc = record.CompletedUtc,
            }))).ToArray(), cancellationToken),
            Sampling = Array.AsReadOnly(records.GroupBy(record => (record.Http?.SamplingStrategyKey, record.Http?.SamplingConfigurationKey))
                .Select(group => new OperationProfilingSamplingSummary
                {
                    StrategyKey = group.Key.SamplingStrategyKey, ConfigurationKey = group.Key.SamplingConfigurationKey, Count = group.LongCount(),
                    MinimumInclusionProbability = group.Min(record => record.Http?.SamplingInclusionProbability),
                    MaximumInclusionProbability = group.Max(record => record.Http?.SamplingInclusionProbability),
                    ProbabilityUnavailable = group.Any(record => record.Http?.SamplingInclusionProbability is null),
                }).OrderBy(policy => policy.StrategyKey, StringComparer.Ordinal).ThenBy(policy => policy.ConfigurationKey, StringComparer.Ordinal).ToArray()),
        };
    }

    /// <summary>Computes midpoint median and nearest-rank percentiles from individual retained observations.</summary>
    /// <example><code>var median = OperationProfilingAnalysis.Distribution([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20)]).P50Milliseconds;</code></example>
    public static OperationProfilingDistribution Distribution(IEnumerable<TimeSpan> observations, int maximumCount = 10000)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (maximumCount <= 0) { throw new ArgumentOutOfRangeException(nameof(maximumCount)); }

        var ticks = observations.Take(maximumCount + 1).Select(value => value.Ticks).Order().ToArray();
        if (ticks.Length > maximumCount) { throw new ArgumentException("The distribution exceeds its exact selection bound.", nameof(observations)); }

        if (ticks.Length == 0) { return new() { DurationUnavailable = true }; }

        decimal total = 0;
        foreach (var value in ticks) { total += value; }

        var middle = ticks.Length / 2;
        var median = ticks.Length % 2 == 0 ? ((decimal)ticks[middle - 1] + ticks[middle]) / 2 : ticks[middle];
        return new()
        {
            Count = ticks.LongLength, MinimumMilliseconds = Milliseconds(ticks[0]), MaximumMilliseconds = Milliseconds(ticks[^1]),
            MeanMilliseconds = Milliseconds(total / ticks.Length), P50Milliseconds = Milliseconds(median),
            P95Milliseconds = Milliseconds(ticks[(int)Math.Ceiling(ticks.Length * .95) - 1]),
            P99Milliseconds = Milliseconds(ticks[(int)Math.Ceiling(ticks.Length * .99) - 1]),
        };
    }

    private static OperationProfilingSegmentAnalysis AnalyzeSegment(List<SegmentOwner> owners, IReadOnlyList<ProfilingSegmentOutcome> selectedOutcomes, int maximumCount, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var label = owners.OrderBy(owner => owner.Record.StartedUtc).ThenBy(owner => owner.Record.Id.ToString("N"), StringComparer.Ordinal).First();
        decimal count = 0, duration = 0, self = 0;
        foreach (var owner in owners)
        {
            count += owner.Statistics.Count;
            duration += owner.Statistics.TotalDuration.Ticks;
            self += owner.Statistics.TotalSelfDuration.Ticks;
        }

        var unavailable = count > long.MaxValue || owners.Any(owner => owner.Statistics.Unavailable);
        var outcomes = new Dictionary<ProfilingSegmentOutcome, long>();
        foreach (var group in owners.SelectMany(owner => owner.Summary.Outcomes)
            .Where(outcome => selectedOutcomes.Count == 0 || selectedOutcomes.Contains(outcome.Outcome)).GroupBy(outcome => outcome.Outcome))
        {
            decimal value = 0;
            foreach (var outcome in group) { value += outcome.Statistics.Count; }

            if (value > long.MaxValue) { unavailable = true; outcomes.Add(group.Key, 0); }
            else { outcomes.Add(group.Key, (long)value); }
        }

        return new()
        {
            OperationKey = label.Record.Key, Kind = label.Record.Kind, Path = label.Summary.Path,
            OperationCount = owners.Count, InvocationCount = unavailable ? 0 : (long)count, Unavailable = unavailable,
            PartialCoverage = owners.Any(owner => owner.Summary.PartialCoverage),
            InvocationMeanMilliseconds = unavailable || count == 0 ? 0 : Milliseconds(duration / count),
            InvocationSelfMeanMilliseconds = unavailable || count == 0 ? 0 : Milliseconds(self / count),
            MinimumInvocationMilliseconds = unavailable ? 0 : owners.Min(owner => owner.Statistics.MinimumDuration.TotalMilliseconds),
            MaximumInvocationMilliseconds = unavailable ? 0 : owners.Max(owner => owner.Statistics.MaximumDuration.TotalMilliseconds),
            TotalPerOperation = Distribution(owners.Select(owner => owner.Statistics.TotalDuration), maximumCount) with { DurationUnavailable = unavailable },
            SelfPerOperation = Distribution(owners.Select(owner => owner.Statistics.TotalSelfDuration), maximumCount) with { DurationUnavailable = unavailable },
            Outcomes = outcomes, Dimensions = ReduceDimensions(owners.Select(owner => owner.Summary.Dimensions).ToArray()),
            Measurements = ReduceMeasurements(owners.SelectMany(owner => owner.Summary.Measurements.Select(measurement =>
                new MeasurementOwner(owner.Record.Id, SelectMeasurement(measurement, selectedOutcomes)))).ToArray(), token),
        };
    }

    private static ProfilingDurationStatistics SelectStatistics(ProfilingSegmentSummary summary, IReadOnlyList<ProfilingSegmentOutcome> selected)
    {
        if (selected.Count == 0) { return summary.Statistics; }

        var values = summary.Outcomes.Where(outcome => selected.Contains(outcome.Outcome)).Select(outcome => outcome.Statistics).ToArray();
        try
        {
            return new()
            {
                Count = values.Sum(value => value.Count), TotalDuration = TimeSpan.FromTicks(values.Sum(value => value.TotalDuration.Ticks)),
                TotalSelfDuration = TimeSpan.FromTicks(values.Sum(value => value.TotalSelfDuration.Ticks)),
                MinimumDuration = values.Length == 0 ? TimeSpan.Zero : values.Min(value => value.MinimumDuration),
                MaximumDuration = values.Length == 0 ? TimeSpan.Zero : values.Max(value => value.MaximumDuration),
                Unavailable = values.Any(value => value.Unavailable),
            };
        }
        catch (OverflowException) { return new() { Unavailable = true }; }
    }

    private static IReadOnlyList<ProfilingSegmentDimensionSummary> ReduceDimensions(IReadOnlyList<IReadOnlyList<ProfilingSegmentDimensionSummary>> owners)
    {
        return Array.AsReadOnly(owners.SelectMany(owner => owner).GroupBy(dimension => dimension.Key, ProfilingKeyComparer.Instance)
            .OrderBy(group => ProfilingKeyComparer.Canonicalize(group.Key), StringComparer.Ordinal).Select(group =>
            {
                var values = group.ToArray();
                var mixed = values.Any(value => value.Mixed) || values.Select(value => value.Value).Distinct().Count() != 1;
                decimal samples = 0;
                foreach (var value in values) { samples += value.SampleCount; }

                return new ProfilingSegmentDimensionSummary
                {
                    Key = group.Key, Value = mixed ? null : values[0].Value, Mixed = mixed,
                    Partial = values.Length < owners.Count || values.Any(value => value.Partial) || samples > long.MaxValue,
                    SampleCount = samples > long.MaxValue ? 0 : (long)samples,
                };
            }).ToArray());
    }

    private static ProfilingMeasurementSummary SelectMeasurement(ProfilingMeasurementSummary measurement, IReadOnlyList<ProfilingSegmentOutcome> selected)
    {
        if (selected.Count == 0) { return measurement; }

        var selectedBuckets = measurement.Outcomes.Where(outcome => selected.Contains(outcome.Outcome)).ToArray();
        var reduced = ReduceMeasurements(selectedBuckets.Select(bucket => new MeasurementOwner(Guid.Empty, measurement with
        {
            Value = bucket.Value, Sum = bucket.Sum, SampleCount = bucket.SampleCount,
            LastCompletionSequence = bucket.LastCompletionSequence, LastCompletedUtc = bucket.LastCompletedUtc,
            Unavailable = measurement.Unavailable || bucket.Unavailable, Outcomes = [],
        })).ToArray(), default);
        return reduced.Count == 0 ? measurement with { SampleCount = 0, Value = null, Sum = null, Outcomes = [] } : reduced[0];
    }

    private static IReadOnlyList<ProfilingMeasurementSummary> ReduceMeasurements(IReadOnlyList<MeasurementOwner> owners, CancellationToken token)
    {
        var reduced = new List<ProfilingMeasurementSummary>();
        foreach (var group in owners.Where(owner => owner.Summary.SampleCount > 0 || owner.Summary.Unavailable || owner.Summary.Conflicting)
            .GroupBy(owner => (ProfilingKeyComparer.Canonicalize(owner.Summary.Key), owner.Summary.Unit, owner.Summary.Aggregation))
            .OrderBy(group => group.Key.Item1, StringComparer.Ordinal).ThenBy(group => group.Key.Unit, StringComparer.Ordinal).ThenBy(group => group.Key.Aggregation))
        {
            token.ThrowIfCancellationRequested();
            var rows = group.ToArray();
            var first = rows[0].Summary;
            var types = rows.Select(row => (row.Summary.Aggregation == MeasurementAggregation.Average ? row.Summary.Sum : row.Summary.Value)?.Type).Distinct().ToArray();
            var conflicting = rows.Any(row => row.Summary.Conflicting) || types.Length != 1;
            var unavailable = conflicting || rows.Any(row => row.Summary.Unavailable);
            long count = 0;
            ProfilingValue aggregate = null, sum = null;
            try
            {
                foreach (var row in rows) { count = checked(count + row.Summary.SampleCount); }

                if (!unavailable)
                {
                    if (first.Aggregation == MeasurementAggregation.Last)
                    {
                        aggregate = rows.OrderByDescending(row => row.Summary.LastCompletedUtc).ThenBy(row => row.Id.ToString("N"), StringComparer.Ordinal)
                            .ThenByDescending(row => row.Summary.LastCompletionSequence).First().Summary.Value;
                    }
                    else
                    {
                        foreach (var row in rows)
                        {
                            var value = first.Aggregation == MeasurementAggregation.Average ? row.Summary.Sum : row.Summary.Value;
                            if (value is null) { unavailable = true; break; }

                            aggregate = aggregate is null ? value : first.Aggregation switch
                            {
                                MeasurementAggregation.Min => ProfilingValueCodec.Compare(aggregate, value) <= 0 ? aggregate : value,
                                MeasurementAggregation.Max => ProfilingValueCodec.Compare(aggregate, value) >= 0 ? aggregate : value,
                                _ => ProfilingValueCodec.Add(aggregate, value),
                            };
                        }

                        if (first.Aggregation == MeasurementAggregation.Average && !unavailable)
                        {
                            sum = aggregate;
                            aggregate = count == 0 ? null : ProfilingValueCodec.Divide(sum, count);
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is OverflowException or ArgumentException or DivideByZeroException)
            {
                unavailable = true;
                count = 0;
            }

            var latest = rows.OrderByDescending(row => row.Summary.LastCompletedUtc).ThenBy(row => row.Id.ToString("N"), StringComparer.Ordinal)
                .ThenByDescending(row => row.Summary.LastCompletionSequence).First();
            reduced.Add(first with
            {
                Value = unavailable ? null : aggregate, Sum = unavailable ? null : sum, SampleCount = count,
                Conflicting = conflicting, Unavailable = unavailable,
                LastCompletedUtc = latest.Summary.LastCompletedUtc, LastCompletionSequence = latest.Summary.LastCompletionSequence, Outcomes = [],
            });
        }

        return Array.AsReadOnly(reduced.ToArray());
    }

    private static double Milliseconds(decimal ticks) => (double)(ticks / TimeSpan.TicksPerMillisecond);
    private sealed record SegmentOwner(OperationProfilingRecord Record, ProfilingSegmentSummary Summary, ProfilingDurationStatistics Statistics);
    private sealed record MeasurementOwner(Guid Id, ProfilingMeasurementSummary Summary);
}
