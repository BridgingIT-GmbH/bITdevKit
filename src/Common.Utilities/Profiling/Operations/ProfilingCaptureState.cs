// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class ProfilingCaptureState
{
    private readonly object sync = new();
    private readonly OperationProfiler profiler;
    private readonly OperationProfilingOptions options;
    private readonly OperationProfilingStartRequest request;
    private readonly ProfilingNode node;
    private readonly long started;
    private readonly DateTimeOffset startedUtc;
    private readonly string displayName;
    private readonly int entryCount;
    private Dictionary<string, ProfilingDimension> dimensions = new(StringComparer.Ordinal);
    private Dictionary<string, ProfilingMeasurement> measurements = new(StringComparer.Ordinal);
    private Dictionary<string, ProfilingAdapterMetadata> sources = new(StringComparer.Ordinal);
    private Dictionary<string, ProfilingSegmentAccumulator> summaries = new(StringComparer.Ordinal);
    private HashSet<ProfilingInvocation> invocations = [];
    private HttpRequestProfilingMetadata http;
    private ProfilingFailureDescriptor failure;
    private OperationProfilingOutcome? outcome;
    private string key;
    private bool closed;
    private bool truncated;
    private bool partial;
    private bool classificationFailed;
    private long rejectedMetadata;
    private long rejectedSegments;
    private long incompleteSegments;
    private long completionSequence;
    private long invocationSequence;
    private ProfilingWallTimeAccumulator wall;

    public ProfilingCaptureState(OperationProfiler profiler, OperationProfilingOptions options, Guid id,
        OperationProfilingStartRequest request, ProfilingNode node, long started, DateTimeOffset startedUtc,
        string displayName, long charge, int entryCount, bool truncated)
    {
        this.profiler = profiler;
        this.options = options;
        this.Id = id;
        this.request = request;
        this.key = request.Key;
        this.node = node;
        this.started = started;
        this.startedUtc = startedUtc;
        this.displayName = displayName;
        this.ChargedBytes = charge;
        this.entryCount = entryCount;
        this.truncated = truncated;
        this.wall = new ProfilingWallTimeAccumulator(started, profiler.Clock);
    }

    public Guid Id { get; }
    public long ChargedBytes { get; set; }
    public Guid? RuntimeSessionId { get; set; }
    public string RuntimeSessionKey { get; set; }
    public IProfilingOperationScope Operation { get; set; }
    public bool IsOpen { get { lock (this.sync) { return !this.closed; } } }

    public void Observe(Action observation)
    {
        lock (this.sync)
        {
            if (this.closed)
            {
                Interlocked.Increment(ref this.profiler.Counters.LateObservations);
                return;
            }

            try
            {
                var now = this.profiler.Clock.GetTimestamp();
                if (this.profiler.Clock.GetElapsedTime(this.started, now) >= this.options.MaxRecordingDuration)
                {
                    this.FinishCore(ProfilingIncompleteReason.CaptureDeadlineExceeded, this.DeadlineTimestamp());
                    Interlocked.Increment(ref this.profiler.Counters.Expired);
                    return;
                }

                observation();
            }
            catch (Exception)
            {
                // Discard inconsistent state while still holding its lock. A parallel finalizer
                // must never publish a partially updated summary or retain its admission budget.
                Interlocked.Increment(ref this.profiler.Counters.CaptureFaults);
                Interlocked.Increment(ref this.profiler.Counters.DiscardedCaptures);
                this.profiler.Release(this);
                this.Abandon();
            }
        }
    }

    public void SetKey(string value) => this.Observe(() =>
    {
        if (!ProfilingValueValidator.IsKey(value, this.options.MaxKeyLength)
            || !this.Charge(ProfilingValueValidator.Charge(value) - ProfilingValueValidator.Charge(this.key)))
        {
            this.RejectMetadata();
            return;
        }

        this.key = value;
    });

    public void SetDimension(string name, object value, ProfilingInvocation invocation = null) => this.Observe(() =>
    {
        if (invocation?.Closed == true)
        {
            return;
        }

        var target = invocation?.Dimensions ?? this.dimensions;
        if (!ProfilingValueValidator.IsKey(name, this.options.MaxKeyLength)
            || !ProfilingValueValidator.TryValue(value, this.options, out var scalar))
        {
            this.RejectMetadata();
            return;
        }

        var canonical = ProfilingKeyComparer.Canonicalize(name);
        target.TryGetValue(canonical, out var previous);
        var delta = previous is null ? 128 + ProfilingValueValidator.Charge(name) + ProfilingValueValidator.Charge(scalar)
            : ProfilingValueValidator.Charge(scalar) - ProfilingValueValidator.Charge(previous.Value);
        if (previous is null && target.Count >= this.options.MaxMetadataEntries || !this.Charge(delta))
        {
            this.RejectMetadata();
            return;
        }

        target[canonical] = new ProfilingDimension { Key = previous?.Key ?? name, Value = scalar };
        if (invocation is not null)
        {
            invocation.ChargedBytes += delta;
        }
    });

    public void SetMeasurement(string name, object value, string unit, ProfilingInvocation invocation = null,
        MeasurementAggregation aggregation = MeasurementAggregation.Sum) => this.Observe(() =>
    {
        if (invocation?.Closed == true)
        {
            return;
        }

        if (!ProfilingValueValidator.IsKey(name, this.options.MaxKeyLength)
            || !ProfilingValueValidator.IsKey(unit, this.options.MaxUnitLength)
            || !Enum.IsDefined(aggregation) || !ProfilingValueValidator.TryNumber(value, this.options, out var scalar))
        {
            this.RejectMetadata();
            return;
        }

        var canonical = ProfilingKeyComparer.Canonicalize(name);
        var target = invocation?.Measurements ?? this.measurements;
        target.TryGetValue(canonical, out var previous);
        var delta = previous is null ? 160 + ProfilingValueValidator.Charge(name) + ProfilingValueValidator.Charge(unit) + ProfilingValueValidator.Charge(scalar)
            : ProfilingValueValidator.Charge(unit) + ProfilingValueValidator.Charge(scalar) - ProfilingValueValidator.Charge(previous.Unit) - ProfilingValueValidator.Charge(previous.Value);
        if (previous is null && target.Count >= this.options.MaxMetadataEntries || !this.Charge(delta))
        {
            this.RejectMetadata();
            return;
        }

        target[canonical] = new ProfilingMeasurement { Key = previous?.Key ?? name, Value = scalar, Unit = unit, Aggregation = aggregation };
        if (invocation is not null)
        {
            invocation.Aggregations[canonical] = aggregation;
            invocation.ChargedBytes += delta;
        }
    });

    public void SetSource(ProfilingAdapterMetadata source) => this.Observe(() =>
    {
        if (source is null || !ProfilingValueValidator.IsKey(source.Kind, this.options.MaxKeyLength)
            || source.SchemaVersion <= 0 || source.Fields is null || source.Fields.Count > this.options.MaxMetadataEntries)
        {
            this.RejectMetadata();
            return;
        }

        var fieldCount = source.Fields.Count;
        if (fieldCount > this.options.MaxMetadataEntries)
        {
            this.RejectMetadata();
            return;
        }

        var fields = new ProfilingDimension[fieldCount];
        for (var index = 0; index < fields.Length; index++)
        {
            fields[index] = source.Fields[index];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field is null || !ProfilingValueValidator.IsKey(field.Key, this.options.MaxKeyLength)
                || field.Value is null || field.Value.Type == ProfilingValueType.String && (field.Value.Scalar.Length > this.options.MaxStringLength || !ProfilingValueValidator.IsUnicode(field.Value.Scalar))
                || !names.Add(ProfilingKeyComparer.Canonicalize(field.Key)))
            {
                this.RejectMetadata();
                return;
            }
        }

        var canonical = ProfilingKeyComparer.Canonicalize(source.Kind);
        this.sources.TryGetValue(canonical, out var previous);
        var delta = SourceCharge(source.Kind, fields) - (previous is null ? 0 : SourceCharge(previous.Kind, previous.Fields));
        if (previous is null && this.sources.Count >= this.options.MaxMetadataEntries || !this.Charge(delta))
        {
            this.RejectMetadata();
            return;
        }

        this.sources[canonical] = source with { Fields = Array.AsReadOnly(fields) };
    });

    public void SetHttp(HttpRequestProfilingMetadata metadata) => this.Observe(() =>
    {
        if (metadata is null || metadata.RequestBytes < 0 || metadata.ResponseBytes < 0
            || metadata.DeclaredRequestBytes < 0 || metadata.DeclaredResponseBytes < 0
            || metadata.ActiveSelectedRequestsAtEntry < 1 || metadata.SamplingInclusionProbability is { } probability && (!double.IsFinite(probability) || probability < 0 || probability > 1)
            || metadata.StatusCode is < 100 or > 599 || !Enum.IsDefined(metadata.RequestBytesQuality) || !Enum.IsDefined(metadata.ResponseBytesQuality))
        {
            this.RejectMetadata();
            return;
        }

        var method = ProfilingValueValidator.Clip(metadata.Method, this.options.MaxKeyLength, out var m);
        var path = ProfilingValueValidator.Clip(metadata.Path, this.options.MaxStringLength, out var p);
        var route = ProfilingValueValidator.Clip(metadata.Route, this.options.MaxStringLength, out var r);
        var applicationRequestId = ProfilingValueValidator.Clip(metadata.ApplicationRequestId, this.options.MaxStringLength, out var a);
        if (metadata.SamplingStrategyKey is not null && !ProfilingValueValidator.IsKey(metadata.SamplingStrategyKey, this.options.MaxKeyLength)
            || metadata.SamplingConfigurationKey is not null && !ProfilingValueValidator.IsKey(metadata.SamplingConfigurationKey, this.options.MaxKeyLength))
        {
            this.RejectMetadata();
            return;
        }

        var normalized = metadata with { Method = method, Path = path, Route = route, ApplicationRequestId = applicationRequestId };
        var delta = HttpCharge(normalized) - (this.http is null ? 0 : HttpCharge(this.http));
        if (!this.Charge(delta))
        {
            this.RejectMetadata();
            return;
        }

        this.truncated |= m || p || r || a;
        this.http = normalized;
    });

    public void Declare(OperationProfilingOutcome value, ProfilingFailureDescriptor descriptor = null, ProfilingInvocation invocation = null,
        bool classifierFailed = false) => this.Observe(() =>
    {
        if (invocation?.Closed == true)
        {
            return;
        }

        this.classificationFailed |= classifierFailed;
        if (invocation is null)
        {
            if (this.outcome is null || Rank(value) > Rank(this.outcome.Value))
            {
                this.outcome = value;
            }

            if (value == OperationProfilingOutcome.Failed && this.failure is null)
            {
                this.failure = this.SafeFailure(descriptor);
            }
        }
        else
        {
            if (invocation.Outcome is null || Rank(value) > Rank(invocation.Outcome.Value))
            {
                invocation.Outcome = value;
            }

            if (value == OperationProfilingOutcome.Failed && invocation.Failure is null)
            {
                invocation.Failure = this.SafeFailure(descriptor);
                invocation.ChargedBytes += FailureCharge(invocation.Failure);
            }
        }
    });

    public void MarkClassificationFailed() => this.Observe(() => this.classificationFailed = true);

    public ProfilingInvocation StartSegment(ProfilingInvocation parent, string name, string display)
    {
        if (parent?.Closed == true || !ProfilingValueValidator.IsKey(name, this.options.MaxKeyLength)
            || this.invocations.Count >= this.options.MaxLiveSegments)
        {
            this.RejectSegment(parent);
            return null;
        }

        var components = parent is null ? new[] { name } : parent.Summary.Path.Components.Append(name).ToArray();
        if (components.Length > this.options.MaxSegmentDepth)
        {
            this.RejectSegment(parent);
            return null;
        }

        var path = new ProfilingSegmentPath(components);
        var canonical = path.ComparisonKey;
        this.summaries.TryGetValue(canonical, out var summary);
        var label = ProfilingValueValidator.Clip(display, this.options.MaxStringLength, out var clipped);
        var summaryCharge = summary is null ? 512 + components.Sum(ProfilingValueValidator.Charge) + ProfilingValueValidator.Charge(label) : 0;
        if (summary is null && this.summaries.Count >= this.options.MaxSegmentPaths || !this.Charge(192 + summaryCharge))
        {
            this.RejectSegment(parent);
            return null;
        }

        if (summary is null)
        {
            summary = new ProfilingSegmentAccumulator(this.Id, this.node.Identity.Id, name, label, path, parent?.Summary.Path, this.options);
            this.summaries.Add(canonical, summary);
        }

        this.truncated |= clipped;
        var now = this.profiler.Clock.GetTimestamp();
        var invocation = new ProfilingInvocation(summary, parent, now, ++this.invocationSequence);
        this.invocations.Add(invocation);
        if (parent is null)
        {
            this.wall.Start(invocation, now);
        }
        else
        {
            parent.StartChild(now);
        }

        this.profiler.Lifecycle.StartSegment(this.Id, summary.Path, invocation.Sequence);
        return invocation;
    }

    public void CloseSegment(ProfilingInvocation invocation)
    {
        this.Observe(() =>
        {
            if (invocation is not null && !invocation.Closed)
            {
                this.CloseSegmentCore(invocation, this.profiler.Clock.GetTimestamp(), this.profiler.Clock.GetUtcNow(), false);
            }
        });
    }

    public void Finish(ProfilingIncompleteReason forced = ProfilingIncompleteReason.None)
    {
        try
        {
            lock (this.sync)
            {
                if (this.closed)
                {
                    return;
                }

                var now = this.profiler.Clock.GetTimestamp();
                if (this.profiler.Clock.GetElapsedTime(this.started, now) >= this.options.MaxRecordingDuration)
                {
                    forced = ProfilingIncompleteReason.CaptureDeadlineExceeded;
                    now = this.DeadlineTimestamp();
                    Interlocked.Increment(ref this.profiler.Counters.Expired);
                }

                this.FinishCore(forced, now);
            }
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.profiler.Counters.CaptureFaults);
            Interlocked.Increment(ref this.profiler.Counters.DiscardedCaptures);
            this.profiler.Release(this);
            this.Abandon();
        }
    }

    public void Abandon()
    {
        lock (this.sync)
        {
            this.closed = true;
            if (this.invocations is not null)
            {
                foreach (var invocation in this.invocations)
                {
                    invocation.Closed = true;
                    invocation.Release();
                }
            }

            this.dimensions = null;
            this.measurements = null;
            this.sources = null;
            this.summaries = null;
            this.invocations = null;
            this.wall = null;
            this.http = null;
            this.failure = null;
        }
    }

    private void FinishCore(ProfilingIncompleteReason forced, long ended)
    {
        this.closed = true;
        try
        {
            var duration = this.profiler.Clock.GetElapsedTime(this.started, ended);
            var completedUtc = forced == ProfilingIncompleteReason.CaptureDeadlineExceeded
                ? this.startedUtc.Add(duration) : this.profiler.Clock.GetUtcNow();
            foreach (var invocation in this.invocations.Where(i => i.Parent is null).ToArray())
            {
                this.CloseSegmentCore(invocation, ended, completedUtc, true);
            }

            var reason = forced != ProfilingIncompleteReason.None ? forced
                : this.outcome is null ? this.classificationFailed ? ProfilingIncompleteReason.ClassificationFailed : ProfilingIncompleteReason.UnmarkedScope : ProfilingIncompleteReason.None;
            var segments = Array.AsReadOnly(this.summaries.Values.Select(s => s.Freeze()).ToArray());
            var record = new OperationProfilingRecord
            {
                Id = this.Id,
                Key = this.key,
                Kind = this.request.Kind,
                DisplayName = this.displayName,
                CorrelationId = this.request.CorrelationId,
                ParentOperationId = this.request.ParentOperationId,
                Node = this.node,
                StartedUtc = this.startedUtc,
                CompletedUtc = completedUtc,
                Duration = duration,
                StartedTimestamp = this.started,
                CompletedTimestamp = ended,
                TimestampFrequency = this.profiler.Clock.TimestampFrequency,
                Outcome = reason == ProfilingIncompleteReason.None ? this.outcome.Value : OperationProfilingOutcome.Incomplete,
                Failure = this.failure,
                ActiveRecordingOperationsAtEntry = this.entryCount,
                RuntimeSessionIdAtStart = this.RuntimeSessionId,
                RuntimeSessionKeyAtStart = this.RuntimeSessionKey,
                Dimensions = Array.AsReadOnly(this.dimensions.Values.ToArray()),
                Measurements = Array.AsReadOnly(this.measurements.Values.ToArray()),
                Sources = Array.AsReadOnly(this.sources.Values.ToArray()),
                Http = this.http,
                Segments = segments,
                WallTime = this.wall.Freeze(ended),
                Quality = new ProfilingCaptureQuality
                {
                    Truncated = this.truncated,
                    PartialCoverage = this.partial || this.incompleteSegments > 0 || reason != ProfilingIncompleteReason.None || segments.Any(s => s.PartialCoverage),
                    RejectedMetadataCount = this.rejectedMetadata,
                    RejectedSegmentCount = this.rejectedSegments,
                    IncompleteSegmentCount = this.incompleteSegments,
                    ClassificationFailed = this.classificationFailed,
                    IncompleteReason = reason,
                    ActualCompletionUnobserved = forced != ProfilingIncompleteReason.None,
                    ClockDiscontinuity = (completedUtc - this.startedUtc - duration).Duration() > TimeSpan.FromSeconds(1),
                },
                EstimatedPayloadBytes = this.ChargedBytes,
            };
            this.profiler.Lifecycle.StopOperation(record);
            this.profiler.Publish(record);
        }
        finally
        {
            this.profiler.Release(this);
            this.Abandon();
        }
    }

    private void CloseSegmentCore(ProfilingInvocation invocation, long ended, DateTimeOffset utc, bool forced)
    {
        if (invocation.Closed)
        {
            return;
        }

        foreach (var child in this.invocations.Where(i => ReferenceEquals(i.Parent, invocation)).ToArray())
        {
            this.CloseSegmentCore(child, ended, utc, true);
        }

        invocation.Closed = true;
        this.invocations.Remove(invocation);
        if (invocation.Parent is null)
        {
            this.wall.Stop(invocation, ended);
        }
        else
        {
            invocation.Parent.StopChild(ended);
        }

        var elapsed = this.profiler.Clock.GetElapsedTime(invocation.Started, ended);
        var covered = invocation.CoveredDuration(this.profiler.Clock, ended);
        var outcome = forced || invocation.Outcome is null ? ProfilingSegmentOutcome.Incomplete : invocation.Outcome.Value switch
        {
            OperationProfilingOutcome.Completed => ProfilingSegmentOutcome.Completed,
            OperationProfilingOutcome.Canceled => ProfilingSegmentOutcome.Canceled,
            OperationProfilingOutcome.Failed => ProfilingSegmentOutcome.Failed,
            _ => ProfilingSegmentOutcome.Incomplete,
        };
        if (outcome == ProfilingSegmentOutcome.Incomplete)
        {
            this.incompleteSegments++;
        }

        this.profiler.Lifecycle.StopSegment(this.Id, invocation.Summary.Path, invocation.Sequence, outcome, elapsed, invocation.Failure);
        try
        {
            invocation.Summary.Add(invocation, elapsed, elapsed > covered ? elapsed - covered : TimeSpan.Zero, outcome, ++this.completionSequence, utc, this.Charge);
        }
        finally
        {
            this.Charge(-invocation.ChargedBytes);
            invocation.Release();
        }
    }

    private bool Charge(long delta) => this.profiler.TryCharge(this, delta);
    private void RejectMetadata() { this.rejectedMetadata++; this.partial = true; }
    private void RejectSegment(ProfilingInvocation parent) { this.rejectedSegments++; this.partial = true; if (parent is not null) { parent.Summary.Partial = true; } }
    private long DeadlineTimestamp() => checked(this.started + (long)(this.options.MaxRecordingDuration.TotalSeconds * this.profiler.Clock.TimestampFrequency));
    private static int Rank(OperationProfilingOutcome value) => value switch { OperationProfilingOutcome.Aborted => 4, OperationProfilingOutcome.Failed => 3, OperationProfilingOutcome.Canceled => 2, _ => 1 };
    private static long HttpCharge(HttpRequestProfilingMetadata value) => 384 + ProfilingValueValidator.Charge(value.Method) + ProfilingValueValidator.Charge(value.Path) + ProfilingValueValidator.Charge(value.Route)
        + ProfilingValueValidator.Charge(value.ApplicationRequestId) + ProfilingValueValidator.Charge(value.SamplingStrategyKey) + ProfilingValueValidator.Charge(value.SamplingConfigurationKey);
    private static long SourceCharge(string kind, IReadOnlyList<ProfilingDimension> fields) => 192 + ProfilingValueValidator.Charge(kind) + fields.Sum(f => 128 + ProfilingValueValidator.Charge(f.Key) + ProfilingValueValidator.Charge(f.Value));
    private static long FailureCharge(ProfilingFailureDescriptor failure) => failure is null ? 0 : 192 + ProfilingValueValidator.Charge(failure.Source) + ProfilingValueValidator.Charge(failure.Code) + ProfilingValueValidator.Charge(failure.ExceptionType) + ProfilingValueValidator.Charge(failure.Message);

    private ProfilingFailureDescriptor SafeFailure(ProfilingFailureDescriptor descriptor)
    {
        descriptor ??= new ProfilingFailureDescriptor { Source = "Application", Code = "Failed" };
        var source = descriptor.Source ?? "Application";
        var code = descriptor.Code ?? "Failed";
        var type = descriptor.ExceptionType;
        if (!ProfilingValueValidator.IsKey(source, this.options.MaxKeyLength) || !ProfilingValueValidator.IsKey(code, this.options.MaxKeyLength)
            || type is not null && !ProfilingValueValidator.IsKey(type, this.options.MaxStringLength))
        {
            this.RejectMetadata();
            source = "Application";
            code = "InvalidDescriptor";
            type = null;
        }

        var message = ProfilingValueValidator.Clip(descriptor.Message, this.options.MaxFailureLength, out var m);
        this.truncated |= m;
        var safe = new ProfilingFailureDescriptor { Source = source, Code = code, ExceptionType = type, Message = message };
        if (!this.Charge(FailureCharge(safe)))
        {
            this.RejectMetadata();
            return null;
        }

        return safe;
    }
}

