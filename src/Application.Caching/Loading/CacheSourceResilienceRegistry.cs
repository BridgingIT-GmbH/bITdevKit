// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Frozen;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

/// <summary>Classifies a repeatable source failure for a host-registered resilience pipeline without exposing a key or source exception.</summary>
/// <example><code>var predicate = new PredicateBuilder().Handle&lt;CacheSourceException&gt;();</code></example>
public sealed class CacheSourceException : Exception
{
    /// <summary>Captures a repeatable source result for policy classification without exposing its payload key or original exception.</summary>
    public CacheSourceException(IResult failure) : base("The repeatable cache source returned a failure.")
    {
        this.Failure = failure;
        this.Code = failure.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.SourceFailure;
    }

    /// <summary>Gets the source classification. Provider acquisition/publication failures never enter this policy.</summary>
    public CacheErrorCode Code { get; }

    /// <summary>Gets the original controlled source failure result used to preserve its errors after policy execution.</summary>
    public IResult Failure { get; }
}

/// <summary>Atomically limits total source attempts across cache retries and a selected host dependency policy.</summary>
/// <example><code>var budget = new CacheSourceAttemptBudget(2); var admitted = budget.TryTake();</code></example>
public sealed class CacheSourceAttemptBudget(int maximum)
{
    private int used;

    /// <summary>Consumes one sequential source attempt, returning false once the original owned attempt ceiling is exhausted.</summary>
    public bool TryTake()
    {
        while (true)
        {
            var current = Volatile.Read(ref this.used);
            if (current >= maximum)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref this.used, current + 1, current) == current)
            {
                return true;
            }
        }
    }
}

/// <summary>Lazily resolves trusted dependency resilience pipelines by startup alias, independently of provider infrastructure policies.</summary>
/// <example><code>var result = await policies.ExecuteAsync(&quot;inventory&quot;, source, budget, false, token);</code></example>
public sealed class CacheSourceResilienceRegistry
{
    private readonly FrozenDictionary<string, Lazy<ResiliencePipeline>> policies;

    /// <summary>Captures code-owned dependency policies without resolving or executing them during registration.</summary>
    public CacheSourceResilienceRegistry(CacheCatalog catalog, IServiceProvider services)
    {
        this.policies = catalog.Registry.SourceResilience.ToFrozenDictionary(p => p.Key, p => new Lazy<ResiliencePipeline>(() =>
            p.Value(services) as ResiliencePipeline ?? throw new ArgumentException("The registered source policy did not return a resilience pipeline."),
            LazyThreadSafetyMode.ExecutionAndPublication), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Executes a source through its trusted policy within one sequential attempt budget and awaits cancellation-ignoring source work before releasing ownership.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    public async ValueTask<Result<T>> ExecuteAsync<T>(string alias, Func<CancellationToken, Task<Result<T>>> source,
        CacheSourceAttemptBudget budget, bool singleAttempt, CancellationToken cancellationToken, Func<bool> canRetry = null)
    {
        var pipeline = this.policies[CacheIdentityNormalizer.Name(alias)].Value;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var calls = 0;
        var active = 0;
        var budgetEnded = false;
        Task<Result<T>> actual = null;
        Result<T>? lastFailure = null;
        try
        {
            return await pipeline.ExecuteAsync<Result<T>>(async token =>
            {
                if ((Volatile.Read(ref calls) != 0 && (singleAttempt || canRetry?.Invoke() == false)) || !budget.TryTake())
                {
                    budgetEnded = true;
                    stop.Cancel();
                    throw new OperationCanceledException(stop.Token);
                }

                if (Interlocked.CompareExchange(ref active, 1, 0) != 0)
                {
                    budgetEnded = true;
                    stop.Cancel();
                    throw new OperationCanceledException(stop.Token); // No parallel/hedged source executions within one admitted work unit.
                }

                Interlocked.Increment(ref calls);
                try
                {
                    actual = source(token);
                    var result = await actual.ConfigureAwait(false);
                    if (CacheSourceFailureClassifier.IsSourceFailure(result))
                    {
                        lastFailure = result;
                        throw new CacheSourceException(result);
                    }

                    return result;
                }
                finally
                {
                    Volatile.Write(ref active, 0);
                }
            }, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (budgetEnded && !cancellationToken.IsCancellationRequested)
        {
            return lastFailure ?? Result<T>.Failure(new CacheError(CacheErrorCode.SourceRejected, "The source policy exceeded its sequential attempt budget."));
        }
        catch (CacheSourceException exception)
        {
            return Result<T>.Failure(exception.Failure);
        }
        catch (BrokenCircuitException)
        {
            return Result<T>.Failure(new CacheError(CacheErrorCode.SourceRejected, "The registered source circuit is open."));
        }
        catch (TimeoutRejectedException)
        {
            return Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The registered source policy rejected its bounded execution."));
        }
        finally
        {
            if (actual is not null && !actual.IsCompleted)
            {
                // A policy timeout does not end a callback that ignores cancellation. Keep its
                // cache-owned scope and source admission alive until actual termination.
                try { await actual.ConfigureAwait(false); }
                catch (Exception) { }
            }
        }
    }
}

/// <summary>Separates repeatable dependency failures from cache validation, admission, authority and provider failures.</summary>
/// <example><code>var retryable = CacheSourceFailureClassifier.IsSourceFailure(result);</code></example>
public static class CacheSourceFailureClassifier
{
    /// <summary>Tests whether a failed result contains only retryable source failure/timeout classifications or ordinary source errors.</summary>
    public static bool IsSourceFailure(IResult result) => result.IsFailure && !result.Errors.OfType<CacheError>().Any(e =>
        e.Code is not (CacheErrorCode.SourceFailure or CacheErrorCode.SourceTimeout));
}