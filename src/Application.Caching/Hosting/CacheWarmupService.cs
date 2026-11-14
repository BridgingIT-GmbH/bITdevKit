// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Runs bounded warm-up passes as a helper of the refresh worker; it owns no hosted timer or polling loop.</summary>
/// <param name="catalog">The immutable trusted startup catalog.</param>
/// <param name="cache">The same decorated collection handles used by foreground callers.</param>
/// <param name="control">The selected node's pause state.</param>
/// <param name="access">The provider and registered-loader readiness boundary.</param>
/// <param name="scopeFactory">The factory for one owned key-enumeration scope per pass.</param>
/// <param name="timeProvider">The clock used for work admission and periodic eligibility.</param>
/// <example><code>var outcome = await warmup.RunPassAsync(binding, deadline, cancellationToken);</code></example>
public sealed class CacheWarmupService(CacheCatalog catalog, IApplicationCache cache, CacheBackgroundControl control,
    CacheWorkerAccess access, IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastPass = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets whether a configured collection has a startup or explicitly periodic pass due.</summary>
    /// <param name="binding">The declared collection.</param>
    /// <returns>True for a configured unpaused pass that has not run or whose explicit interval elapsed.</returns>
    public bool IsDue(CacheCollectionBinding binding)
    {
        var state = control.GetState(binding.Name);
        return state.WarmupConfigured && !state.Paused && (!this.lastPass.TryGetValue(binding.Name, out var last) ||
            binding.Options.Warmup.Interval.HasValue && timeProvider.GetUtcNow() - last >= binding.Options.Warmup.Interval.Value);
    }

    /// <summary>Gets whether all configured startup-only passes have reached a bounded completion boundary.</summary>
    public bool StartupPassesCompleted => catalog.Collections.Values.Where(c => c.Options.Enabled && c.Options.Warmup.Enabled)
        .All(c => this.lastPass.ContainsKey(c.Name));

    /// <summary>Runs one bounded ordinary read-through pass, disposing its source enumerator before returning.</summary>
    /// <param name="binding">The ready declared collection.</param>
    /// <param name="deadline">The fixed deadline for admitting more keys.</param>
    /// <param name="cancellationToken">The refresh worker's shutdown token.</param>
    /// <returns>The bounded key and loader outcomes.</returns>
    public async ValueTask<CacheWorkerOutcome> RunPassAsync(CacheCollectionBinding binding, DateTimeOffset deadline,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (!this.IsDue(binding)) { return new(binding.Name, true, now, now, Skipped: 1); }

        var examined = 0;
        var completed = 0;
        var failures = 0;
        await using var scope = scopeFactory.CreateAsyncScope();
        var ready = await access.EnsureReadyAsync(binding, scope.ServiceProvider, cancellationToken, binding.Options.Warmup.Loader).ConfigureAwait(false);
        if (ready.IsFailure) { return new(binding.Name, false, now, now + binding.Options.Refresh.Interval, Failures: 1); }

        using var budget = new CacheCancellationDeadline(deadline > now ? deadline - now : TimeSpan.Zero, timeProvider, scope.ServiceProvider.GetService<CacheRuntimeTasks>());
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, budget.Token);
        var source = binding.Options.Warmup.KeySource is null ? Configured(binding.Options.Warmup.Items, linked.Token) :
            catalog.Registry.WarmupSources[CacheIdentityNormalizer.Name(binding.Options.Warmup.KeySource)](scope.ServiceProvider)
                .ReadAsync(new(catalog.Options.Namespace, binding.Name, deadline), linked.Token);
        var observations = scope.ServiceProvider.GetService<CacheDiagnostics>()?.ForCollection(binding.Name);
        var loader = catalog.Registry.Loaders[CacheIdentityNormalizer.Name(binding.Options.Warmup.Loader)];
        try
        {
            await using var enumerator = source.GetAsyncEnumerator(linked.Token);
            while (examined < binding.Options.Limits.MaxBatchSize && timeProvider.GetUtcNow() < deadline && !control.GetState(binding.Name).Paused)
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) { break; }

                if (control.GetState(binding.Name).Paused || timeProvider.GetUtcNow() >= deadline) { break; }

                examined++;
                observations?.Record(CacheObservationKind.WarmupAttempt);
                var result = await loader.Background(cache, binding, enumerator.Current, false, null, cancellationToken).ConfigureAwait(false);
                observations?.Record(result.IsSuccess ? CacheObservationKind.WarmupSuccess : CacheObservationKind.WarmupFailure);
                if (result.IsSuccess) { completed++; }
                else { failures++; }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        finally
        {
            // Paused passes restart with a fresh scoped enumerator; no cursor survives a pause.
            if (!control.GetState(binding.Name).Paused) { this.lastPass[binding.Name] = timeProvider.GetUtcNow(); }
        }

        return new(binding.Name, true, now, now + (binding.Options.Warmup.Interval ?? TimeSpan.FromDays(1)), examined, completed,
            examined - completed - failures, failures, completed > 0 ? timeProvider.GetUtcNow() : null);
    }

    private static async IAsyncEnumerable<CacheItem> Configured(IReadOnlyList<CacheItem> items, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask;
    }
}