internal sealed class ProfilingInvocation(ProfilingSegmentAccumulator summary, ProfilingInvocation parent, long started, long sequence = 0)
{
    private int activeChildren;
    private long coverageStart;
    private long coverageTicks;
    public ProfilingSegmentAccumulator Summary { get; private set; } = summary;
    public ProfilingInvocation Parent { get; } = parent;
    public long Started { get; } = started;
    public long Sequence { get; } = sequence;
    public bool Closed { get; set; }
    public OperationProfilingOutcome? Outcome { get; set; }
    public ProfilingFailureDescriptor Failure { get; set; }
    public long ChargedBytes { get; set; } = 192;
    public Dictionary<string, ProfilingDimension> Dimensions { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ProfilingMeasurement> Measurements { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, MeasurementAggregation> Aggregations { get; private set; } = new(StringComparer.Ordinal);

    public void StartChild(long now)
    {
        if (this.activeChildren++ == 0)
        {
            this.coverageStart = now;
        }
    }

    public void StopChild(long now)
    {
        if (--this.activeChildren == 0)
        {
            this.coverageTicks += now - this.coverageStart;
        }
    }

    public TimeSpan CoveredDuration(TimeProvider clock, long ended) =>
        TimeSpan.FromSeconds((double)(this.coverageTicks + (this.activeChildren > 0 ? ended - this.coverageStart : 0)) / clock.TimestampFrequency);

    public void Release()
    {
        this.Dimensions = null;
        this.Measurements = null;
        this.Aggregations = null;
        this.Failure = null;
        this.Summary = null;
    }
}
