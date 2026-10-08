// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Logging;

internal sealed class OperationProfiler : IOperationProfiler
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, ProfilingCaptureState> active = [];
    private readonly OperationProfilingContext context = new();
    private readonly OperationProfilingOptions options;
    private readonly bool enabled;
    private readonly ProfilingNode node;
    private readonly IOperationProfilingCompletionSink sink;
    private readonly IProfilingSafeErrorPolicy errors;
    private readonly Func<RuntimeProfilingSession> runtimeHint;
    private long activeBytes;

    public OperationProfiler(
        ProfilingOptions options,
        IProfilingNodeIdentityProvider nodes,
        IOperationProfilingCompletionSink sink,
        TimeProvider timeProvider = null,
        IProfilingSafeErrorPolicy errors = null,
        Func<RuntimeProfilingSession> runtimeHint = null,
        ILogger<OperationProfiler> operationLogger = null,
        ILogger<ProfilingSegmentScope> segmentLogger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(sink);
        options.Validate();
        this.enabled = options.OperationEnabled;
        this.options = options.Operations.Snapshot();
        this.node = nodes.GetNode();
        this.sink = sink;
        this.Clock = timeProvider ?? TimeProvider.System;
        this.errors = errors;
        this.runtimeHint = runtimeHint;
        this.Lifecycle = new ProfilingLifecycleLogger(operationLogger, segmentLogger, this.Counters);
    }

    public TimeProvider Clock { get; }
    public ProfilingLifecycleLogger Lifecycle { get; }
    public OperationProfilingCaptureCounters Counters { get; } = new();
    public IProfilingOperationScope Current => this.context.Current?.Operation;
    public int ActiveCount { get { lock (this.sync) { return this.active.Count; } } }
    public long ActiveBytes { get { lock (this.sync) { return this.activeBytes; } } }

    public IProfilingOperationScope BeginOperation(string key, OperationProfilingKind kind = OperationProfilingKind.Custom) =>
        this.BeginOperation(new OperationProfilingStartRequest { Key = key, Kind = kind.ToString() });

    public IProfilingOperationScope BeginOperation(OperationProfilingStartRequest request)
    {
        var previous = this.context.Current;
        var suppressed = previous?.Suppressed == true;
        var id = this.enabled && !suppressed ? Guid.NewGuid() : Guid.Empty;
        ProfilingCaptureState capture = null;
        if (id != Guid.Empty)
        {
            Interlocked.Increment(ref this.Counters.Attempted);
            try
            {
                if (request is not null && ProfilingValueValidator.IsKey(request.Key, this.options.MaxKeyLength)
                    && ProfilingValueValidator.IsKey(request.Kind, this.options.MaxKeyLength)
                    && (request.CorrelationId is null || request.CorrelationId.Length <= this.options.MaxStringLength && ProfilingValueValidator.IsUnicode(request.CorrelationId)))
                {
                    var started = this.Clock.GetTimestamp();
                    var utc = this.Clock.GetUtcNow();
                    var display = ProfilingValueValidator.Clip(request.DisplayName, this.options.MaxStringLength, out var truncated);
                    var charge = 2048 + ProfilingValueValidator.Charge(request.Key) + ProfilingValueValidator.Charge(request.Kind)
                        + ProfilingValueValidator.Charge(display) + ProfilingValueValidator.Charge(request.CorrelationId);
                    lock (this.sync)
                    {
                        if (this.active.Count < this.options.MaxActiveOperations && charge <= this.options.MaxRecordBytes
                            && charge <= this.options.MaxActiveBytes - this.activeBytes)
                        {
                            capture = new ProfilingCaptureState(this, this.options, id, request with { DisplayName = display }, this.node, started, utc, display, charge, this.active.Count + 1, truncated);
                            this.active.Add(id, capture);
                            this.activeBytes += charge;
                        }
                    }

                    if (capture is not null)
                    {
                        try
                        {
                            var hint = this.runtimeHint?.Invoke();
                            capture.RuntimeSessionId = hint?.Identity.Id;
                            capture.RuntimeSessionKey = hint?.Identity.Key;
                        }
                        catch (Exception)
                        {
                            // Optional Runtime hints never determine operation capture availability.
                            Interlocked.Increment(ref this.Counters.CaptureFaults);
                        }

                        Interlocked.Increment(ref this.Counters.Admitted);
                    }
                }
            }
            catch (Exception)
            {
                if (capture is not null)
                {
                    this.Release(capture);
                    capture.Abandon();
                    capture = null;
                }

                Interlocked.Increment(ref this.Counters.CaptureFaults);
            }

            if (capture is null)
            {
                Interlocked.Increment(ref this.Counters.Rejected);
            }
        }

        var scope = new ProfilingOperationScope(this, capture, id, previous);
        if (capture is not null)
        {
            capture.Operation = scope;
        }

        var frame = new OperationProfilingFrame(scope, capture, null, suppressed);
        scope.Restoration = this.context.Enter(frame);
        if (capture is not null)
        {
            this.Lifecycle.StartOperation(id, request.Key, request.Kind, request.CorrelationId);
        }

        return scope;
    }

    public IProfilingSegmentScope BeginSegment(string key, string displayName = null)
    {
        var frame = this.context.Current;
        return this.BeginSegment(frame?.Capture, frame?.Invocation, key, displayName);
    }

    public void SetKey(string key) => this.Current?.SetKey(key);
    public void SetDimension(string key, object value) => this.Current?.SetDimension(key, value);
    public void SetMeasurement(string key, object value, string unit) => this.Current?.SetMeasurement(key, value, unit);
    public IDisposable Suppress() => this.context.Enter(new OperationProfilingFrame(new ProfilingOperationScope(this, null, Guid.Empty), null, null, true));
    public IDisposable BeginExecutionBoundary() => this.context.Enter(null);

    internal IProfilingSegmentScope BeginSegment(ProfilingCaptureState capture, ProfilingInvocation parent, string key, string displayName)
    {
        ProfilingInvocation invocation = null;
        if (this.context.Current?.Suppressed != true && capture is not null && (parent is null || !parent.Closed))
        {
            capture.Observe(() => invocation = capture.StartSegment(parent, key, displayName));
        }

        var scope = new ProfilingSegmentScope(this, capture, invocation);
        var current = this.context.Current;
        if (current is not null || capture is not null)
        {
            // A rejected child is an explicit suppressed boundary, never a new top-level sample.
            var frame = new OperationProfilingFrame(capture?.Operation ?? current?.Operation, capture, invocation, current?.Suppressed == true || invocation is null);
            scope.Restoration = this.context.Enter(frame);
        }

        return scope;
    }

    internal bool TryCharge(ProfilingCaptureState capture, long delta)
    {
        lock (this.sync)
        {
            if (!this.active.ContainsKey(capture.Id) || delta > this.options.MaxActiveBytes - this.activeBytes
                || delta > this.options.MaxRecordBytes - capture.ChargedBytes)
            {
                return false;
            }

            this.activeBytes += delta;
            capture.ChargedBytes += delta;
            return true;
        }
    }

    internal void Release(ProfilingCaptureState capture)
    {
        lock (this.sync)
        {
            if (this.active.Remove(capture.Id))
            {
                this.activeBytes -= capture.ChargedBytes;
            }
        }
    }

    internal void Publish(OperationProfilingRecord record)
    {
        try
        {
            if (!this.sink.TryEnqueue(record))
            {
                Interlocked.Increment(ref this.Counters.CompletionRejected);
            }

            Interlocked.Increment(ref this.Counters.Completed);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.Counters.CaptureFaults);
            Interlocked.Increment(ref this.Counters.CompletionRejected);
        }
    }

    internal ProfilingFailureDescriptor Classify(Exception exception, out bool classificationFailed)
    {
        classificationFailed = false;
        return this.ClassifyCore(exception, out classificationFailed);
    }

    private ProfilingFailureDescriptor ClassifyCore(Exception exception, out bool classificationFailed)
    {
        classificationFailed = false;
        if (exception is null)
        {
            return new ProfilingFailureDescriptor { Source = "Application", Code = "Failed" };
        }

        try
        {
            return this.errors?.Classify(exception) ?? new ProfilingFailureDescriptor
            {
                Source = "Exception",
                Code = "Unhandled",
                ExceptionType = exception.GetType().FullName,
            };
        }
        catch (Exception)
        {
            classificationFailed = true;
            return new ProfilingFailureDescriptor { Source = "Exception", Code = "ClassificationFailed", ExceptionType = exception.GetType().FullName };
        }
    }

    public void ExpireAbandoned()
    {
        ProfilingCaptureState[] captures;
        lock (this.sync)
        {
            captures = this.active.Values.ToArray();
        }

        foreach (var capture in captures)
        {
            capture.Observe(() => { });
        }
    }

    public void CloseForHost()
    {
        ProfilingCaptureState[] captures;
        lock (this.sync)
        {
            captures = this.active.Values.ToArray();
        }

        foreach (var capture in captures)
        {
            capture.Finish(ProfilingIncompleteReason.HostStopping);
        }
    }
}
