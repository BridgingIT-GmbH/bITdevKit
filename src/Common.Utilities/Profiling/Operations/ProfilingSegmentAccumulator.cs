// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class ProfilingSegmentAccumulator(Guid operationId, Guid nodeId, string key, string displayName,
    ProfilingSegmentPath path, ProfilingSegmentPath parentPath, OperationProfilingOptions options)
{
    private readonly DurationAccumulator statistics = new();
    private readonly Dictionary<ProfilingSegmentOutcome, DurationAccumulator> outcomes = [];
    private readonly Dictionary<string, DimensionAccumulator> dimensions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProfilingMeasurementAccumulator> measurements = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Source, string Code, string Type), ProfilingFailureSummary> failures = [];
    private long otherFailures;
    public string Key { get; } = key;
    public ProfilingSegmentPath Path { get; } = path;
    public bool Partial { get; set; }

    public void Add(ProfilingInvocation invocation, TimeSpan duration, TimeSpan self, ProfilingSegmentOutcome outcome,
        long sequence, DateTimeOffset utc, Func<long, bool> charge)
    {
        this.statistics.Add(duration, self);
        if (!this.outcomes.TryGetValue(outcome, out var bucket))
        {
            bucket = new DurationAccumulator();
            this.outcomes.Add(outcome, bucket);
        }

        bucket.Add(duration, self);
        foreach (var dimension in invocation.Dimensions)
        {
            if (!this.dimensions.TryGetValue(dimension.Key, out var accumulator))
            {
                if (this.dimensions.Count >= options.MaxMetadataEntries
                    || !charge(192 + ProfilingValueValidator.Charge(dimension.Value.Key) + ProfilingValueValidator.Charge(dimension.Value.Value)))
                {
                    this.Partial = true;
                    continue;
                }

                accumulator = new DimensionAccumulator(dimension.Value.Key, dimension.Value.Value);
                this.dimensions.Add(dimension.Key, accumulator);
            }

            accumulator.Add(dimension.Value.Value);
        }

        foreach (var measurement in invocation.Measurements)
        {
            if (!this.measurements.TryGetValue(measurement.Key, out var accumulator))
            {
                if (this.measurements.Count >= options.MaxMetadataEntries
                    || !charge(2048 + ProfilingValueValidator.Charge(measurement.Value.Key) + ProfilingValueValidator.Charge(measurement.Value.Unit)))
                {
                    this.Partial = true;
                    continue;
                }

                accumulator = new ProfilingMeasurementAccumulator(measurement.Value);
                this.measurements.Add(measurement.Key, accumulator);
            }

            accumulator.Add(measurement.Value, outcome, sequence, utc);
        }

        if (outcome == ProfilingSegmentOutcome.Failed && invocation.Failure is { } failure)
        {
            var category = (failure.Source, failure.Code, failure.ExceptionType);
            if (this.failures.TryGetValue(category, out var existing))
            {
                this.failures[category] = existing with { Count = existing.Count + 1, LastObservedUtc = utc };
            }
            else if (this.failures.Count < options.MaxFailureCategories
                && charge(256 + ProfilingValueValidator.Charge(failure.Source) + ProfilingValueValidator.Charge(failure.Code)
                    + ProfilingValueValidator.Charge(failure.ExceptionType) + ProfilingValueValidator.Charge(failure.Message)))
            {
                this.failures.Add(category, new ProfilingFailureSummary { Failure = failure, Count = 1, FirstObservedUtc = utc, LastObservedUtc = utc });
            }
            else
            {
                this.otherFailures++;
            }
        }
        else if (outcome == ProfilingSegmentOutcome.Failed)
        {
            this.otherFailures++;
            this.Partial = true;
        }
    }

    public ProfilingSegmentSummary Freeze() => new()
    {
        OperationId = operationId,
        NodeId = nodeId,
        Key = this.Key,
        DisplayName = displayName,
        Path = this.Path,
        ParentPath = parentPath,
        Statistics = this.statistics.Freeze(),
        Outcomes = Array.AsReadOnly(this.outcomes.Select(p => new ProfilingSegmentOutcomeSummary { Outcome = p.Key, Statistics = p.Value.Freeze() }).ToArray()),
        Dimensions = Array.AsReadOnly(this.dimensions.Values.Select(d => d.Freeze(this.statistics.Count)).ToArray()),
        Measurements = Array.AsReadOnly(this.measurements.Values.Select(m => m.Freeze()).ToArray()),
        Failures = Array.AsReadOnly(this.failures.Values.ToArray()),
        OtherFailureCount = this.otherFailures,
        PartialCoverage = this.Partial || this.statistics.Unavailable,
    };

    private sealed class DimensionAccumulator(string key, ProfilingValue initial)
    {
        private long count;
        private bool mixed;
        public void Add(ProfilingValue value) { this.count++; this.mixed |= value != initial; }
        public ProfilingSegmentDimensionSummary Freeze(long total) => new()
        {
            Key = key, Value = this.mixed ? null : initial, SampleCount = this.count, Mixed = this.mixed, Partial = this.count != total,
        };
    }

    private sealed class DurationAccumulator
    {
        private long total;
        private long self;
        private long minimum = long.MaxValue;
        private long maximum;
        public long Count { get; private set; }
        public bool Unavailable { get; private set; }

        public void Add(TimeSpan duration, TimeSpan selfDuration)
        {
            this.Count++;
            this.minimum = Math.Min(this.minimum, duration.Ticks);
            this.maximum = Math.Max(this.maximum, duration.Ticks);
            try
            {
                this.total = checked(this.total + duration.Ticks);
                this.self = checked(this.self + selfDuration.Ticks);
            }
            catch (OverflowException)
            {
                this.Unavailable = true;
            }
        }

        public ProfilingDurationStatistics Freeze() => new()
        {
            Count = this.Count, TotalDuration = TimeSpan.FromTicks(this.total), TotalSelfDuration = TimeSpan.FromTicks(this.self),
            MinimumDuration = this.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(this.minimum), MaximumDuration = TimeSpan.FromTicks(this.maximum), Unavailable = this.Unavailable,
        };
    }
}

