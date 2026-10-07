// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Provides synchronous, nonblocking execution-local operation instrumentation.</summary>
/// <example><code>using var operation = profiling.BeginOperation("Refresh", OperationProfilingKind.Service); operation.Complete();</code></example>
public interface IOperationProfiler
{
    /// <summary>Gets the current execution boundary, including closed or suppressed boundaries.</summary>
    /// <example><code>var operation = profiling.Current;</code></example>
    IProfilingOperationScope Current { get; }
    /// <summary>Begins an independent root while restoring prior ownership when disposed.</summary>
    /// <example><code>using var operation = profiling.BeginOperation("Refresh", OperationProfilingKind.Service);</code></example>
    IProfilingOperationScope BeginOperation(string key, OperationProfilingKind kind = OperationProfilingKind.Custom);
    /// <summary>Begins a root with host-defined metadata.</summary>
    /// <example><code>using var operation = profiling.BeginOperation(new OperationProfilingStartRequest { Key = "Refresh", Kind = "Service" });</code></example>
    IProfilingOperationScope BeginOperation(OperationProfilingStartRequest request);
    /// <summary>Starts a segment under the current execution's parent invocation.</summary>
    /// <example><code>using var segment = profiling.BeginSegment("Load"); segment.Complete();</code></example>
    IProfilingSegmentScope BeginSegment(string key, string displayName = null);
    /// <summary>Changes the current root's grouping key, preserving its previous key on rejection.</summary>
    /// <example><code>profiling.SetKey("catalog:lookup");</code></example>
    void SetKey(string key);
    /// <summary>Replaces one typed dimension on the current root.</summary>
    /// <example><code>profiling.SetDimension("category", "books");</code></example>
    void SetDimension(string key, object value);
    /// <summary>Replaces one finite numeric root measurement.</summary>
    /// <example><code>profiling.SetMeasurement("items", 42, "count");</code></example>
    void SetMeasurement(string key, object value, string unit);
    /// <summary>Suppresses capture and replacement roots until the boundary is disposed.</summary>
    /// <example><code>using var suppression = profiling.Suppress();</code></example>
    IDisposable Suppress();
    /// <summary>Clears inherited ownership for an independent worker entry and restores it on exit.</summary>
    /// <example><code>using var boundary = profiling.BeginExecutionBoundary();</code></example>
    IDisposable BeginExecutionBoundary();
}

/// <summary>Defines explicit outcome and typed enrichment for recording scopes.</summary>
/// <example><code>segment.SetDimension("dependency", "sql"); segment.Complete();</code></example>
public interface IProfilingRecordingScope : IDisposable
{
    /// <summary>Gets whether the scope can still accept observations.</summary>
    /// <example><code>if (scope.IsRecording) { scope.SetDimension("source", "catalog"); }</code></example>
    bool IsRecording { get; }
    /// <summary>Declares normal observed completion.</summary>
    /// <example><code>scope.Complete();</code></example>
    void Complete();
    /// <summary>Classifies an application exception without retaining its object or raw message.</summary>
    /// <example><code>scope.Fail(exception);</code></example>
    void Fail(Exception exception);
    /// <summary>Records a bounded safe domain failure descriptor.</summary>
    /// <example><code>scope.Fail(new ProfilingFailureDescriptor { Source = "Domain", Code = "Rejected" });</code></example>
    void Fail(ProfilingFailureDescriptor failure);
    /// <summary>Declares reliably attributed cancellation.</summary>
    /// <example><code>scope.Cancel();</code></example>
    void Cancel();
    /// <summary>Replaces a typed dimension on this scope.</summary>
    /// <example><code>scope.SetDimension("dependency", "sql");</code></example>
    void SetDimension(string key, object value);
    /// <summary>Begins a child under this specific live parent, suitable for parallel branches.</summary>
    /// <example><code>using var child = scope.BeginSegment("Read");</code></example>
    IProfilingSegmentScope BeginSegment(string key, string displayName = null);
}

/// <summary>Owns one root's capture lifecycle and unique occurrence ID.</summary>
/// <example><code>using var operation = profiling.BeginOperation("Refresh"); var id = operation.Id;</code></example>
public interface IProfilingOperationScope : IProfilingRecordingScope
{
    /// <summary>Gets the selected occurrence ID even when admission rejects recording.</summary>
    /// <example><code>var id = operation.Id;</code></example>
    Guid Id { get; }
    /// <summary>Changes this root's logical grouping key.</summary>
    /// <example><code>operation.SetKey("catalog:refresh");</code></example>
    void SetKey(string key);
    /// <summary>Replaces a finite numeric root measurement.</summary>
    /// <example><code>operation.SetMeasurement("items", 42, "count");</code></example>
    void SetMeasurement(string key, object value, string unit);
    /// <summary>Adds bounded typed source metadata.</summary>
    /// <example><code>operation.SetSource(new ProfilingAdapterMetadata { Kind = "Job", SchemaVersion = 1 });</code></example>
    void SetSource(ProfilingAdapterMetadata metadata);
    /// <summary>Sets the optional HTTP projection owned by the HTTP adapter.</summary>
    /// <example><code>operation.SetHttpMetadata(new HttpRequestProfilingMetadata { Method = "GET" });</code></example>
    void SetHttpMetadata(HttpRequestProfilingMetadata metadata);
    /// <summary>Declares transport interruption while preserving failure evidence.</summary>
    /// <example><code>operation.Abort();</code></example>
    void Abort();
    /// <summary>Marks classifier failure independently of application outcome.</summary>
    /// <example><code>operation.MarkClassificationFailed();</code></example>
    void MarkClassificationFailed();
}

/// <summary>Owns one invocation whose values reduce into its path summary at disposal.</summary>
/// <example><code>using var segment = operation.BeginSegment("Load"); segment.Complete();</code></example>
public interface IProfilingSegmentScope : IProfilingRecordingScope
{
    /// <summary>Replaces one invocation measurement, reduced exactly once on closure.</summary>
    /// <example><code>segment.SetMeasurement("items", 42, "count", MeasurementAggregation.Sum);</code></example>
    void SetMeasurement(string key, object value, string unit, MeasurementAggregation aggregation = MeasurementAggregation.Sum);
    /// <summary>Marks a supplied classifier fault without changing the business result.</summary>
    /// <example><code>segment.MarkClassificationFailed();</code></example>
    void MarkClassificationFailed();
}

/// <summary>Allows host-supplied sanitization of failure messages; raw messages are omitted by default.</summary>
/// <example><code>services.AddSingleton&lt;IProfilingSafeErrorPolicy, SafeErrorPolicy&gt;();</code></example>
public interface IProfilingSafeErrorPolicy
{
    /// <summary>Returns safe bounded classification and optionally a sanitized message.</summary>
    /// <example><code>var descriptor = policy.Classify(exception);</code></example>
    ProfilingFailureDescriptor Classify(Exception exception);
}

/// <summary>Supplies one cached process identity without storage or Broadcast access.</summary>
/// <example><code>var node = identities.GetNode();</code></example>
public interface IProfilingNodeIdentityProvider
{
    /// <summary>Returns the immutable cached process descriptor without I/O.</summary>
    /// <example><code>var node = identities.GetNode();</code></example>
    ProfilingNode GetNode();
}
