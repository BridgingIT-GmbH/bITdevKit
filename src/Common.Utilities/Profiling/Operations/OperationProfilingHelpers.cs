// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Times business delegates once, with explicit result classification and fault-isolated observation.</summary>
/// <example><code>var value = await profiling.RunOperationAsync("Refresh", OperationProfilingKind.Service, (_, ct) => service.RefreshAsync(ct), cancellationToken);</code></example>
public static class OperationProfilingHelpers
{
    /// <summary>Runs one synchronous delegate in its own operation and returns the original value.</summary>
    /// <example><code>var value = profiling.RunOperation("Read", OperationProfilingKind.Service, scope => Read(), classify: null);</code></example>
    public static T RunOperation<T>(this IOperationProfiler profiling, string key, OperationProfilingKind kind, Func<IProfilingOperationScope, T> work, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Run(Start(() => profiling.BeginOperation(key, kind), NoOperation.Instance), work, classify);
    }

    /// <summary>Runs one synchronous delegate and observes its execution outcome.</summary>
    /// <example><code>profiling.RunOperation("Read", OperationProfilingKind.Service, scope => Read());</code></example>
    public static void RunOperation(this IOperationProfiler profiling, string key, OperationProfilingKind kind, Action<IProfilingOperationScope> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Run(Start(() => profiling.BeginOperation(key, kind), NoOperation.Instance), scope => { work(scope); return true; }, null);
    }

