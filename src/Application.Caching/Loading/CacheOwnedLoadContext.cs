// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Binds an owned source callback to its immutable item, deadline, publication fence and sequential attempt allowance.</summary>
/// <example><code>if (context.CanPublish) { /* use the captured fence for publication */ }</code></example>
public sealed class CacheOwnedLoadContext(CacheCollectionRuntime runtime, CacheItem item, IServiceProvider services,
    DateTimeOffset deadline, CachePublicationFence? fence, CacheOwnerToken? owner, CacheOwnedAuthority authority,
    CancellationToken interestToken = default)
{
    private readonly CacheObservationSlot observations = services.GetService<CacheDiagnostics>()?.ForCollection(runtime.Binding.Name);
    private readonly CacheSourceAttemptBudget attempts = new(runtime.Binding.Options.Loading.MaxAttempts);

    /// <summary>Gets the caller-facing context with declared identity, host scope, original item and fixed deadline.</summary>
    public CacheLoadContext Public { get; } = new(runtime.Identity(item.Key), item, services, deadline);

    /// <summary>Gets the authority captured before admitted source access, or null when storage publication is unavailable.</summary>
    public CachePublicationFence? Fence => fence;

    /// <summary>Gets the confirmed shared-store owner token when shared authority was acquired.</summary>
    public CacheOwnerToken? Owner => owner;

    /// <summary>Gets whether a captured fence, confirmed authority and interested callers still permit publication.</summary>
    public bool CanPublish => fence.HasValue && authority.IsConfirmed && !interestToken.IsCancellationRequested;

    /// <summary>Gets cancellation of the owning flight&apos;s interested caller group, independently of an individual waiter.</summary>
    public CancellationToken InterestToken => interestToken;

    /// <summary>Invokes only sequential admitted source attempts under the fixed owned deadline and optional trusted dependency policy.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public async ValueTask<Result<T>> InvokeAsync<T>(Func<CacheLoadContext, CancellationToken, ValueTask<Result<T>>> loader,
        CancellationToken cancellationToken, bool singleAttempt = false, Func<bool> canRetry = null)
    {
        var options = runtime.Binding.Options.Loading;
        if (options.SourceResilience is not null)
        {
            return await services.GetRequiredService<CacheSourceResilienceRegistry>().ExecuteAsync(options.SourceResilience,
                ExecuteCallbackAsync, this.attempts, singleAttempt, cancellationToken, canRetry).ConfigureAwait(false);
        }

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!this.attempts.TryTake())
            {
                return Result<T>.Failure(new CacheError(CacheErrorCode.SourceRejected, "The total owned source attempt budget is exhausted."));
            }

            var result = await ExecuteCallbackAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (result.IsSuccess || singleAttempt || canRetry?.Invoke() == false || !CacheSourceFailureClassifier.IsSourceFailure(result) || attempt >= options.MaxAttempts)
            {
                return result;
            }

            await Task.Delay(options.RetryDelay, runtime.Time, cancellationToken).ConfigureAwait(false);
        }

        async Task<Result<T>> ExecuteCallbackAsync(CancellationToken token)
        {
            // Only admitted callbacks are dispatched; native provider I/O remains on its truthful execution path.
            return await Task.Run(async () =>
            {
                if (owner.HasValue && !authority.IsConfirmed)
                {
                    return Result<T>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Shared source authority ended before this source attempt began."));
                }

                var started = runtime.Time.GetTimestamp();
                this.observations?.Record(CacheObservationKind.SourceAttempt);
                try
                {
                    var result = await CacheLoadExecutionContext.InvokeAsync(() => loader(this.Public, token)).ConfigureAwait(false);
                    this.observations?.Record(result.IsSuccess ? CacheObservationKind.SourceSuccess : CacheObservationKind.SourceFailure);
                    return result;
                }
                catch (OperationCanceledException)
                {
                    if (runtime.Time.GetUtcNow() < deadline) { this.observations?.Record(CacheObservationKind.Cancelled); }

                    throw;
                }
                catch (CacheTransferException exception)
                {
                    this.observations?.Record(CacheObservationKind.SourceFailure);
                    return Result<T>.Failure(new CacheError(exception.Code, "The cache-owned source destination could not complete."));
                }
                catch (StreamSizeLimitExceededException)
                {
                    this.observations?.Record(CacheObservationKind.SourceFailure);
                    return Result<T>.Failure(new CacheError(CacheErrorCode.Capacity, "The source output exceeds its finite byte limit."));
                }
                catch (Exception)
                {
                    this.observations?.Record(CacheObservationKind.SourceFailure);
                    return Result<T>.Failure(new CacheError(CacheErrorCode.SourceFailure, "The repeatable cache source failed."));
                }
                finally
                {
                    this.observations?.RecordDuration(CacheLatencyKind.Source, runtime.Time.GetElapsedTime(started));
                }
            }, token).ConfigureAwait(false);
        }
    }

    /// <summary>Runs an admitted binary batch callback with the same bounded sequential source attempt and authority checks.</summary>
    public async ValueTask<Result> InvokeBatchAsync(Func<CancellationToken, ValueTask<Result>> callback,
        CancellationToken cancellationToken, Func<bool> canRetry)
    {
        var result = await this.InvokeAsync<bool>(async (_, token) =>
        {
            var executed = await callback(token).ConfigureAwait(false);
            return executed.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure((IResult)executed);
        }, cancellationToken, canRetry: canRetry).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }
}