internal sealed class ProfilingMeasurementAccumulator(ProfilingMeasurement definition)
{
    private readonly Reduction all = new(definition);
    private readonly Dictionary<ProfilingSegmentOutcome, Reduction> outcomes = [];
    private bool conflicting;

    public void Add(ProfilingMeasurement measurement, ProfilingSegmentOutcome outcome, long sequence, DateTimeOffset utc)
    {
        if (!this.outcomes.TryGetValue(outcome, out var bucket))
        {
            bucket = new Reduction(definition);
            this.outcomes.Add(outcome, bucket);
        }

        var compatible = measurement.Unit == definition.Unit && measurement.Aggregation == definition.Aggregation && measurement.Value.Type == definition.Value.Type;
        this.conflicting |= !compatible;
        this.all.Add(measurement.Value, sequence, utc, compatible);
        bucket.Add(measurement.Value, sequence, utc, compatible);
    }

    public ProfilingMeasurementSummary Freeze() => new()
    {
        Key = definition.Key,
        Unit = definition.Unit,
        Aggregation = definition.Aggregation,
        SampleCount = this.all.Count,
        Value = this.all.Value(),
        Sum = this.all.SumValue(),
        LastCompletionSequence = this.all.Sequence,
        LastCompletedUtc = this.all.Utc,
        Conflicting = this.conflicting,
        Unavailable = this.all.Unavailable,
        Outcomes = Array.AsReadOnly(this.outcomes.Select(p => new ProfilingOutcomeMeasurementSummary
        {
            Outcome = p.Key, SampleCount = p.Value.Count, Value = p.Value.Value(), Sum = p.Value.SumValue(), Unavailable = p.Value.Unavailable,
            LastCompletionSequence = p.Value.Sequence, LastCompletedUtc = p.Value.Utc,
        }).ToArray()),
    };

    private sealed class Reduction(ProfilingMeasurement definition)
    {
        private ProfilingValue selected;
        private ProfilingValue sum;
        public long Count { get; private set; }
        public long Sequence { get; private set; }
        public DateTimeOffset Utc { get; private set; }
        public bool Unavailable { get; private set; }

        public void Add(ProfilingValue value, long sequence, DateTimeOffset utc, bool compatible)
        {
            this.Count++;
            this.Unavailable |= !compatible;
            this.Sequence = sequence;
            this.Utc = utc;
            if (this.Unavailable)
            {
                return;
            }

            try
            {
                if (definition.Aggregation is MeasurementAggregation.Sum or MeasurementAggregation.Average)
                {
                    this.sum = this.sum is null ? value : ProfilingValueCodec.Add(this.sum, value);
                }

                this.selected = definition.Aggregation switch
                {
                    MeasurementAggregation.Min when this.selected is not null => ProfilingValueCodec.Compare(this.selected, value) <= 0 ? this.selected : value,
                    MeasurementAggregation.Max when this.selected is not null => ProfilingValueCodec.Compare(this.selected, value) >= 0 ? this.selected : value,
                    _ => value,
                };
            }
            catch (Exception exception) when (exception is OverflowException or ArgumentException)
            {
                this.Unavailable = true;
                this.sum = null;
                this.selected = null;
            }
        }

        public ProfilingValue Value() => this.Unavailable ? null : definition.Aggregation switch
        {
            MeasurementAggregation.Sum => this.sum,
            MeasurementAggregation.Average => this.sum is null ? null : ProfilingValueCodec.Divide(this.sum, this.Count),
            _ => this.selected,
        };
        public ProfilingValue SumValue() => this.Unavailable ? null : this.sum;
    }
}
