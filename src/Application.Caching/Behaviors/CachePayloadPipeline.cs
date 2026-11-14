// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Composes same-store payload transformations before engine-owned publication.</summary>
/// <remarks>Chains are built once. Each invocation requires its own engine-created staging context.</remarks>
/// <example><code>var pipeline = new CachePayloadPipeline([new CompressionCachePayloadBehavior()]);</code></example>
public sealed class CachePayloadPipeline
{
    /// <summary>Gets the compression-only default payload pipeline used when no additional transformation stages are selected.</summary>
    public static readonly CachePayloadPipeline CompressionOnly = new([new CompressionCachePayloadBehavior()]);
    private readonly CachePayloadContinuation chain;

    /// <summary>Builds the bounded transformation chain in its declared order.</summary>
    /// <param name="behaviors">The compatible payload behaviors, in outermost-to-innermost order.</param>
    public CachePayloadPipeline(IReadOnlyList<ICachePayloadBehavior> behaviors)
    {
        this.chain = (context, token) => ContinueAsync(context, token);
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var next = this.chain;
            var stage = index;
            this.chain = (context, token) => context.Invocation is CacheBehaviorInvocation<bool> invocation && invocation.TryEnterStage(stage) ?
                behavior.ExecuteAsync(context, next, token) : ValueTask.FromResult(Result.Failure(new CacheError(CacheErrorCode.Contract,
                    "A payload behavior cannot repeat a remaining transformation stage.")));
        }
    }

    /// <summary>Transforms the selected stage and requires every remaining mandatory continuation to complete.</summary>
    /// <param name="context">The engine-owned staging context supplied to the payload behavior.</param>
    /// <param name="cancellationToken">The original bounded transfer cancellation token.</param>
    /// <returns>Success when the chain completed, or a failure that prevents publication.</returns>
    public async ValueTask<Result> ExecuteAsync(CachePayloadBehaviorContext context, CancellationToken cancellationToken)
    {
        var invocation = new CacheBehaviorInvocation<bool>(_ => ValueTask.FromResult(Result<bool>.Success(true)));
        context.Invocation = invocation;
        try
        {
            var result = await this.chain(context, cancellationToken).ConfigureAwait(false);
            var actual = invocation.Seal();
            if (actual is not null)
            {
                await actual.ConfigureAwait(false);
            }

            return result.IsFailure ? result : actual is not null ? result : Result.Failure(new CacheError(CacheErrorCode.Contract,
                "A payload behavior cannot omit the remaining required transformation chain."));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result.Failure(new CacheError(exception is CacheTransferException transfer ? transfer.Code :
                exception is StreamSizeLimitExceededException ? CacheErrorCode.Capacity :
                exception is IOException ? CacheErrorCode.ProviderUnavailable : CacheErrorCode.Contract,
                "The bounded payload behavior chain did not complete."));
        }
        finally
        {
            _ = invocation.Seal();
            context.Invocation = null;
        }
    }

    private static async ValueTask<Result> ContinueAsync(CachePayloadBehaviorContext context, CancellationToken cancellationToken)
    {
        if (context.Invocation is not CacheBehaviorInvocation<bool> invocation)
        {
            return Result.Failure(new CacheError(CacheErrorCode.Contract, "The payload continuation is no longer active."));
        }

        var result = await invocation.ContinueAsync(cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }
}