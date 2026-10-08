// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;

internal sealed class OperationProfilingCleanupService : BackgroundService
{
    private readonly OperationProfiler profiler;
    private readonly TimeProvider clock;
    private readonly TimeSpan interval;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public OperationProfilingCleanupService(OperationProfiler profiler, ProfilingOptions options, TimeProvider clock = null)
    {
        this.profiler = profiler;
        this.clock = clock ?? TimeProvider.System;
        this.interval = options.Operations.CleanupInterval;
    }

    internal Task Ready => this.ready.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var boundary = this.profiler.BeginExecutionBoundary();
            using var suppression = this.profiler.Suppress();
            using var timer = new ProfilingPeriodicTimer(this.interval, this.clock);
            this.ready.TrySetResult();
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                this.profiler.ExpireAbandoned();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host cancellation ends observation without changing application cancellation.
        }
        catch (Exception)
        {
            Interlocked.Increment(ref this.profiler.Counters.CaptureFaults);
            this.profiler.CloseForHost();
        }
        finally
        {
            this.ready.TrySetResult();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Register this worker after the writer so its reverse-order stop closes captures before drain.
        this.profiler.CloseForHost();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
