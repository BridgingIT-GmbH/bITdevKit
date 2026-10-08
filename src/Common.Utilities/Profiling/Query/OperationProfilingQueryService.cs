// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

/// <summary>Builds retained-history views under shared non-waiting admission and one deadline.</summary>
/// <example><code>var page = await queries.QueryAsync(new OperationProfilingQuery(), token);</code></example>
public sealed class OperationProfilingQueryService : IOperationProfilingQueryService
{
    private readonly IOperationProfilingStore store;
    private readonly OperationRuntimeCorrelationService correlation;
    private readonly IOperationProfilingHealthSource health;
    private readonly IOperationProfiler profiler;
    private readonly ProfilingOptions options;
    private readonly OperationProfilingQueryValidator validator;
    private readonly SemaphoreSlim admission;

    /// <summary>Creates a singleton view service sharing its provider, capture suppression and query budgets.</summary>
    /// <example><code>var queries = new OperationProfilingQueryService(provider, options, health, profiler);</code></example>
    public OperationProfilingQueryService(IProfilingStorageProvider provider, ProfilingOptions options,
        IOperationProfilingHealthSource health, IOperationProfiler profiler = null, TimeProvider clock = null)
    {
        this.store = provider.Operations;
        this.options = options;
        this.health = health;
        this.profiler = profiler;
        this.validator = new(options, clock);
        this.admission = new(options.Queries.MaximumConcurrentQueries);
        this.correlation = new(provider.Runtime as IRuntimeProfilingCorrelationStore, options.Queries.MaximumOverlaySnapshots);
    }

    /// <inheritdoc />
    public async Task<IResult<T>> BuildViewAsync<T>(Func<IOperationProfilingQueryService, CancellationToken, Task<IResult<T>>> build, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.options.Enabled) { return Result<T>.Failure(new ProfilingDisabledError()); }

        if (!this.admission.Wait(0)) { return Result<T>.Failure(new ProfilingBusyError("Profiling view capacity is occupied. Retry later.")); }

        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var session = new ViewSession(this, deadline.Token);
        Task<IResult<T>> work = null;
        try
        {
            deadline.CancelAfter(this.options.Queries.Timeout);
            if (ExecutionContext.IsFlowSuppressed()) { work = Task.Run(InvokeAsync, CancellationToken.None); }
            else
            {
                using (ExecutionContext.SuppressFlow()) { work = Task.Run(InvokeAsync, CancellationToken.None); }
            }

            return await work.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return Result<T>.Failure(new ProfilingQueryTimeoutError()); }
        catch (Exception) { return Result<T>.Failure(new ProfilingUnavailableError("Profiling history could not be read.")); }
        finally
        {
            session.Close();
            // Retain the admission until cancellation-ignoring work actually unwinds, including fire-and-forget subordinate calls.
            _ = ReleaseAsync();
        }

        async Task<IResult<T>> InvokeAsync()
        {
            deadline.Token.ThrowIfCancellationRequested();
            using var boundary = OperationProfilingHelpers.BeginSafeExecutionBoundary(this.profiler);
            IDisposable suppression = null;
            try
            {
                try { suppression = this.profiler?.Suppress(); } catch (Exception) { }

                return await build(session, deadline.Token).ConfigureAwait(false);
            }
            finally { try { suppression?.Dispose(); } catch (Exception) { } }
        }

