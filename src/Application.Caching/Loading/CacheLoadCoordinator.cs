// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Coordinates admitted node-local flights and optional shared-store authority without extending the original owned deadline.</summary>
/// <typeparam name="T">The declared value or operation result type.</typeparam>
/// <example><code>var completion = await coordinator.LoadAsync(item, source, token);</code></example>
public sealed class CacheLoadCoordinator<T>(CacheCollectionRuntime runtime, IServiceScopeFactory scopes,
    Func<CacheItem, CacheOwnershipObservation, CancellationToken, ValueTask<Result<T>>> resolveRemote,
    IReadOnlyList<ICacheLoadBehavior> behaviors = null,
    Func<CacheItem, CancellationToken, ValueTask<Result<T>>> recheck = null,
    Func<CacheItem, CacheOwnershipObservation, CancellationToken, ValueTask<Result<T>>> resolveBackground = null,
    Func<CacheItem, CancellationToken, ValueTask<Result<T>>> recheckBackground = null, CacheObservationSlot observations = null) where T : ICacheSourceReceipt
{
    private const int MaximumUnretainedHandoffs = 32;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Flight> flights = new(StringComparer.Ordinal);
    private readonly string node = Guid.NewGuid().ToString("N");
    private readonly CacheLoadPipeline<T> loadPipeline = behaviors?.Count > 0 ? new(behaviors, runtime.Tasks) : null;
    private readonly CacheLoadPipeline<bool> batchPipeline = behaviors?.Count > 0 ? new(behaviors, runtime.Tasks) : null;

    /// <summary>Joins or owns one bounded same-key flight and returns the caller&apos;s result independently of other interested waiters.</summary>
    public async ValueTask<Result<CacheFlightCompletion<T>>> LoadAsync(CacheItem item,
        Func<CacheOwnedLoadContext, CancellationToken, ValueTask<Result<T>>> work, CancellationToken cancellationToken, bool recheckCache = true, bool background = false)
    {
        if (CacheLoadExecutionContext.IsLoading)
        {
            return Result<CacheFlightCompletion<T>>.Failure(new CacheError(CacheErrorCode.NestedCacheLoad,
                "An executing cache loader cannot start or join nested source work."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var registered = this.Register(item, recheckCache, background);
        if (registered.IsFailure)
        {
            return Result<CacheFlightCompletion<T>>.Failure(registered);
        }

        if (registered.Value.Joined) { observations?.Record(CacheObservationKind.Join); }

        if (!registered.Value.Joined)
        {
            runtime.Tasks.Track(this.RunAsync(registered.Value.Flight, work), "source-work");
        }

        return await this.WaitAsync(registered.Value, cancellationToken).ConfigureAwait(false);
    }

    private Result<Registration> Register(CacheItem item, bool recheckCache = true, bool background = false)
    {
        if (runtime.Tasks.Stopping.IsCancellationRequested)
        {
            return Result<Registration>.Failure(new CacheError(CacheErrorCode.SourceRejected, "The cache host is stopping."));
        }

        var identity = runtime.Identity(item.Key);
        var independent = runtime.Binding.Options.Loading.Strategy == CacheLoadingStrategy.Independent;
        var key = independent ? Guid.NewGuid().ToString("N") : identity.Canonical;
        Flight flight;
        var joined = false;
        lock (this.gate)
        {
            if (this.flights.TryGetValue(key, out var completed) && completed.Completion.Task.IsCompletedSuccessfully &&
                completed.Completion.Task.Result.IsSuccess)
            {
                this.flights.Remove(key);
            }

            if (this.flights.TryGetValue(key, out flight))
            {
                joined = true;
                if (flight.Waiters >= runtime.Binding.Options.Limits.MaxFollowersPerKey)
                {
                    return Result<Registration>.Failure(new CacheError(CacheErrorCode.Capacity, "The source flight's bounded waiter limit was reached."));
                }

                // A source-required participant cannot be satisfied by an owner's later cache recheck.
                flight.RecheckCache &= recheckCache;
            }
            else
            {
                var limits = runtime.Binding.Options.Limits;
                if (this.flights.Count >= Math.Max(limits.MaxBatchSize, limits.MaxActiveSources + limits.MaxQueuedSources) ||
                    !runtime.CollectionResources.Resident.TryReserve(Math.Max(128 * CacheDefaults.KiB,
                        CacheSizePolicy.EstimateItemResidentBytes(item)), out var memory))
                {
                    return Result<Registration>.Failure(new CacheError(CacheErrorCode.Capacity, "Source flight metadata admission was rejected."));
                }

                flight = new(key, item, identity, memory, runtime.Time, runtime.Binding.Options.Loading.SourceTimeout, runtime.Tasks);
                flight.Background = background;
                flight.RecheckCache = recheckCache && runtime.Enabled && !independent && recheck is not null;
                this.flights.Add(key, flight);
            }

            flight.Waiters++;
        }

        return Result<Registration>.Success(new(flight, joined));
    }

    private async Task<Result<CacheFlightCompletion<T>>> WaitAsync(Registration registration, CancellationToken cancellationToken)
    {
        var flight = registration.Flight;
        var joined = registration.Joined;
        var waitStarted = runtime.Time.GetTimestamp();
        observations?.ChangeActive(CacheActiveKind.Waiters, 1);
        try
        {
            var result = await flight.Completion.Task.WaitAsync(runtime.Binding.Options.Loading.WaitTimeout, runtime.Time,
                cancellationToken).ConfigureAwait(false);
            observations?.Record(CacheObservationKind.WaitCompleted);
            return result.IsSuccess ? Result<CacheFlightCompletion<T>>.Success(new(result.Value, joined || flight.JoinedRemote)) : Result<CacheFlightCompletion<T>>.Failure(result);
        }
        catch (TimeoutException)
        {
            observations?.Record(CacheObservationKind.WaitTimeout);
            return Result<CacheFlightCompletion<T>>.Failure(new CacheError(CacheErrorCode.CoordinationTimeout, "The caller's source wait budget expired."));
        }
        catch (OperationCanceledException)
        {
            observations?.Record(CacheObservationKind.Cancelled);
            throw;
        }
        finally
        {
            observations?.RecordDuration(CacheLatencyKind.Wait, runtime.Time.GetElapsedTime(waitStarted));
            observations?.ChangeActive(CacheActiveKind.Waiters, -1);
            var cancel = false;
            var withdraw = false;
            lock (this.gate)
            {
                flight.Waiters--;
                withdraw = flight.Waiters == 0 && runtime.Enabled && runtime.Binding.Options.Loading.CoordinationScope == CacheCoordinationScope.SharedStore &&
                    !flight.Completion.Task.IsCompleted;
                cancel = !withdraw && flight.Waiters == 0 && !runtime.Binding.Options.Loading.ContinueWithoutWaiters && !flight.Completion.Task.IsCompleted;
                if (withdraw && this.flights.TryGetValue(flight.Key, out var current) && ReferenceEquals(current, flight))
                {
                    // A later local caller registers a new native observation group. It must not revive released interest on this group.
                    this.flights.Remove(flight.Key);
                }

                if (cancel)
                {
                    flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceRejected, "The last interested source caller left.")));
                }
            }

            if (cancel)
            {
                flight.Cancel();
            }

            if (withdraw)
            {
                runtime.Tasks.Track(this.WithdrawSharedInterestAsync(flight), "source-interest-withdrawal");
            }
        }
    }

    private readonly record struct Registration(Flight Flight, bool Joined);

    private async Task RunAsync(Flight flight, Func<CacheOwnedLoadContext, CancellationToken, ValueTask<Result<T>>> work)
    {
        CacheAdmissionLease backgroundAdmission = null;
        CachePublicationFence? fence = null;
        CacheOwnerToken? owner = null;
        string remoteFlight = null;
        var token = flight.Cancellation.Token;
        var authorityState = flight.Authority;
        var sharedCompleted = false;
        var observedOwner = false;
        Task renewal = null;
        using var renewStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var deadlineRegistration = token.Register(() =>
        {
            observations?.Record(runtime.Time.GetUtcNow() >= flight.Deadline ? CacheObservationKind.SourceTimeout : CacheObservationKind.Cancelled);
            flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The owned source execution ended before completion.")));
        });
        try
        {
            if (runtime.Enabled && runtime.Binding.Options.Loading.CoordinationScope == CacheCoordinationScope.SharedStore)
            {
                while (true)
                {
                    var authority = await this.AcquireSharedAsync(flight, token).ConfigureAwait(false);
                    if (authority.IsFailure)
                    {
                        flight.Completion.TrySetResult(Result<T>.Failure(authority));
                        return;
                    }

                    remoteFlight = authority.Value.FlightId;
                    await this.AttachSharedAsync(flight, authority.Value).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (authority.Value.State == CacheOwnershipState.Owned)
                    {
                        fence = authority.Value.Fence;
                        owner = authority.Value.Owner;
                        renewal = this.RenewSharedAsync(flight, owner.Value, authorityState, renewStop.Token);
                        break;
                    }

                    flight.JoinedRemote = true;
                    observations?.Record(CacheObservationKind.Join);
                    var completed = await this.ObserveSharedAsync(flight, authority.Value, token).ConfigureAwait(false);
                    if (completed.HasValue)
                    {
                        flight.Completion.TrySetResult(completed.Value);
                        return;
                    }

                    var retained = await this.RecheckAsync(flight, token).ConfigureAwait(false);
                    if (retained.HasValue && this.TryCompleteRecheck(flight, retained.Value))
                    {
                        return;
                    }

                    var released = await this.ReleaseHandoffInterestAsync(flight, remoteFlight, token).ConfigureAwait(false);
                    if (released.IsFailure)
                    {
                        flight.Completion.TrySetResult(Result<T>.Failure(released));
                        return;
                    }

                    remoteFlight = null;
                    // Reuse this node flight, its waiters, admission and fixed deadline. Only native authority is reacquired.
                }
            }

            var queueStarted = runtime.Time.GetTimestamp();
            if (flight.Background)
            {
                var background = await runtime.CollectionResources.RefreshSources.AcquireAsync(flight.Deadline - runtime.Time.GetUtcNow(), token).ConfigureAwait(false);
                if (background.IsFailure)
                {
                    flight.Completion.TrySetResult(Result<T>.Failure(background));
                    return;
                }

                backgroundAdmission = background.Value;
            }

            var admitted = await runtime.CollectionResources.Sources.AcquireAsync(runtime.Binding.Options.Loading.SourceTimeout,
                token).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                flight.Completion.TrySetResult(Result<T>.Failure(admitted));
                return;
            }

            using var sourceAdmission = admitted.Value;
            observations?.RecordDuration(CacheLatencyKind.Queue, runtime.Time.GetElapsedTime(queueStarted));
            observations?.ChangeActive(CacheActiveKind.Owners, 1);
            if (flight.Background) { observations?.ChangeActive(CacheActiveKind.Refresh, 1); }

            observedOwner = true;
            if (runtime.Enabled && !fence.HasValue)
            {
                var captured = await runtime.Access.CallAsync(ct => runtime.Access.Provider.CaptureFenceAsync(flight.Identity, ct), token,
                    async late => { await runtime.Access.Provider.ReleaseFenceAsync(flight.Identity, late).ConfigureAwait(false); }).ConfigureAwait(false);
                if (captured.IsSuccess)
                {
                    fence = captured.Value;
                }
                else if (runtime.Binding.Contract.IsBinary || !captured.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.ProviderUnavailable))
                {
                    flight.Completion.TrySetResult(Result<T>.Failure(captured));
                    return;
                }
            }

            var acquired = await this.RecheckAsync(flight, token).ConfigureAwait(false);
            if (acquired.HasValue && this.TryCompleteRecheck(flight, acquired.Value))
            {
                if (owner.HasValue)
                {
                    sharedCompleted = true;
                    await this.CompleteSharedAsync(flight, owner.Value, acquired.Value, token).ConfigureAwait(false);
                }

                return;
            }

            await using var scope = scopes.CreateAsyncScope();
            var context = new CacheOwnedLoadContext(runtime, flight.Item, scope.ServiceProvider, flight.Deadline, fence, owner, authorityState, token);
            var result = await CacheLoadExecutionContext.InvokeAsync(() => this.loadPipeline is null ? work(context, token) :
                this.loadPipeline.ExecuteAsync(new(runtime.Binding, [context.Public]), ct => work(context, ct), token)).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (owner.HasValue)
            {
                sharedCompleted = true;
                await this.CompleteSharedAsync(flight, owner.Value, result, token).ConfigureAwait(false);
            }

            flight.Completion.TrySetResult(result);
        }
        catch (OperationCanceledException)
        {
            flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The owned source execution exceeded its deadline or lost interest.")));
        }
        catch (Exception)
        {
            flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceFailure, "The owned cache source did not complete.")));
        }
        finally
        {
            renewStop.Cancel();
            if (renewal is not null)
            {
                try { await renewal.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }

            if (owner.HasValue && !sharedCompleted)
            {
                await this.SafeCleanupAsync(() => runtime.Access.CallAsync(ct => runtime.Access.Provider.CompleteOwnershipAsync(flight.Identity, owner.Value,
                    new(CacheOwnershipState.Lost), ct), CancellationToken.None)).ConfigureAwait(false);
            }

            if (fence.HasValue)
            {
                await this.SafeCleanupAsync(() => runtime.Access.CallAsync(ct => runtime.Access.Provider.ReleaseFenceAsync(flight.Identity, fence.Value, ct),
                    CancellationToken.None)).ConfigureAwait(false);
            }

            if (remoteFlight is not null)
            {
                await this.SafeCleanupAsync(() => runtime.Access.CallAsync(ct => runtime.Access.Provider.ReleaseInterestAsync(flight.Identity, remoteFlight,
                    flight.Interest, ct), CancellationToken.None)).ConfigureAwait(false);
            }

            Task withdrawal;
            lock (this.gate) { withdrawal = flight.InterestWithdrawal; }

            if (withdrawal is not null) { await withdrawal.ConfigureAwait(false); }

            lock (this.gate)
            {
                if (this.flights.TryGetValue(flight.Key, out var registered) && ReferenceEquals(registered, flight))
                {
                    this.flights.Remove(flight.Key);
                }
            }

            backgroundAdmission?.Dispose();
            if (observedOwner)
            {
                observations?.ChangeActive(CacheActiveKind.Owners, -1);
                if (flight.Background) { observations?.ChangeActive(CacheActiveKind.Refresh, -1); }
            }

            flight.Dispose();
        }
    }

    private sealed class Flight : IDisposable
    {
        private readonly ResourceReservation memory;
        private readonly CancellationTokenRegistration hostStop;

        public Flight(string key, CacheItem item, CacheEntryIdentity identity, ResourceReservation memory, TimeProvider time, TimeSpan timeout, CacheRuntimeTasks tasks)
        {
            this.Key = key;
            this.Item = item;
            this.Identity = identity;
            this.memory = memory;
            this.Deadline = time.GetUtcNow().Add(timeout);
            this.Cancellation = new(timeout, time, tasks);
            this.hostStop = tasks.Stopping.UnsafeRegister(static state => ((Flight)state).Cancel(), this);
        }

        public string Key { get; }

        public CacheItem Item { get; }

        public CacheEntryIdentity Identity { get; }

        public string Interest { get; } = Guid.NewGuid().ToString("N");

        public DateTimeOffset Deadline { get; }

        public CacheCancellationDeadline Cancellation { get; }

        public TaskCompletionSource<Result<T>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Waiters { get; set; }

        public bool JoinedRemote { get; set; }

        public int UnretainedHandoffs { get; set; }

        public bool RecheckCache { get; set; }

        public bool Background { get; set; }

        public string NativeFlight { get; set; }

        public CacheOwnerToken? NativeOwner { get; set; }

        public CacheOwnedAuthority Authority { get; } = new();

        public Task InterestWithdrawal { get; set; }

        public CacheError BatchError { get; set; }

        public void Cancel()
        {
            this.Cancellation.Cancel();
        }

        public void Dispose()
        {
            this.hostStop.Dispose();
            this.Cancellation.Dispose();
            this.memory.Dispose();
        }
    }

    /// <summary>Admits an ordered bounded batch while preserving one flight per key, per-item authority and partial outcomes.</summary>
    public async ValueTask<Result<IReadOnlyList<Result<CacheFlightCompletion<T>>>>> LoadBatchAsync(IReadOnlyList<CacheItem> items,
        Func<IReadOnlyList<CacheOwnedLoadContext>, Func<string, Result<T>, ValueTask<Result>>, CancellationToken, ValueTask<Result>> work,
        CancellationToken cancellationToken)
    {
        if (CacheLoadExecutionContext.IsLoading)
        {
            return Result<IReadOnlyList<Result<CacheFlightCompletion<T>>>>.Failure(new CacheError(CacheErrorCode.NestedCacheLoad,
                "An executing loader cannot acquire or join batch source ownership."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Reserve every local miss before starting a callback or awaiting a foreign flight.
        var registrations = items.Select(item => this.Register(item)).ToArray();
        var owned = registrations.Where(r => r.IsSuccess && !r.Value.Joined).Select(r => r.Value.Flight).ToArray();
        var groups = owned.Chunk(runtime.Binding.Options.Limits.BatchSize).Select(group => this.RunBatchAsync(group, work)).ToArray();
        foreach (var group in groups)
        {
            runtime.Tasks.Track(group, "batch-source-work");
        }

        var waits = registrations.Select(r => r.IsSuccess ? this.WaitAsync(r.Value, cancellationToken) :
            Task.FromResult(Result<CacheFlightCompletion<T>>.Failure(r))).ToArray();
        var completedWaits = Task.WhenAll(waits);
        try
        {
            // Owned callbacks publish promptly and finish before this request joins foreign work.
            await Task.WhenAll(groups).WaitAsync(runtime.Binding.Options.Loading.WaitTimeout, runtime.Time, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException) { }
        catch (OperationCanceledException)
        {
            runtime.Tasks.Track(completedWaits, "batch-waiters");
            throw;
        }

        var results = await completedWaits.ConfigureAwait(false);
        for (var index = 0; index < registrations.Length; index++)
        {
            if (registrations[index].IsSuccess && !registrations[index].Value.Joined && registrations[index].Value.Flight.BatchError is { } error)
            {
                results[index] = Result<CacheFlightCompletion<T>>.Failure(error);
            }
        }

        return Result<IReadOnlyList<Result<CacheFlightCompletion<T>>>>.Success(Array.AsReadOnly(results));
    }

    private async Task RunBatchAsync(Flight[] owned,
        Func<IReadOnlyList<CacheOwnedLoadContext>, Func<string, Result<T>, ValueTask<Result>>, CancellationToken, ValueTask<Result>> work)
    {
        var members = owned.Select(f => new BatchMember(f)).ToArray();
        using var deadline = new CacheCancellationDeadline(Positive(owned.Min(f => f.Deadline) - runtime.Time.GetUtcNow()), runtime.Time, runtime.Tasks);
        var registrations = members.Select(member => member.Flight.Cancellation.Token.Register(() =>
        {
            member.Flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The owned batch item ended before completion.")));
            if (members.All(m => m.Flight.Cancellation.IsCancellationRequested))
            {
                deadline.Cancel();
            }
        })).ToArray();
        var observers = new List<Task>();
        var ownedCallbacksFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            foreach (var member in members)
            {
                try
                {
                    if (member.Flight.Cancellation.IsCancellationRequested)
                    {
                        continue;
                    }

                    if (runtime.Enabled && runtime.Binding.Options.Loading.CoordinationScope == CacheCoordinationScope.SharedStore)
                    {
                        var acquired = await this.AcquireSharedAsync(member.Flight, member.Flight.Cancellation.Token).ConfigureAwait(false);
                        if (acquired.IsFailure)
                        {
                            member.Flight.Completion.TrySetResult(Result<T>.Failure(acquired));
                            continue;
                        }

                        member.RemoteFlight = acquired.Value.FlightId;
                        await this.AttachSharedAsync(member.Flight, acquired.Value).ConfigureAwait(false);
                        member.Flight.Cancellation.Token.ThrowIfCancellationRequested();
                        if (acquired.Value.State != CacheOwnershipState.Owned)
                        {
                            member.Flight.JoinedRemote = true;
                            member.Observed = true;
                            var observer = this.ObserveBatchMemberAsync(member, acquired.Value, ownedCallbacksFinished.Task, work);
                            observers.Add(observer);
                            runtime.Tasks.Track(observer, "batch-remote-observer");
                            continue;
                        }

                        member.Fence = acquired.Value.Fence;
                        member.Owner = acquired.Value.Owner;
                        member.Renewal = this.RenewSharedAsync(member.Flight, member.Owner.Value, member.Authority, member.RenewStop.Token);
                    }
                    else if (runtime.Enabled)
                    {
                        var captured = await runtime.Access.CallAsync(token => runtime.Access.Provider.CaptureFenceAsync(member.Flight.Identity, token),
                            member.Flight.Cancellation.Token, async late => { await runtime.Access.Provider.ReleaseFenceAsync(member.Flight.Identity, late).ConfigureAwait(false); }).ConfigureAwait(false);
                        if (captured.IsSuccess)
                        {
                            member.Fence = captured.Value;
                        }
                        else if (runtime.Binding.Contract.IsBinary || !captured.Errors.OfType<CacheError>().Any(e => e.Code == CacheErrorCode.ProviderUnavailable))
                        {
                            member.Flight.Completion.TrySetResult(Result<T>.Failure(captured));
                            continue;
                        }
                    }

                    member.Prepared = true;
                }
                catch (OperationCanceledException) when (member.Flight.Cancellation.IsCancellationRequested && !deadline.IsCancellationRequested)
                {
                    // Other interested keys keep their original owned batch work.
                }
            }

            var active = members.Where(m => m.Prepared && !m.Flight.Cancellation.IsCancellationRequested).ToArray();
            if (active.Length == 0)
            {
                return;
            }

            var admitted = await runtime.CollectionResources.Sources.AcquireAsync(runtime.Binding.Options.Loading.SourceTimeout, deadline.Token).ConfigureAwait(false);
            if (admitted.IsFailure)
            {
                foreach (var member in active)
                {
                    member.Flight.Completion.TrySetResult(Result<T>.Failure(admitted));
                }

                return;
            }

            using var sourceAdmission = admitted.Value;
            foreach (var member in active)
            {
                var acquired = await this.RecheckAsync(member.Flight, member.Flight.Cancellation.Token).ConfigureAwait(false);
                if (!acquired.HasValue || !this.TryCompleteRecheck(member.Flight, acquired.Value)) { continue; }

                member.Completed = true;
                if (member.Owner.HasValue)
                {
                    member.SharedCompleted = true;
                    await this.CompleteSharedAsync(member.Flight, member.Owner.Value, acquired.Value, member.Flight.Cancellation.Token).ConfigureAwait(false);
                }
            }

            active = active.Where(m => !m.Completed && !m.Flight.Cancellation.IsCancellationRequested).ToArray();
            if (active.Length == 0) { return; }

            await using var scope = scopes.CreateAsyncScope();
            var contexts = active.Select(member => new CacheOwnedLoadContext(runtime, member.Flight.Item, scope.ServiceProvider,
                member.Flight.Deadline, member.Fence, member.Owner, member.Authority, member.Flight.Cancellation.Token)).ToArray();
            var byKey = active.ToDictionary(m => m.Flight.Identity.Key, StringComparer.Ordinal);
            Result executed;
            if (this.batchPipeline is null)
            {
                executed = await CacheLoadExecutionContext.InvokeAsync(() => work(Array.AsReadOnly(contexts), CompleteAsync, deadline.Token)).ConfigureAwait(false);
            }
            else
            {
                var observed = await CacheLoadExecutionContext.InvokeAsync(() => this.batchPipeline.ExecuteAsync(new(runtime.Binding, Array.AsReadOnly(contexts.Select(c => c.Public).ToArray())),
                    async token =>
                    {
                        var result = await work(Array.AsReadOnly(contexts), CompleteAsync, token).ConfigureAwait(false);
                        return result.IsSuccess ? Result<bool>.Success(true) : Result<bool>.Failure((IResult)result);
                    }, deadline.Token)).ConfigureAwait(false);
                executed = observed.IsSuccess ? Result.Success() : Result.Failure().WithErrors(observed.Errors);
            }

            foreach (var member in active.Where(m => !m.Completed))
            {
                member.Flight.Completion.TrySetResult(executed.IsFailure ? Result<T>.Failure(executed) : Result<T>.Failure(new CacheError(
                    CacheErrorCode.Incomplete, "The batch loader omitted this owned key.")));
            }

            async ValueTask<Result> CompleteAsync(string key, Result<T> result)
            {
                BatchMember member;
                try
                {
                    if (!byKey.TryGetValue(CacheIdentityNormalizer.Key(key), out member))
                    {
                        active[0].Flight.BatchError = new(CacheErrorCode.Contract, "The batch loader returned an unknown key.");
                        return Result.Failure(active[0].Flight.BatchError);
                    }
                }
                catch (ArgumentException)
                {
                    active[0].Flight.BatchError = new(CacheErrorCode.Contract, "The batch loader returned an invalid key.");
                    return Result.Failure(active[0].Flight.BatchError);
                }

                if (member.Completed)
                {
                    member.Flight.BatchError = new(CacheErrorCode.Contract, "The batch loader returned a duplicate key.");
                    return Result.Failure(member.Flight.BatchError);
                }

                member.Completed = true;
                if (member.Flight.Cancellation.IsCancellationRequested)
                {
                    return Result.Success();
                }

                if (member.Owner.HasValue)
                {
                    member.SharedCompleted = true;
                    try
                    {
                        await this.CompleteSharedAsync(member.Flight, member.Owner.Value, result, member.Flight.Cancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (member.Flight.Cancellation.IsCancellationRequested && !deadline.IsCancellationRequested)
                    {
                        return Result.Success();
                    }
                }

                member.Flight.Completion.TrySetResult(result);
                return Result.Success();
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var member in members.Where(m => !m.Observed))
            {
                member.Flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The owned batch deadline or interested callers ended.")));
            }
        }
        catch (Exception)
        {
            foreach (var member in members.Where(m => !m.Observed))
            {
                member.Flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceFailure, "The repeatable batch source failed.")));
            }
        }
        finally
        {
            ownedCallbacksFinished.TrySetResult();
            for (var index = 0; index < members.Length; index++)
            {
                registrations[index].Dispose();
                if (!members[index].Observed)
                {
                    await this.CleanupBatchMemberAsync(members[index]).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task ObserveBatchMemberAsync(BatchMember member, CacheOwnershipObservation observation, Task ownedCallbacksFinished,
        Func<IReadOnlyList<CacheOwnedLoadContext>, Func<string, Result<T>, ValueTask<Result>>, CancellationToken, ValueTask<Result>> work)
    {
        var handedOff = false;
        try
        {
            var token = member.Flight.Cancellation.Token;
            var completed = await this.ObserveSharedAsync(member.Flight, observation, token).ConfigureAwait(false);
            if (completed.HasValue)
            {
                member.Flight.Completion.TrySetResult(completed.Value);
                return;
            }

            var retained = await this.RecheckAsync(member.Flight, token).ConfigureAwait(false);
            if (retained.HasValue && this.TryCompleteRecheck(member.Flight, retained.Value))
            {
                return;
            }

            var released = await this.ReleaseHandoffInterestAsync(member.Flight, member.RemoteFlight, token).ConfigureAwait(false);
            if (released.IsFailure)
            {
                member.Flight.Completion.TrySetResult(Result<T>.Failure(released));
                return;
            }

            member.RemoteFlight = null;
            await ownedCallbacksFinished.WaitAsync(token).ConfigureAwait(false);
            handedOff = true;
            // Foreign observation never delays the original batch's owned callbacks. A handed-off key reuses its existing flight.
            await this.RunAsync(member.Flight, async (context, owned) =>
            {
                Result<T>? result = null;
                var executed = await work(Array.AsReadOnly(new[] { context }), CompleteAsync, owned).ConfigureAwait(false);
                return result ?? (executed.IsFailure ? Result<T>.Failure(executed) : Result<T>.Failure(new CacheError(
                    CacheErrorCode.Incomplete, "The batch loader omitted the handed-off owned key.")));

                ValueTask<Result> CompleteAsync(string key, Result<T> value)
                {
                    if (!string.Equals(CacheIdentityNormalizer.Key(key), member.Flight.Identity.Key, StringComparison.Ordinal) || result.HasValue)
                    {
                        member.Flight.BatchError = new(CacheErrorCode.Contract, "The batch loader returned an unknown or duplicate handed-off key.");
                        return ValueTask.FromResult(Result.Failure(member.Flight.BatchError));
                    }

                    result = value;
                    return ValueTask.FromResult(Result.Success());
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            member.Flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.SourceTimeout, "The shared batch observation ended.")));
        }
        catch (Exception)
        {
            member.Flight.Completion.TrySetResult(Result<T>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The shared batch completion is unavailable.")));
        }
        finally
        {
            if (handedOff) { member.RenewStop.Dispose(); }
            else { await this.CleanupBatchMemberAsync(member).ConfigureAwait(false); }
        }
    }

    private async ValueTask CleanupBatchMemberAsync(BatchMember member)
    {
        member.RenewStop.Cancel();
        if (member.Renewal is not null)
        {
            try { await member.Renewal.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        if (member.Owner.HasValue && !member.SharedCompleted)
        {
            await this.SafeCleanupAsync(() => runtime.Access.CallAsync(token => runtime.Access.Provider.CompleteOwnershipAsync(member.Flight.Identity,
                member.Owner.Value, new(CacheOwnershipState.Lost), token), CancellationToken.None)).ConfigureAwait(false);
        }

        if (member.Fence.HasValue)
        {
            await this.SafeCleanupAsync(() => runtime.Access.CallAsync(token => runtime.Access.Provider.ReleaseFenceAsync(member.Flight.Identity,
                member.Fence.Value, token), CancellationToken.None)).ConfigureAwait(false);
        }

        if (member.RemoteFlight is not null)
        {
            await this.SafeCleanupAsync(() => runtime.Access.CallAsync(token => runtime.Access.Provider.ReleaseInterestAsync(member.Flight.Identity,
                member.RemoteFlight, member.Flight.Interest, token), CancellationToken.None)).ConfigureAwait(false);
        }

        Task withdrawal;
        lock (this.gate) { withdrawal = member.Flight.InterestWithdrawal; }

        if (withdrawal is not null) { await withdrawal.ConfigureAwait(false); }

        lock (this.gate)
        {
            if (this.flights.TryGetValue(member.Flight.Key, out var registered) && ReferenceEquals(registered, member.Flight))
            {
                this.flights.Remove(member.Flight.Key);
            }
        }

        member.RenewStop.Dispose();
        member.Flight.Dispose();
    }

    private static TimeSpan Positive(TimeSpan value) => value > TimeSpan.Zero ? value : TimeSpan.FromTicks(1);

    private sealed class BatchMember
    {
        public BatchMember(Flight flight)
        {
            this.Flight = flight;
            this.RenewStop = CancellationTokenSource.CreateLinkedTokenSource(flight.Cancellation.Token);
        }

        public Flight Flight { get; }

        public CachePublicationFence? Fence { get; set; }

        public CacheOwnerToken? Owner { get; set; }

        public string RemoteFlight { get; set; }

        public CacheOwnedAuthority Authority => this.Flight.Authority;

        public CancellationTokenSource RenewStop { get; }

        public Task Renewal { get; set; }

        public bool Prepared { get; set; }

        public bool Observed { get; set; }

        public bool Completed { get; set; }

        public bool SharedCompleted { get; set; }
    }

    private async ValueTask<Result<T>?> RecheckAsync(Flight flight, CancellationToken cancellationToken)
    {
        lock (this.gate) { if (!flight.RecheckCache) { return null; } }

        var result = await (flight.Background && recheckBackground is not null ? recheckBackground : recheck)(flight.Item, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure && result.Errors.All(e => e is CacheError { Code: CacheErrorCode.NotFound })) { return null; }

        return result;
    }

    private bool TryCompleteRecheck(Flight flight, Result<T> result)
    {
        lock (this.gate)
        {
            // Registration and completion share a boundary, including joins during an asynchronous recheck.
            if (!flight.RecheckCache) { return false; }

            flight.Completion.TrySetResult(result);
            return true;
        }
    }

    private Task AttachSharedAsync(Flight flight, CacheOwnershipObservation observation)
    {
        bool withdraw;
        lock (this.gate)
        {
            flight.NativeFlight = observation.FlightId;
            flight.NativeOwner = observation.Owner;
            withdraw = flight.Waiters == 0;
        }

        return withdraw ? this.WithdrawSharedInterestAsync(flight) : Task.CompletedTask;
    }

    private Task WithdrawSharedInterestAsync(Flight flight)
    {
        TaskCompletionSource completion;
        lock (this.gate)
        {
            if (flight.NativeFlight is null) { return Task.CompletedTask; }

            if (flight.InterestWithdrawal is not null) { return flight.InterestWithdrawal; }

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            flight.InterestWithdrawal = completion.Task;
        }

        _ = ExecuteAsync();
        return completion.Task;

        async Task ExecuteAsync()
        {
            try
            {
                var released = await runtime.Access.CallAsync(token => runtime.Access.Provider.ReleaseInterestAsync(flight.Identity, flight.NativeFlight,
                    flight.Interest, token), CancellationToken.None).ConfigureAwait(false);
                if (released.IsFailure) { throw new InvalidOperationException("Shared source interest release was not confirmed."); }

                if (!flight.NativeOwner.HasValue) { flight.Cancel(); }
                else
                {
                    var renewed = await runtime.Access.CallAsync(token => runtime.Access.Provider.RenewOwnershipAsync(flight.Identity, flight.NativeOwner.Value, token),
                        CancellationToken.None).ConfigureAwait(false);
                    if (renewed.IsFailure || renewed.Value.State != CacheOwnershipState.Owned || renewed.Value.Owner != flight.NativeOwner)
                    {
                        flight.Authority.Lose();
                        flight.Cancel();
                    }
                }

                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                flight.Authority.Lose();
                flight.Cancel();
                runtime.Tasks.Track(Task.FromException(exception), "source-interest-withdrawal-failure");
                completion.TrySetResult();
            }
        }
    }

    private CacheOwnershipRequest OwnershipRequest(Flight flight) => new(flight.Identity, this.node, flight.Interest,
        flight.Deadline, runtime.Binding.Provider.Options.LeaseLifetime,
        runtime.Binding.Options.Loading.Strategy == CacheLoadingStrategy.Independent);

    private async ValueTask<Result<CacheOwnershipObservation>> AcquireSharedAsync(Flight flight, CancellationToken cancellationToken)
    {
        var result = await runtime.Access.CallAsync(token => runtime.Access.Provider.AcquireOwnershipAsync(this.OwnershipRequest(flight), token),
            cancellationToken, async late =>
            {
                if (late.Owner.HasValue)
                {
                    await runtime.Access.Provider.CompleteOwnershipAsync(flight.Identity, late.Owner.Value, new(CacheOwnershipState.Lost)).ConfigureAwait(false);
                    await runtime.Access.Provider.ReleaseFenceAsync(flight.Identity, late.Fence).ConfigureAwait(false);
                }

                await runtime.Access.Provider.ReleaseInterestAsync(flight.Identity, late.FlightId, flight.Interest).ConfigureAwait(false);
            }).ConfigureAwait(false);
        if (result.IsFailure || result.Value is null || string.IsNullOrEmpty(result.Value.FlightId) ||
            (result.Value.State == CacheOwnershipState.Owned && !result.Value.Owner.HasValue))
        {
            return Result<CacheOwnershipObservation>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable,
                "Shared-store source authority could not be confirmed."));
        }

        return result;
    }

    private async ValueTask<Result<T>?> ObserveSharedAsync(Flight flight, CacheOwnershipObservation observation,
        CancellationToken cancellationToken)
    {
        var delay = runtime.Binding.Options.Loading.ObservationMinimumDelay;
        var attempt = 0;
        while (observation.State == CacheOwnershipState.Pending)
        {
            await Task.Delay(this.ObservationDelay(flight, delay, attempt++), runtime.Time, cancellationToken).ConfigureAwait(false);
            var observed = await runtime.Access.CallAsync(token => runtime.Access.Provider.ObserveOwnershipAsync(
                this.OwnershipRequest(flight), observation.FlightId, token), cancellationToken).ConfigureAwait(false);
            if (observed.IsFailure)
            {
                return Result<T>.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "Shared source completion authority is unavailable."));
            }

            observation = observed.Value;
            var maximum = runtime.Binding.Options.Loading.ObservationMaximumDelay.Ticks;
            delay = TimeSpan.FromTicks(delay.Ticks > maximum / 2 ? maximum : delay.Ticks * 2);
        }

        if (observation.State == CacheOwnershipState.Unretained && (!runtime.Binding.Contract.IsBinary || !observation.IsNotFound))
        {
            // Recheck completions transfer no source result. Only fresh binary source absence can be delivered without publication.
            if (++flight.UnretainedHandoffs <= MaximumUnretainedHandoffs) { return null; }

            return Result<T>.Failure(new CacheError(CacheErrorCode.CoordinationTimeout,
                "The bounded successful-unretained handoff attempts were exhausted."));
        }

        if (observation.State is CacheOwnershipState.Published or CacheOwnershipState.Unretained)
        {
            return await (flight.Background && resolveBackground is not null ? resolveBackground : resolveRemote)(flight.Item, observation, cancellationToken).ConfigureAwait(false);
        }

        return Result<T>.Failure(new CacheError(observation.Failure ?? (observation.State == CacheOwnershipState.Lost ?
            CacheErrorCode.ProviderUnavailable : CacheErrorCode.SourceFailure), "The shared source flight did not publish a usable result."));
    }

    private async ValueTask<Result> ReleaseHandoffInterestAsync(Flight flight, string nativeFlight, CancellationToken cancellationToken)
    {
        var released = await runtime.Access.CallAsync(token => runtime.Access.Provider.ReleaseInterestAsync(flight.Identity, nativeFlight,
            flight.Interest, token), cancellationToken).ConfigureAwait(false);
        if (released.IsFailure)
        {
            return Result.Failure(new CacheError(CacheErrorCode.ProviderUnavailable, "The previous successful-unretained interest could not be released authoritatively."));
        }

        lock (this.gate)
        {
            if (flight.InterestWithdrawal is not null) { flight.Cancel(); }

            flight.NativeFlight = null;
            flight.NativeOwner = null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Result.Success();
    }

    private TimeSpan ObservationDelay(Flight flight, TimeSpan ceiling, int attempt)
    {
        var lower = Math.Max(runtime.Binding.Options.Loading.ObservationMinimumDelay.Ticks, ceiling.Ticks - ceiling.Ticks / 4);
        var width = ceiling.Ticks - lower;
        if (width == 0) { return ceiling; }

        // One node/flight observer owns this backoff. Local followers add no timers or jitter state.
        var sample = (uint)HashCode.Combine(this.node, flight.Interest, attempt);
        return TimeSpan.FromTicks(lower + (long)(sample % (ulong)width));
    }

    private async Task RenewSharedAsync(Flight flight, CacheOwnerToken owner, CacheOwnedAuthority authority, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(runtime.Binding.Provider.Options.LeaseRenewalInterval, runtime.Time, cancellationToken).ConfigureAwait(false);
                var renewed = await runtime.Access.CallAsync(token => runtime.Access.Provider.RenewOwnershipAsync(flight.Identity, owner, token),
                    cancellationToken).ConfigureAwait(false);
                if (renewed.IsFailure || renewed.Value.State != CacheOwnershipState.Owned || renewed.Value.Owner != owner)
                {
                    authority.Lose();
                    bool cancel;
                    lock (this.gate) { cancel = flight.Waiters == 0; }

                    if (cancel) { flight.Cancel(); }

                    return; // Already executing typed work can finish locally; this does not grant publication.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            authority.Lose();
            bool cancel;
            lock (this.gate) { cancel = flight.Waiters == 0; }

            if (cancel) { flight.Cancel(); }

            runtime.Tasks.Track(Task.FromException(exception), "ownership-renewal-failure");
        }
    }

    private async ValueTask SafeCleanupAsync(Func<ValueTask<Result>> cleanup)
    {
        try
        {
            var result = await cleanup().ConfigureAwait(false);
            if (result.IsFailure)
            {
                runtime.Tasks.Track(Task.FromException(new InvalidOperationException("Native source cleanup was not confirmed.")), "source-cleanup-failure");
            }
        }
        catch (Exception exception)
        {
            runtime.Tasks.Track(Task.FromException(exception), "source-cleanup-failure");
        }
    }

    private ValueTask<Result> CompleteSharedAsync(Flight flight, CacheOwnerToken owner, Result<T> result, CancellationToken cancellationToken)
    {
        var published = result.IsSuccess && result.Value.StorageOutcome == CacheStorageOutcome.Stored && result.Value.Version.HasValue;
        var rechecked = result.IsSuccess && result.Value.StorageOutcome == CacheStorageOutcome.None && result.Value.Version.HasValue;
        var completion = result.IsFailure ? new CacheOwnershipCompletion(CacheOwnershipState.Failed, Failure:
            result.Errors.OfType<CacheError>().FirstOrDefault()?.Code ?? CacheErrorCode.SourceFailure) :
            new CacheOwnershipCompletion(published ? CacheOwnershipState.Published : CacheOwnershipState.Unretained,
                published ? result.Value.Version : null, IsNotFound: !rechecked && result.Value.IsNotFound);
        return runtime.Access.CallAsync(token => runtime.Access.Provider.CompleteOwnershipAsync(flight.Identity, owner, completion, token), cancellationToken);
    }
}