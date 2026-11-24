// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Uses optional Common profiling. Owned source work always owns an independent operation root.</summary>
/// <example><code>collection.WithProfiling();</code></example>
public sealed class ProfilingCacheBehavior(IOperationProfiler profiling = null) : ICacheCollectionBehavior, ICacheLoadBehavior, ICacheTransferObserver
{
    /// <inheritdoc />
    public ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next, CancellationToken cancellationToken = default) =>
        profiling is null ? next(context, cancellationToken) : profiling.JoinOrStartValueTaskAsync("cache:" + context.Collection + ":" + context.Operation,
            OperationProfilingKind.Service, (_, token) => next(context, token), cancellationToken, result => OperationProfilingHelpers.ClassifyResult(result));

    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheLoadBehaviorContext context, CacheLoadContinuation<T> next, CancellationToken cancellationToken = default)
    {
        IProfilingOperationScope operation = null;
        try { operation = profiling?.BeginOperation("cache:" + context.Collection + ":source", OperationProfilingKind.Service); }
        catch { /* Optional instrumentation must not prevent source execution. */ }

        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            try
            {
                if (result.IsSuccess) { operation?.Complete(); }
                else { operation?.Fail(new ProfilingFailureDescriptor { Source = "Cache", Code = "SourceFailure" }); }
            }
            catch { /* Preserve the actual source result. */ }

            return result;
        }
        catch (Exception exception)
        {
            try
            {
                if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) { operation?.Cancel(); }
                else { operation?.Fail(exception); }
            }
            catch { /* Preserve the actual source exception. */ }

            throw;
        }
        finally
        {
            try { operation?.Dispose(); }
            catch { /* Preserve the actual source outcome. */ }
        }
    }

    /// <inheritdoc />
    public ICacheTransferObservationScope BeginTransfer(CacheTransferContext context) => profiling is null ? null :
        new TransferScope(profiling.BeginOperation("cache:" + context.Collection + ":transfer", OperationProfilingKind.Service));

    private sealed class TransferScope(IProfilingOperationScope operation) : ICacheTransferObservationScope
    {
        public void Complete(CacheTransferObservation observation)
        {
            try
            {
                if (observation.Completion.IsSuccess && observation.Completion.Value != CacheReadCompletion.Incomplete) { operation?.Complete(); }
                else { operation?.Fail(new ProfilingFailureDescriptor { Source = "Cache", Code = "TransferIncompleteOrFailed" }); }
            }
            finally { operation?.Dispose(); }
        }
    }
}