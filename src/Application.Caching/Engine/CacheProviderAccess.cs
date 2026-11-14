// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;

/// <summary>Serializes lazy initialization once per complete provider using all declared contracts bound to that provider.</summary>
/// <example><code>var ready = await initialization.EnsureAsync(provider, token);</code></example>
public sealed class CacheProviderInitialization(CacheCatalog catalog)
{
    private readonly ConcurrentDictionary<string, Initialization> states = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes the complete provider once, retaining retryable failure instead of reporting unconfirmed readiness.</summary>
    public async ValueTask<Result> EnsureAsync(ICacheStoreProvider provider, CancellationToken cancellationToken)
    {
        var state = this.states.GetOrAdd(provider.Name, _ => new());
        if (Volatile.Read(ref state.Ready))
        {
            return Result.Success();
        }

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.Ready)
            {
                return Result.Success();
            }

            var definitions = catalog.Collections.Values.Where(c => c.Provider.Name == provider.Name).Select(c =>
                new CacheProviderCollection(catalog.Options.Namespace, c.Name, c.Options.ContractVersion, c.Contract.Alias,
                    c.Options.Dimensions, c.Options)).ToArray();
            var result = await provider.InitializeAsync(definitions, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                Volatile.Write(ref state.Ready, true);
            }

            return result;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private sealed class Initialization
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public bool Ready;
    }
}

