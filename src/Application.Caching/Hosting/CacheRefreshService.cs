// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>Runs explicitly enabled proactive refresh and bounded warm-up through exactly one monitored source worker.</summary>
/// <param name="catalog">The finalized startup catalog.</param>
/// <param name="access">The shared provider readiness and native accounting boundary.</param>
/// <param name="cache">The same decorated collection graph used by foreground operations.</param>
/// <param name="warmup">The bounded warm-up helper, which owns no timer.</param>
/// <param name="control">The selected node's operational pause controls.</param>
/// <param name="scopeFactory">The factory for owned readiness scopes.</param>
/// <param name="applicationLifetime">The host lifecycle used for application-start gating.</param>
/// <param name="timeProvider">The clock used for due eligibility, work budgets, and retry state.</param>
/// <example><code>var sweep = await refresh.SweepOnceAsync(cancellationToken);</code></example>
public sealed class CacheRefreshService(CacheCatalog catalog, CacheWorkerAccess access, IApplicationCache cache,
    CacheWarmupService warmup, CacheBackgroundControl control, IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime, TimeProvider timeProvider)
    : CacheBackgroundService(catalog, CacheWorkerOptions.Refresh(catalog), applicationLifetime, timeProvider)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastRefresh = new(StringComparer.OrdinalIgnoreCase);
    private int nextCollection;
    private int remainingCandidates;
    private long admissionDeadlineTicks;
    private readonly ConcurrentDictionary<string, CacheRefreshQuery> pages = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override bool IsEnabled => this.Catalog.Options.Enabled && this.Catalog.Collections.Values.Any(c => c.Options.Enabled &&
        (c.Options.Refresh.Enabled || c.Options.Warmup.Enabled));

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<CacheWorkerOutcome>> ExecuteSweepAsync(CancellationToken cancellationToken)
    {
        this.remainingCandidates = this.Catalog.Options.Limits.MaxRefreshCandidates;
        this.admissionDeadlineTicks = 0;
        var bindings = this.Catalog.Collections.Values.Where(c => c.Options.Enabled && (c.Options.Refresh.Enabled || c.Options.Warmup.Enabled)).ToArray();
        var offset = bindings.Length == 0 ? 0 : (int)((uint)Interlocked.Increment(ref this.nextCollection) % (uint)bindings.Length);
        var tasks = Enumerable.Range(0, bindings.Length).Select(index => this.SweepCollectionAsync(bindings[(index + offset) % bindings.Length], cancellationToken)).ToArray();
        return Array.AsReadOnly(await Task.WhenAll(tasks).ConfigureAwait(false));
    }

    /// <inheritdoc />
    protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        await base.ExecuteIterationAsync(cancellationToken).ConfigureAwait(false);
        if (!this.Catalog.Collections.Values.Any(c => c.Options.Enabled && (c.Options.Refresh.Enabled || c.Options.Warmup.Enabled && c.Options.Warmup.Interval.HasValue)) &&
            warmup.StartupPassesCompleted)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, this.TimeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<CacheWorkerOutcome> SweepCollectionAsync(CacheCollectionBinding binding, CancellationToken cancellationToken)
    {
        var started = this.TimeProvider.GetUtcNow();
        var options = binding.Options.Refresh;
        if (control.GetState(binding.Name).Paused)
        {
            this.pages.TryRemove(binding.Name, out _);
            return new(binding.Name, true, started, started + options.Interval, Skipped: 1);
        }

        var refreshDue = options.Enabled && (!this.lastRefresh.TryGetValue(binding.Name, out var last) || started - last >= options.Interval);
        var warmupDue = warmup.IsDue(binding);
        if (!refreshDue && !warmupDue) { return new(binding.Name, true, started, started + options.Interval); }

        await using var scope = scopeFactory.CreateAsyncScope();
        var alias = refreshDue ? options.Loader : binding.Options.Warmup.Loader;
        var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken, alias).ConfigureAwait(false);
        if (ready.IsFailure) { return new(binding.Name, false, started, started + options.Interval, Failures: 1); }

        var admittedAt = this.TimeProvider.GetUtcNow();
        Interlocked.CompareExchange(ref this.admissionDeadlineTicks, admittedAt.UtcTicks + this.Catalog.Options.BackgroundServices.NewWorkBudget.Ticks, 0);
        var deadline = new DateTimeOffset(Interlocked.Read(ref this.admissionDeadlineTicks), TimeSpan.Zero);
        deadline = deadline < admittedAt + options.NewWorkBudget ? deadline : admittedAt + options.NewWorkBudget;
        var warm = warmupDue ? await warmup.RunPassAsync(binding, deadline, cancellationToken).ConfigureAwait(false) : null;
        if (!refreshDue || this.TimeProvider.GetUtcNow() >= deadline || control.GetState(binding.Name).Paused)
        {
            return warm ?? new(binding.Name, true, started, started + options.Interval, Skipped: 1);
        }

        var allowance = this.ReserveCandidates(options.CandidateLimit);
        if (allowance == 0) { return warm ?? new(binding.Name, true, started, started + options.Interval, Skipped: 1); }

        this.lastRefresh[binding.Name] = this.TimeProvider.GetUtcNow();
        var query = this.pages.GetValueOrDefault(binding.Name) ?? new(this.Catalog.Options.Namespace, binding.Name, binding.Options.ContractVersion,
            this.TimeProvider.GetUtcNow(), options.RefreshAhead, options.ActivityWindow, allowance);
        query = query with { Limit = allowance };
        var found = await access.CallAsync(binding, (provider, token) => provider.QueryRefreshAsync(query, token), cancellationToken).ConfigureAwait(false);
        if (found.IsFailure) { this.pages.TryRemove(binding.Name, out _); return new(binding.Name, true, started, started + options.Interval, Failures: 1); }

        Interlocked.Add(ref this.remainingCandidates, Math.Max(0, allowance - found.Value.CandidatesExamined));
        if (found.Value.Continuation is null) { this.pages.TryRemove(binding.Name, out _); }
        else { this.pages[binding.Name] = query with { Continuation = found.Value.Continuation }; }

        var completed = 0;
        var skipped = 0;
        var failures = 0;
        var observations = scope.ServiceProvider.GetService<CacheDiagnostics>()?.ForCollection(binding.Name);
        var loader = this.Catalog.Registry.Loaders[CacheIdentityNormalizer.Name(options.Loader)];
        await Parallel.ForEachAsync(found.Value.Entries, new ParallelOptions
        {
            MaxDegreeOfParallelism = binding.Options.Limits.MaxRefreshSources, CancellationToken = cancellationToken
        }, async (metadata, token) =>
        {
            if (control.GetState(binding.Name).Paused || this.TimeProvider.GetUtcNow() >= deadline)
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            var current = await access.CallAsync(binding, (provider, ct) => provider.ValidateReadAsync(metadata.Identity, metadata.Version,
                new(ObserveAccess: false), ct), token).ConfigureAwait(false);
            if (current.IsFailure || current.Value.State != CacheReadValidationState.Valid ||
                !CacheRefreshPolicy.IsEligible(current.Value.Metadata, query with { Now = this.TimeProvider.GetUtcNow(), Continuation = null }))
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            observations?.Record(CacheObservationKind.RefreshAttempt);
            var result = await loader.Background(cache, binding, new(metadata.DisplayKey ?? metadata.Identity.Key, metadata.Dimensions), true, metadata.Version, token).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                observations?.Record(result.Value.StorageOutcome == CacheStorageOutcome.Stored ? CacheObservationKind.RefreshSuccess : CacheObservationKind.RefreshFailure);
                if (result.Value.StorageOutcome == CacheStorageOutcome.Stored) { Interlocked.Increment(ref completed); }
                else { Interlocked.Increment(ref skipped); }

                await access.CallAsync(binding, (provider, ct) => provider.RecordRefreshAttemptAsync(new(metadata.Identity, metadata.Version, false, this.TimeProvider.GetUtcNow()), ct), token).ConfigureAwait(false);
            }
            else
            {
                observations?.Record(CacheObservationKind.RefreshFailure);
                Interlocked.Increment(ref failures);
                if (CacheSourceFailureClassifier.IsSourceFailure(result))
                {
                    await access.CallAsync(binding, (provider, ct) => provider.RecordRefreshAttemptAsync(new(metadata.Identity, metadata.Version, true, this.TimeProvider.GetUtcNow()), ct), token).ConfigureAwait(false);
                }
            }
        }).ConfigureAwait(false);
        return new(binding.Name, true, started, this.TimeProvider.GetUtcNow() + options.Interval,
            found.Value.CandidatesExamined + (warm?.Examined ?? 0), completed + (warm?.Completed ?? 0), skipped,
            failures + (warm?.Failures ?? 0), completed > 0 ? this.TimeProvider.GetUtcNow() : warm?.LastSuccessAt);
    }
    private int ReserveCandidates(int maximum)
    {
        while (true)
        {
            var available = Volatile.Read(ref this.remainingCandidates);
            if (available <= 0) { return 0; }

            var reserved = Math.Min(maximum, available);
            if (Interlocked.CompareExchange(ref this.remainingCandidates, available - reserved, available) == available) { return reserved; }
        }
    }

}