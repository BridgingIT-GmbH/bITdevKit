// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

/// <summary>Owns the detached writer task and drains it only after all hosted producer stop calls have completed.</summary>
/// <example><code>await host.StopAsync(cancellationToken);</code></example>
public sealed class AuditWriteBackgroundService<TContext>(EntityFrameworkAuditStorageProvider<TContext> provider, AuditRuntime runtime, AuditEntityFrameworkOptions options,
    IHostApplicationLifetime applicationLifetime = null, IDatabaseReadyService databaseReadyService = null, TimeProvider timeProvider = null)
    : IHostedLifecycleService, IDisposable where TContext : DbContext, IAuditingDbContext
{
    private readonly CancellationTokenSource stopping = new();
    private Task worker = Task.CompletedTask;
    private int disposed;

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Starts one owned writer with no inherited request, actor, module or audit flow.</summary>
    /// <example><code>await service.StartAsync(cancellationToken);</code></example>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Buffered) return Task.CompletedTask;
        if (ExecutionContext.IsFlowSuppressed()) this.worker = Task.Run(this.RunAsync, CancellationToken.None);
        else
        {
            using (ExecutionContext.SuppressFlow()) this.worker = Task.Run(this.RunAsync, CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        await AuditBackgroundStartup.WaitAsync<TContext>(options, applicationLifetime, databaseReadyService, timeProvider, this.stopping.Token).ConfigureAwait(false);
        await provider.RunWriterAsync(this.stopping.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Keeps delivery active during producer stop; draining occurs in the subsequent lifecycle stage.</summary>
    /// <example><code>await service.StopAsync(cancellationToken);</code></example>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Finalizes real owners, closes admission, and drains accepted records within the configured shutdown budget.</summary>
    /// <example><code>await service.StoppedAsync(cancellationToken);</code></example>
    public async Task StoppedAsync(CancellationToken cancellationToken)
    {
        runtime.BeginShutdown();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ShutdownTimeout);
        try { await runtime.WaitForIdleAsync(timeout.Token).ConfigureAwait(false); }
        catch (Exception) { runtime.Report("AuditShutdownActiveRootsPending"); }

        provider.CloseWriterAdmissions();
        try
        {
            var flush = await provider.FlushAsync(timeout.Token).ConfigureAwait(false);
            if (flush.IsFailure || flush.Value.Failed != 0 || flush.Value.Pending != 0) runtime.Report("AuditShutdownDeliveryUnconfirmed");
            await this.worker.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) { runtime.Report("AuditShutdownDeliveryUnconfirmed"); }
        finally
        {
            this.stopping.Cancel();
            _ = this.worker.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    /// <summary>Cancels owned work during disposal without blocking or inventing persistence confirmation.</summary>
    /// <example><code>service.Dispose();</code></example>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
        this.stopping.Cancel();
        this.stopping.Dispose();
    }
}
