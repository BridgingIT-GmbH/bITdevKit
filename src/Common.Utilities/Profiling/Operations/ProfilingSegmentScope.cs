// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class ProfilingSegmentScope(OperationProfiler profiler, ProfilingCaptureState capture, ProfilingInvocation invocation) : IProfilingSegmentScope
{
    private int disposed;
    public IDisposable Restoration { get; set; }
    public bool IsRecording { get { capture?.Observe(() => { }); return invocation is { Closed: false } && capture?.IsOpen == true; } }

    public void Complete() => this.Declare(OperationProfilingOutcome.Completed);
    public void Cancel() => this.Declare(OperationProfilingOutcome.Canceled);
    public void Fail(ProfilingFailureDescriptor failure) => this.Declare(OperationProfilingOutcome.Failed, failure);
    public void Fail(Exception exception)
    {
        if (!this.IsRecording)
        {
            return;
        }

        var descriptor = profiler.Classify(exception, out var classifierFailed);
        capture.Declare(OperationProfilingOutcome.Failed, descriptor, invocation, classifierFailed);
    }

    public void SetDimension(string key, object value)
    {
        if (invocation is { Closed: false })
        {
            capture.SetDimension(key, value, invocation);
        }
    }

    public void SetMeasurement(string key, object value, string unit, MeasurementAggregation aggregation = MeasurementAggregation.Sum)
    {
        if (invocation is { Closed: false })
        {
            capture.SetMeasurement(key, value, unit, invocation, aggregation);
        }
    }

    public void MarkClassificationFailed()
    {
        if (invocation is { Closed: false })
        {
            capture.MarkClassificationFailed();
        }
    }

    public IProfilingSegmentScope BeginSegment(string key, string displayName = null) =>
        profiler.BeginSegment(invocation is { Closed: false } ? capture : null, invocation, key, displayName);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (invocation is not null)
            {
                capture.CloseSegment(invocation);
            }
        }
        finally
        {
            this.Restoration?.Dispose();
            this.Restoration = null;
        }
    }

    private void Declare(OperationProfilingOutcome outcome, ProfilingFailureDescriptor descriptor = null)
    {
        if (invocation is { Closed: false })
        {
            capture.Declare(outcome, descriptor, invocation);
        }
    }
}