    /// <summary>Runs one asynchronous delegate, preserving its result, exception and application token.</summary>
    /// <example><code>var value = await profiling.RunOperationAsync("Read", OperationProfilingKind.Service, (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task<T> RunOperationAsync<T>(this IOperationProfiler profiling, string key, OperationProfilingKind kind, Func<IProfilingOperationScope, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return await RunAsync(Start(() => profiling.BeginOperation(key, kind), NoOperation.Instance), work, cancellationToken, classify).ConfigureAwait(false);
    }

    /// <summary>Runs one asynchronous delegate without a return value.</summary>
    /// <example><code>await profiling.RunOperationAsync("Read", OperationProfilingKind.Service, (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task RunOperationAsync(this IOperationProfiler profiling, string key, OperationProfilingKind kind, Func<IProfilingOperationScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await RunAsync(Start(() => profiling.BeginOperation(key, kind), NoOperation.Instance), async (scope, ct) => { await work(scope, ct).ConfigureAwait(false); return true; }, cancellationToken, null).ConfigureAwait(false);
    }

    /// <summary>Runs one synchronous delegate in its own segment and returns the original value.</summary>
    /// <example><code>var value = profiling.RunSegment("Read", scope => Read(), classify: null);</code></example>
    public static T RunSegment<T>(this IOperationProfiler profiling, string key, Func<IProfilingSegmentScope, T> work, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Run(Start(() => profiling.BeginSegment(key), NoSegment.Instance), work, classify);
    }

    /// <summary>Runs one synchronous delegate and observes its execution outcome.</summary>
    /// <example><code>profiling.RunSegment("Read", scope => Read());</code></example>
    public static void RunSegment(this IOperationProfiler profiling, string key, Action<IProfilingSegmentScope> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Run(Start(() => profiling.BeginSegment(key), NoSegment.Instance), scope => { work(scope); return true; }, null);
    }

    /// <summary>Runs one asynchronous delegate, preserving its result, exception and application token.</summary>
    /// <example><code>var value = await profiling.RunSegmentAsync("Read", (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task<T> RunSegmentAsync<T>(this IOperationProfiler profiling, string key, Func<IProfilingSegmentScope, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return await RunAsync(Start(() => profiling.BeginSegment(key), NoSegment.Instance), work, cancellationToken, classify).ConfigureAwait(false);
    }

    /// <summary>Runs one asynchronous delegate without a return value.</summary>
    /// <example><code>await profiling.RunSegmentAsync("Read", (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task RunSegmentAsync(this IOperationProfiler profiling, string key, Func<IProfilingSegmentScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await RunAsync(Start(() => profiling.BeginSegment(key), NoSegment.Instance), async (scope, ct) => { await work(scope, ct).ConfigureAwait(false); return true; }, cancellationToken, null).ConfigureAwait(false);
    }

    /// <summary>Runs one synchronous delegate in its own segment and returns the original value.</summary>
    /// <example><code>var value = profiling.RunSegment("Read", scope => Read(), classify: null);</code></example>
    public static T RunSegment<T>(this IProfilingRecordingScope profiling, string key, Func<IProfilingSegmentScope, T> work, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Run(Start(() => profiling.BeginSegment(key), NoSegment.Instance), work, classify);
    }

    /// <summary>Runs one synchronous delegate and observes its execution outcome.</summary>
    /// <example><code>profiling.RunSegment("Read", scope => Read());</code></example>
    public static void RunSegment(this IProfilingRecordingScope profiling, string key, Action<IProfilingSegmentScope> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        Run(Start(() => profiling.BeginSegment(key), NoSegment.Instance), scope => { work(scope); return true; }, null);
    }

    /// <summary>Runs one asynchronous delegate, preserving its result, exception and application token.</summary>
    /// <example><code>var value = await profiling.RunSegmentAsync("Read", (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task<T> RunSegmentAsync<T>(this IProfilingRecordingScope profiling, string key, Func<IProfilingSegmentScope, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default, Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return await RunAsync(Start(() => profiling.BeginSegment(key), NoSegment.Instance), work, cancellationToken, classify).ConfigureAwait(false);
    }

    /// <summary>Runs one asynchronous delegate without a return value.</summary>
    /// <example><code>await profiling.RunSegmentAsync("Read", (scope, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static async Task RunSegmentAsync(this IProfilingRecordingScope profiling, string key, Func<IProfilingSegmentScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await RunAsync(Start(() => profiling.BeginSegment(key), NoSegment.Instance), async (scope, ct) => { await work(scope, ct).ConfigureAwait(false); return true; }, cancellationToken, null).ConfigureAwait(false);
    }

    /// <summary>Runs a Custom operation and returns the original business value.</summary>
    /// <example><code>var value = profiling.RunOperation("Read", _ => Read());</code></example>
    public static T RunOperation<T>(this IOperationProfiler profiling, string key, Func<IProfilingOperationScope, T> work,
        Func<T, ProfilingResultClassification> classify = null) => profiling.RunOperation(key, OperationProfilingKind.Custom, work, classify);

    /// <summary>Runs a Custom operation with no return value.</summary>
    /// <example><code>profiling.RunOperation("Read", _ => Read());</code></example>
    public static void RunOperation(this IOperationProfiler profiling, string key, Action<IProfilingOperationScope> work) =>
        profiling.RunOperation(key, OperationProfilingKind.Custom, work);

    /// <summary>Runs an asynchronous Custom operation and returns the original business value.</summary>
    /// <example><code>var value = await profiling.RunOperationAsync("Read", (_, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static Task<T> RunOperationAsync<T>(this IOperationProfiler profiling, string key, Func<IProfilingOperationScope, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default, Func<T, ProfilingResultClassification> classify = null) =>
        profiling.RunOperationAsync(key, OperationProfilingKind.Custom, work, cancellationToken, classify);

    /// <summary>Runs an asynchronous Custom operation with no return value.</summary>
    /// <example><code>await profiling.RunOperationAsync("Read", (_, ct) => ReadAsync(ct), cancellationToken);</code></example>
    public static Task RunOperationAsync(this IOperationProfiler profiling, string key, Func<IProfilingOperationScope, CancellationToken, Task> work,
        CancellationToken cancellationToken = default) => profiling.RunOperationAsync(key, OperationProfilingKind.Custom, work, cancellationToken);

    /// <summary>Joins the current boundary or owns one root when no operation exists.</summary>
    /// <example><code>var result = await profiling.JoinOrStartAsync("job:refresh", OperationProfilingKind.Job, (_, ct) => next(ct), cancellationToken);</code></example>
    public static async Task<T> JoinOrStartAsync<T>(this IOperationProfiler profiling, string key, OperationProfilingKind kind,
        Func<IProfilingRecordingScope, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default,
        Func<T, ProfilingResultClassification> classify = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return await RunAsync(JoinOrStart(profiling, key, kind), work, cancellationToken, classify).ConfigureAwait(false);
    }

    /// <summary>Explicitly classifies the devkit's Result contract without storing result payloads or raw messages.</summary>
    /// <example><code>var value = await profiling.RunSegmentAsync("Read", (_, ct) => ReadAsync(ct), cancellationToken, value => ClassifyResult(value));</code></example>
    public static ProfilingResultClassification ClassifyResult(IResult result) => result is null ? null : new()
    {
        Outcome = result.IsFailure ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed,
        Failure = result.IsFailure ? new ProfilingFailureDescriptor { Source = "Result", Code = "Failure" } : null,
    };

    /// <summary>Explicitly classifies a typed Result without retaining its value or error messages.</summary>
    /// <example><code>var value = await profiling.RunSegmentAsync("Read", (_, ct) => ReadAsync(ct), cancellationToken, value => ClassifyResult(value));</code></example>
    public static ProfilingResultClassification ClassifyResult<T>(IResult<T> result) => result is null ? null : new()
    {
        Outcome = result.IsFailure ? OperationProfilingOutcome.Failed : OperationProfilingOutcome.Completed,
        Failure = result.IsFailure ? new ProfilingFailureDescriptor { Source = "Result", Code = "Failure" } : null,
    };

    internal static async ValueTask<T> RunSegmentValueTaskAsync<T>(IOperationProfiler profiling, string key,
        Func<IProfilingSegmentScope, CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken,
        Func<T, ProfilingResultClassification> classify = null) =>
        await RunValueTaskAsync(Start(() => profiling.BeginSegment(key), NoSegment.Instance), work, cancellationToken, classify).ConfigureAwait(false);

    internal static async ValueTask<T> JoinOrStartValueTaskAsync<T>(IOperationProfiler profiling, string key, OperationProfilingKind kind,
        Func<IProfilingRecordingScope, CancellationToken, ValueTask<T>> work, CancellationToken cancellationToken,
        Func<T, ProfilingResultClassification> classify = null) => await RunValueTaskAsync(JoinOrStart(profiling, key, kind), work, cancellationToken, classify).ConfigureAwait(false);

    private static IProfilingRecordingScope JoinOrStart(IOperationProfiler profiling, string key, OperationProfilingKind kind)
    {
        try
        {
            return profiling is null ? NoOperation.Instance : profiling.Current is null
                ? profiling.BeginOperation(key, kind) : profiling.BeginSegment(key);
        }
        catch (Exception)
        {
            return NoOperation.Instance;
        }
    }

    private static TScope Start<TScope>(Func<TScope> start, TScope fallback) where TScope : class, IProfilingRecordingScope
    {
        try
        {
            return start() ?? fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static T Run<TScope, T>(TScope scope, Func<TScope, T> work, Func<T, ProfilingResultClassification> classify)
        where TScope : IProfilingRecordingScope
    {
        try
        {
            T result;
            try
            {
                result = work(scope);
            }
            catch (Exception exception)
            {
                RecordException(scope, exception, default);
                throw;
            }

            RecordResult(scope, result, classify);
            return result;
        }
        finally
        {
            Safe(scope.Dispose);
        }
    }

    private static async Task<T> RunAsync<TScope, T>(TScope scope, Func<TScope, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken, Func<T, ProfilingResultClassification> classify) where TScope : IProfilingRecordingScope
    {
        try
        {
            T result;
            try
            {
                result = await work(scope, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordException(scope, exception, cancellationToken);
                throw;
            }

            RecordResult(scope, result, classify);
            return result;
        }
        finally
        {
            Safe(scope.Dispose);
        }
    }

    private static async ValueTask<T> RunValueTaskAsync<TScope, T>(TScope scope, Func<TScope, CancellationToken, ValueTask<T>> work,
        CancellationToken cancellationToken, Func<T, ProfilingResultClassification> classify) where TScope : IProfilingRecordingScope
    {
        try
        {
            T result;
            try
            {
                result = await work(scope, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordException(scope, exception, cancellationToken);
                throw;
            }

            RecordResult(scope, result, classify);
            return result;
        }
        finally
        {
            Safe(scope.Dispose);
        }
    }

    private static void RecordException(IProfilingRecordingScope scope, Exception exception, CancellationToken token) => Safe(() =>
    {
        if (exception is OperationCanceledException cancellation && token.CanBeCanceled && token.IsCancellationRequested && cancellation.CancellationToken == token)
        {
            scope.Cancel();
        }
        else
        {
            scope.Fail(exception);
        }
    });

    private static void RecordResult<T>(IProfilingRecordingScope scope, T result, Func<T, ProfilingResultClassification> classify)
    {
        try
        {
            if (!scope.IsRecording)
            {
                return;
            }

            if (classify is null)
            {
                scope.Complete();
                return;
            }

            var classification = classify(result);
            switch (classification?.Outcome)
            {
                case OperationProfilingOutcome.Completed:
                    scope.Complete();
                    break;
                case OperationProfilingOutcome.Failed:
                    scope.Fail(classification.Failure);
                    break;
                case OperationProfilingOutcome.Canceled:
                    scope.Cancel();
                    break;
                default:
                    MarkClassifierFailure(scope);
                    break;
            }
        }
        catch (Exception)
        {
            MarkClassifierFailure(scope);
        }
    }

    private static void MarkClassifierFailure(IProfilingRecordingScope scope) => Safe(() =>
    {
        if (scope is IProfilingOperationScope operation)
        {
            operation.MarkClassificationFailed();
        }
        else if (scope is IProfilingSegmentScope segment)
        {
            segment.MarkClassificationFailed();
        }
    });

    private static void Safe(Action observation)
    {
        try { observation(); }
        catch (Exception) { /* Observation never replaces a business result or exception. */ }
    }

    private sealed class NoOperation : IProfilingOperationScope
    {
        public static NoOperation Instance { get; } = new();
        public Guid Id => Guid.Empty;
        public bool IsRecording => false;
        public void Complete() { }
        public void Cancel() { }
        public void Abort() { }
        public void Dispose() { }
        public void Fail(Exception exception) { }
        public void Fail(ProfilingFailureDescriptor failure) { }
        public void SetKey(string key) { }
        public void SetDimension(string key, object value) { }
        public void SetMeasurement(string key, object value, string unit) { }
        public void SetSource(ProfilingAdapterMetadata metadata) { }
        public void SetHttpMetadata(HttpRequestProfilingMetadata metadata) { }
        public void MarkClassificationFailed() { }
        public IProfilingSegmentScope BeginSegment(string key, string displayName = null) => NoSegment.Instance;
    }

    private sealed class NoSegment : IProfilingSegmentScope
    {
        public static NoSegment Instance { get; } = new();
        public bool IsRecording => false;
        public void Complete() { }
        public void Cancel() { }
        public void Dispose() { }
        public void Fail(Exception exception) { }
        public void Fail(ProfilingFailureDescriptor failure) { }
        public void SetDimension(string key, object value) { }
        public void SetMeasurement(string key, object value, string unit, MeasurementAggregation aggregation = MeasurementAggregation.Sum) { }
        public void MarkClassificationFailed() { }
        public IProfilingSegmentScope BeginSegment(string key, string displayName = null) => this;
    }
}
