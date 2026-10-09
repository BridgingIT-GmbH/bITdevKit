// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;

/// <summary>Resumes bounded provider maintenance independently of enabled capture capabilities.</summary>
/// <example>Used by the shared profiling writer and its node-local health observations.</example>
public sealed class ProfilingMaintenanceService : BackgroundService
{
    private readonly IProfilingStorageProvider provider;
    private readonly IOperationProfiler profiler;
    private readonly OperationProfilingHealthState health;
    private readonly ProfilingMaintenanceRequest request;
    private readonly TimeProvider clock;
    private readonly TimeSpan interval;
    private readonly TimeSpan timeout;

    /// <summary>Resumes bounded provider maintenance independently of enabled capture capabilities.</summary>
    /// <example>Used by the shared profiling writer and its node-local health observations.</example>
    public ProfilingMaintenanceService(IProfilingStorageProvider provider, ProfilingOptions options,
        OperationProfilingHealthState health, IOperationProfiler profiler = null, TimeProvider clock = null)
    {
        this.provider = provider;
        this.profiler = profiler;
        this.health = health;
        this.clock = clock ?? TimeProvider.System;
        this.interval = options.Storage.MaintenanceInterval;
        this.timeout = options.Operations.AttemptTimeout;
        this.request = new()
        {
            MaximumRoots = options.Storage.MaximumMaintenanceRoots, Budget = options.Storage.MaintenanceTimeBudget,
            MaximumOperationCount = provider.Capabilities.OperationRetention?.MaximumCount ?? options.Operations.MaximumRetainedOperations,
            MaximumOperationBytes = provider.Capabilities.OperationRetention is { } retention ? retention.MaximumBytes : options.Operations.MaximumRetainedBytes,
            MaximumOperationAge = provider.Capabilities.OperationRetention?.MaximumAge ?? options.Operations.MaximumOperationAge,
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var boundary = this.profiler?.BeginExecutionBoundary();
            using var suppression = this.profiler?.Suppress();
            using var timer = new ProfilingPeriodicTimer(this.interval, this.clock);
            do
            {
                using var deadline = new CancellationTokenSource(this.timeout, this.clock);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, stoppingToken);
                try
                {
                    // One call at a time, including recovery; never replace a cancellation-ignoring call.
                    var result = await this.provider.ResumeMaintenanceAsync(this.request, linked.Token).ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        Interlocked.Add(ref this.health.RetentionRemovals, result.Value.RetentionRemovedOperations + result.Value.CapacityEvictedOperations);
                        Interlocked.Exchange(ref this.health.ApplyingClears, result.Value.RemainingClears);
                    }
                    else
                    {
                        Interlocked.Increment(ref this.health.Faults);
                    }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref this.health.Faults);
                }
            }

            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // No application execution is canceled by maintenance shutdown.
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.health.Faults);
        }
    }
}
