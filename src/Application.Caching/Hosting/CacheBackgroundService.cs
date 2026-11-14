// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

/// <summary>Serializes scheduled and explicit cache sweeps through the DevKit host-start and shutdown lifecycle.</summary>
/// <param name="catalog">The immutable feature catalog.</param>
/// <param name="options">The specific worker schedule.</param>
/// <param name="applicationLifetime">The host startup and shutdown signals.</param>
/// <param name="timeProvider">The deterministic scheduling clock.</param>
/// <example><code>var outcome = await worker.SweepOnceAsync(cancellationToken);</code></example>
public abstract class CacheBackgroundService(CacheCatalog catalog, CacheWorkerOptions options,
    IHostApplicationLifetime applicationLifetime, TimeProvider timeProvider)
    : PeriodicBackgroundService(options.ToPeriodicOptions(), applicationLifetime, timeProvider)
{
    private readonly SemaphoreSlim sweepGate = new(1, 1);
    private readonly ConcurrentDictionary<string, CacheWorkerOutcome> outcomes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the most recent bounded outcome for a declared collection or provider.</summary>
    /// <param name="name">The canonical collection or provider alias reported by this worker.</param>
    /// <returns>The latest observed outcome, or null before its first sweep.</returns>
    public CacheWorkerOutcome GetLastOutcome(string name) => this.outcomes.GetValueOrDefault(CacheIdentityNormalizer.Name(name));

    /// <summary>Executes one explicit bounded sweep; it cannot overlap the hosted iteration or another explicit sweep.</summary>
    /// <param name="cancellationToken">Cancellation for admission and bounded units.</param>
    /// <returns>The observed scope outcomes, or a failure when a sweep already occupies this instance.</returns>
    public async Task<Result<IReadOnlyList<CacheWorkerOutcome>>> SweepOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!this.IsEnabled) { return Result<IReadOnlyList<CacheWorkerOutcome>>.Success([]); }

        if (!await this.sweepGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return Result<IReadOnlyList<CacheWorkerOutcome>>.Failure(new CacheError(CacheErrorCode.Capacity, "This worker already owns an active sweep."));
        }

        try
        {
            var result = await this.ExecuteSweepAsync(cancellationToken).ConfigureAwait(false);
            foreach (var outcome in result) { this.outcomes[outcome.Name] = outcome; }

            return Result<IReadOnlyList<CacheWorkerOutcome>>.Success(result);
        }
        finally
        {
            this.sweepGate.Release();
        }
    }

    /// <summary>Gets the finalized catalog shared by this worker and its bounded units.</summary>
    protected CacheCatalog Catalog { get; } = catalog;

    /// <inheritdoc />
    protected override bool IsEnabled => this.Catalog.Options.Enabled;

    /// <summary>Executes provider-specific bounded work and leaves unexpected invariant failures observable to host monitoring.</summary>
    /// <param name="cancellationToken">The host shutdown token.</param>
    /// <returns>The finite observed scope outcomes.</returns>
    protected abstract Task<IReadOnlyList<CacheWorkerOutcome>> ExecuteSweepAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    protected override async Task ExecuteIterationAsync(CancellationToken cancellationToken) =>
        await this.SweepOnceAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>Reports a bounded worker scope without exposing keys, payloads, connection strings, or source exceptions.</summary>
/// <param name="Name">The declared collection or provider alias.</param>
/// <param name="Ready">Whether dependencies were ready for admitted work.</param>
/// <param name="SweptAt">The observation timestamp.</param>
/// <param name="NextSweepAt">The earliest ordinary next sweep.</param>
/// <param name="Examined">The metadata or seed candidates examined.</param>
/// <param name="Completed">The completed admitted work units.</param>
/// <param name="Skipped">The ineligible, paused, conflicted, or bounded-out units.</param>
/// <param name="Failures">The failed provider or source units.</param>
/// <param name="LastSuccessAt">The latest successful publication or cleanup observation in this sweep.</param>
/// <example><code>var lastSweep = worker.GetLastOutcome("cards");</code></example>
public sealed record CacheWorkerOutcome(string Name, bool Ready, DateTimeOffset SweptAt, DateTimeOffset NextSweepAt,
    int Examined = 0, int Completed = 0, int Skipped = 0, int Failures = 0, DateTimeOffset? LastSuccessAt = null);