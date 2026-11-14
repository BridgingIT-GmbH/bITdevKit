// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Composes optional behaviors once per response shape and preserves the actual public collection operation result.</summary>
/// <typeparam name="T">The mandatory operation's response type.</typeparam>
/// <remarks>Each invocation requires its own context. A behavior may reject before continuation, but cannot repeat or replace confirmed work.</remarks>
/// <example><code>var pipeline = new CacheOperationPipeline&lt;CacheWriteOutcome&gt;(behaviors, tasks);</code></example>
public sealed class CacheOperationPipeline<T>
{
    private readonly CacheOperationContinuation<T> chain;
    private readonly CacheRuntimeTasks tasks;

    /// <summary>Builds the delegate chain in the supplied outermost-to-innermost order.</summary>
    /// <param name="behaviors">The bounded, compatible behavior instances selected during startup.</param>
    /// <param name="tasks">The runtime owner that observes behavior faults.</param>
    public CacheOperationPipeline(IReadOnlyList<ICacheCollectionBehavior> behaviors, CacheRuntimeTasks tasks)
    {
        this.tasks = tasks;
        this.chain = (context, token) => context.Invocation is CacheBehaviorInvocation<T> invocation ? invocation.ContinueAsync(token) :
            ValueTask.FromResult(Result<T>.Failure(new CacheError(CacheErrorCode.Contract, "The operation continuation is no longer active.")));
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = this.chain;
            var stage = index;
            this.chain = (context, token) => context.Invocation is CacheBehaviorInvocation<T> invocation && invocation.TryEnterStage(stage) ?
                behavior.ExecuteAsync(context, next, token) : ValueTask.FromResult(Result<T>.Failure(new CacheError(CacheErrorCode.Contract,
                    "An operation behavior cannot repeat a remaining stage.")));
        }
    }

    /// <summary>Runs one behavior chain and awaits mandatory work even when a behavior fails to await its continuation.</summary>
    /// <param name="context">A fresh context belonging only to this invocation.</param>
    /// <param name="operation">The mandatory work to invoke at most once.</param>
    /// <param name="cancellationToken">The original invocation cancellation token.</param>
    /// <returns>The actual mandatory result when work started, otherwise an explicit behavior rejection or contract failure.</returns>
    public async ValueTask<Result<T>> ExecuteAsync(CacheOperationContext context, Func<CancellationToken, ValueTask<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var invocation = new CacheBehaviorInvocation<T>(operation);
        context.Invocation = invocation;
        try
        {
            Result<T> observed;
            try
            {
                observed = await this.chain(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (invocation.Actual is null)
                {
                    throw;
                }

                observed = await invocation.Actual.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception)
            {
                this.tasks.Track(Task.FromException(exception), "operation-observer-failure");
                observed = Result<T>.Failure(new CacheError(CacheErrorCode.Contract, "An optional cache operation behavior failed."));
            }

            // Await a continuation even when an observer incorrectly forgot to await it. Never
            // describe an already running/confirmed mutation as a no-effect observer rejection.
            var actual = invocation.Seal();
            if (actual is not null)
            {
                return await actual.ConfigureAwait(false);
            }

            return observed.IsFailure || (observed.Value is CacheWriteOutcome { StorageOutcome: CacheStorageOutcome.Suppressed, Version: null, Metadata: null }) ?
                observed : Result<T>.Failure(new CacheError(CacheErrorCode.Contract,
                "A behavior cannot fabricate a successful engine outcome without continuation."));
        }
        finally
        {
            _ = invocation.Seal();
            context.Invocation = null;
        }
    }

}