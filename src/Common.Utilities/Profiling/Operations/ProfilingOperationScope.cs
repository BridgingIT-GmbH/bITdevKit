// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

internal sealed class ProfilingOperationScope(OperationProfiler profiler, ProfilingCaptureState capture, Guid id, OperationProfilingFrame previous = null) : IProfilingOperationScope
{
    private int disposed;
    public Guid Id { get; } = id;
    public IDisposable Restoration { get; set; }
    public OperationProfilingFrame Previous { get; } = previous;
    public bool Disposed => Volatile.Read(ref this.disposed) != 0;
    public bool IsRecording { get { capture?.Observe(() => { }); return capture?.IsOpen == true; } }

    public void Complete() => capture?.Declare(OperationProfilingOutcome.Completed);
    public void Cancel() => capture?.Declare(OperationProfilingOutcome.Canceled);
    public void Abort() => capture?.Declare(OperationProfilingOutcome.Aborted);
    public void Fail(ProfilingFailureDescriptor failure) => capture?.Declare(OperationProfilingOutcome.Failed, failure);
    public void Fail(Exception exception)
    {
        if (capture?.IsOpen != true)
        {
            return;
        }

        var descriptor = profiler.Classify(exception, out var classifierFailed);
        capture.Declare(OperationProfilingOutcome.Failed, descriptor, classifierFailed: classifierFailed);
    }

    public void SetKey(string key) => capture?.SetKey(key);
    public void SetDimension(string key, object value) => capture?.SetDimension(key, value);
    public void SetMeasurement(string key, object value, string unit) => capture?.SetMeasurement(key, value, unit);
    public void SetSource(ProfilingAdapterMetadata metadata) => capture?.SetSource(metadata);
    public void SetHttpMetadata(HttpRequestProfilingMetadata metadata) => capture?.SetHttp(metadata);
    public void MarkClassificationFailed() => capture?.MarkClassificationFailed();
    public IProfilingSegmentScope BeginSegment(string key, string displayName = null) => profiler.BeginSegment(capture, null, key, displayName);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        try
        {
            capture?.Finish();
        }
        finally
        {
            this.Restoration?.Dispose();
            this.Restoration = null;
        }
    }
}