// A returned timeout does not release a still occupied native call. Late resource-bearing
// results are disposed by the observed tail, rather than escaping after the caller has left.
/// <summary>Bounds provider work and initialization while retaining actual native-tail accounting after caller timeout.</summary>
/// <example><code>var result = await access.CallAsync(ct =&gt; access.Provider.LookupAsync(identity, new(), ct), token);</code></example>
public sealed class CacheProviderAccess(CacheCollectionBinding binding, ICacheStoreProvider provider,
    CacheRuntimeResources resources, CacheProviderInitialization initialization, CacheRuntimeTasks tasks, TimeProvider time, CacheObservationSlot observations = null)
{
    /// <summary>Gets the complete declared provider without wrapping it in a fallback storage implementation.</summary>
    public ICacheStoreProvider Provider => provider;

    /// <summary>Gets the optional predeclared collection observation slot used by bounded provider and payload work.</summary>
    public CacheObservationSlot Observations => observations;

    /// <summary>Runs one admitted native operation within its attempt budget and observes unfinished native work before releasing its accounts.</summary>
    /// <typeparam name="T">The declared value or operation result type.</typeparam>
    /// <remarks>Enable observeInfrastructureOutcome only for native session results which bypass the provider SPI behavior; guarded SPI results already update the circuit.</remarks>
    public async ValueTask<Result<T>> CallAsync<T>(Func<CancellationToken, ValueTask<Result<T>>> operation,
        CancellationToken cancellationToken, Func<T, ValueTask> discard = null, bool initialize = true,
        Func<ValueTask> notStarted = null, CacheErrorCode timeoutCode = CacheErrorCode.ProviderUnavailable, bool observeInfrastructureOutcome = false)
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(binding.Provider.Kind.Alias, "InMemory"))
        {
            return await this.CallInProcessAsync(operation, cancellationToken, initialize, notStarted).ConfigureAwait(false);
        }

        Result<CacheAdmissionLease> admitted;
        try
        {
            admitted = await resources.ForCollection(binding.Name).ProviderIO.AcquireAsync(binding.Provider.Options.AttemptTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (notStarted is not null)
            {
                await notStarted().ConfigureAwait(false);
            }

            throw;
        }

        if (admitted.IsFailure)
        {
            if (notStarted is not null)
            {
                await notStarted().ConfigureAwait(false);
            }

            return Result<T>.Failure(admitted);
        }

        var timer = new CacheCancellationDeadline(binding.Provider.Options.AttemptTimeout, time, tasks);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        var observer = binding.Provider.Options.Breaker.Enabled && binding.Provider.Capabilities.Persistent ? provider as ICacheProviderInfrastructureObserver : null;
        var circuitEpoch = observer?.CircuitEpoch ?? 0;
        var actual = this.ExecuteAsync(operation, admitted.Value, linked.Token, initialize, notStarted);
        var detached = false;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            var result = actual.IsCompletedSuccessfully ? actual.Result :
                await actual.WaitAsync(linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (observeInfrastructureOutcome && observer is not null)
            {
                try { await observer.ReportInfrastructureOutcomeAsync(circuitEpoch, CacheProviderFailureClassifier.IsInfrastructureFailure(result)).ConfigureAwait(false); }
                catch { /* Optional observation preserves the actual native outcome, including confirmed publication. */ }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            detached = true;
            tasks.Track(ObserveAsync(actual, discard, linked, timer), "provider-tail");
            cancellationToken.ThrowIfCancellationRequested();
            observations?.Record(CacheObservationKind.ProviderFailure);
            if (observer is not null && !(actual.IsCompletedSuccessfully && CacheProviderFailureClassifier.IsInfrastructureFailure(actual.Result)))
            {
                try { await observer.ReportInfrastructureTimeoutAsync(circuitEpoch).ConfigureAwait(false); }
                catch { /* The timed-out native operation retains its existing ownership independently of diagnostics. */ }
            }

            return Result<T>.Failure(new CacheError(timeoutCode, "The bounded native provider attempt did not complete."));
        }
        finally
        {
            if (!detached)
            {
                linked.Dispose();
                timer.Dispose();
            }
        }
    }

    private async ValueTask<Result<T>> CallInProcessAsync<T>(Func<CancellationToken, ValueTask<Result<T>>> operation,
        CancellationToken cancellationToken, bool initialize, Func<ValueTask> notStarted)
    {
        var invoked = false;
        var started = observations?.Enabled == true ? time.GetTimestamp() : 0;
        observations?.Record(CacheObservationKind.ProviderCall);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (initialize)
            {
                var ready = await initialization.EnsureAsync(provider, cancellationToken).ConfigureAwait(false);
                if (ready.IsFailure)
                {
                    return Result<T>.Failure(ready);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            invoked = true;
            var result = await operation(cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested && CacheProviderFailureClassifier.IsInfrastructureFailure(result)) { observations?.Record(CacheObservationKind.ProviderFailure); }

            return result;
        }
        finally
        {
            if (observations?.Enabled == true) { observations.RecordDuration(CacheLatencyKind.Provider, time.GetElapsedTime(started)); }

            if (!invoked && notStarted is not null)
            {
                await notStarted().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Runs one admitted native operation within its attempt budget and observes unfinished native work before releasing its accounts.</summary>
    public async ValueTask<Result> CallAsync(Func<CancellationToken, ValueTask<Result>> operation,
        CancellationToken cancellationToken, bool initialize = true)
    {
        var result = await this.CallAsync(async token =>
        {
            var called = await operation(token).ConfigureAwait(false);
            return called.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure((IResult)called);
        }, cancellationToken, initialize: initialize).ConfigureAwait(false);
        return result.IsSuccess ? Result.Success() : Result.Failure().WithErrors(result.Errors);
    }

    private async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, ValueTask<Result<T>>> operation,
        CacheAdmissionLease admission, CancellationToken cancellationToken, bool initialize, Func<ValueTask> notStarted)
    {
        using (admission)
        {
            var invoked = false;
            var started = observations?.Enabled == true ? time.GetTimestamp() : 0;
            observations?.Record(CacheObservationKind.ProviderCall);
            try
            {
                if (initialize)
                {
                    var ready = await initialization.EnsureAsync(provider, cancellationToken).ConfigureAwait(false);
                    if (ready.IsFailure)
                    {
                        return Result<T>.Failure(ready);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                invoked = true;
                var result = await operation(cancellationToken).ConfigureAwait(false);
                if (!cancellationToken.IsCancellationRequested && CacheProviderFailureClassifier.IsInfrastructureFailure(result)) { observations?.Record(CacheObservationKind.ProviderFailure); }

                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
            {
                if (!cancellationToken.IsCancellationRequested) { observations?.Record(CacheObservationKind.ProviderFailure); }

                return Result<T>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The native cache provider is unavailable."));
            }
            finally
            {
                if (observations?.Enabled == true) { observations.RecordDuration(CacheLatencyKind.Provider, time.GetElapsedTime(started)); }

                if (!invoked && notStarted is not null)
                {
                    await notStarted().ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task ObserveAsync<T>(Task<Result<T>> actual, Func<T, ValueTask> discard,
        CancellationTokenSource linked, CacheCancellationDeadline timer)
    {
        try
        {
            var result = await actual.ConfigureAwait(false);
            if (result.IsSuccess && discard is not null)
            {
                await discard(result.Value).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            linked.Dispose();
            timer.Dispose();
        }
    }
}