        async Task ReleaseAsync()
        {
            try
            {
                if (work is not null) { try { await work.ConfigureAwait(false); } catch (Exception) { } }

                await session.DrainAsync().ConfigureAwait(false);
            }
            finally { deadline.Dispose(); this.admission.Release(); }
        }
    }

    /// <inheritdoc />
    public Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.QueryAsync(query, token), cancellationToken);
    /// <inheritdoc />
    public Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.GroupAsync(query, token), cancellationToken);
    /// <inheritdoc />
    public Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.FindAsync(id, token), cancellationToken);
    /// <inheritdoc />
    public Task<IResult<OperationProfilingDistribution>> AnalyzeAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.AnalyzeAsync(query, token), cancellationToken);
    /// <inheritdoc />
    public Task<IResult<OperationProfilingComparison>> CompareAsync(OperationProfilingQuery baseline, OperationProfilingQuery candidate, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.CompareAsync(baseline, candidate, token), cancellationToken);
    /// <inheritdoc />
    public Task<IResult<OperationProfilingRuntimeOverlay>> GetRuntimeOverlayAsync(Guid id, CancellationToken cancellationToken = default) =>
        this.BuildViewAsync((session, token) => session.GetRuntimeOverlayAsync(id, token), cancellationToken);
    /// <inheritdoc />
    public OperationProfilingHealth GetHealth()
    {
        try { return this.health.GetSnapshot(); }
        catch (Exception) { return new() { QueueOldestAgeUnavailable = true, CaptureFaults = 1, ProviderScope = "Unavailable" }; }
    }

    private async Task<IResult<OperationProfilingDistribution>> AnalyzeCoreAsync(OperationProfilingQuery query, CancellationToken token)
    {
        var valid = this.validator.Validate(query);
        if (valid.IsFailure) { return Result<OperationProfilingDistribution>.Failure(valid); }

        var selection = await this.store.SelectAnalysisAsync(query, token).ConfigureAwait(false);
        if (selection.IsFailure) { return Result<OperationProfilingDistribution>.Failure(selection); }

        if (selection.Value.Records.Count > Math.Min(query.MaximumAnalysisCount, this.options.Queries.MaximumAnalysisRecords))
        { return Result<OperationProfilingDistribution>.Failure(new ProfilingQueryLimitError()); }

        return Result<OperationProfilingDistribution>.Success(OperationProfilingAnalysis.Create(selection.Value, query, token));
    }

    private sealed class ViewSession(OperationProfilingQueryService owner, CancellationToken deadline) : IOperationProfilingQueryService
    {
        private readonly object sync = new();
        private Task pending = Task.CompletedTask;
        private bool closed;
        private bool running;

        /// <summary>Executes the bounded BuildViewAsync view-session operation.</summary>
        public Task<IResult<T>> BuildViewAsync<T>(Func<IOperationProfilingQueryService, CancellationToken, Task<IResult<T>>> build, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(token => build(this, token), cancellationToken);
        /// <summary>Executes the bounded QueryAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingPage>> QueryAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(token => this.ValidateAsync(query, () => owner.store.QueryAsync(query, token)), cancellationToken);
        /// <summary>Executes the bounded GroupAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingGroupPage>> GroupAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(token => this.ValidateAsync(query, () => owner.store.GroupAsync(query, token)), cancellationToken);
        /// <summary>Executes the bounded FindAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingRecord>> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(token => id == Guid.Empty ? Task.FromResult<IResult<OperationProfilingRecord>>(Result<OperationProfilingRecord>.Failure(new ProfilingInvalidKeyError("operation"))) : owner.store.FindAsync(id, token), cancellationToken);
        /// <summary>Executes the bounded AnalyzeAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingDistribution>> AnalyzeAsync(OperationProfilingQuery query, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(token => owner.AnalyzeCoreAsync(query, token), cancellationToken);
        /// <summary>Executes the bounded CompareAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingComparison>> CompareAsync(OperationProfilingQuery baseline, OperationProfilingQuery candidate, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync<OperationProfilingComparison>(async token =>
            {
                var first = await owner.AnalyzeCoreAsync(baseline, token).ConfigureAwait(false);
                if (first.IsFailure) { return Result<OperationProfilingComparison>.Failure(first); }

                var second = await owner.AnalyzeCoreAsync(candidate, token).ConfigureAwait(false);
                return second.IsFailure ? Result<OperationProfilingComparison>.Failure(second)
                    : Result<OperationProfilingComparison>.Success(new() { Baseline = first.Value, Candidate = second.Value, P95ChangePercent = first.Value.Count == 0 || second.Value.Count == 0 || first.Value.P95Milliseconds == 0 ? null : (second.Value.P95Milliseconds - first.Value.P95Milliseconds) / first.Value.P95Milliseconds * 100 });
            }, cancellationToken);
        /// <summary>Executes the bounded GetRuntimeOverlayAsync view-session operation.</summary>
        public Task<IResult<OperationProfilingRuntimeOverlay>> GetRuntimeOverlayAsync(Guid id, CancellationToken cancellationToken = default) =>
            this.ExecuteAsync(async token =>
            {
                var record = await owner.store.FindAsync(id, token).ConfigureAwait(false);
                if (record.IsFailure) { return Result<OperationProfilingRuntimeOverlay>.Failure(record); }

                return record.Value is null ? Result<OperationProfilingRuntimeOverlay>.Success(new() { OperationId = id, Limitation = "OperationUnavailable" })
                    : await owner.correlation.GetOverlayAsync(record.Value, token).ConfigureAwait(false);
            }, cancellationToken);
        /// <summary>Executes the bounded GetHealth view-session operation.</summary>
        public OperationProfilingHealth GetHealth() => owner.GetHealth();
        /// <summary>Executes the bounded Close view-session operation.</summary>
        public void Close() { lock (this.sync) { this.closed = true; } }
        /// <summary>Executes the bounded DrainAsync view-session operation.</summary>
        public Task DrainAsync() { lock (this.sync) { return this.pending; } }

        private Task<IResult<T>> ValidateAsync<T>(OperationProfilingQuery query, Func<Task<IResult<T>>> read)
        {
            var valid = owner.validator.Validate(query);
            return valid.IsFailure ? Task.FromResult<IResult<T>>(Result<T>.Failure(valid)) : read();
        }

        private Task<IResult<T>> ExecuteAsync<T>(Func<CancellationToken, Task<IResult<T>>> read, CancellationToken caller)
        {
            TaskCompletionSource completion;
            lock (this.sync)
            {
                if (this.closed || deadline.IsCancellationRequested) { return Task.FromResult<IResult<T>>(Result<T>.Failure(new ProfilingQueryTimeoutError())); }

                if (this.running) { return Task.FromResult<IResult<T>>(Result<T>.Failure(new ProfilingBusyError("A profiling view supports one subordinate query at a time."))); }

                this.running = true;
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                this.pending = completion.Task;
            }

            // Provider/user code must never execute under the session lock, including before its first await.
            return RunAsync();

            async Task<IResult<T>> RunAsync()
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline, caller);
                try { return await read(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { return Result<T>.Failure(new ProfilingUnavailableError("Profiling history could not be read.")); }
                finally
                {
                    lock (this.sync) { this.running = false; completion.TrySetResult(); }
                }
            }
        }
    }
}
