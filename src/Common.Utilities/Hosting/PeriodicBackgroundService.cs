// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Runs one asynchronous operation at a time after application startup and at a configured interval.
/// </summary>
/// <remarks>
/// Unexpected iteration failures remain observable through <see cref="BackgroundService.ExecuteTask" />.
/// Cancellation requested by host shutdown is treated as normal completion.
/// </remarks>
/// <example>
/// <code>
/// public sealed class CleanupService(IHostApplicationLifetime lifetime)
///     : PeriodicBackgroundService(new() { Interval = TimeSpan.FromHours(1) }, lifetime)
/// {
///     protected override Task ExecuteIterationAsync(CancellationToken cancellationToken) =&gt;
///         CleanupAsync(cancellationToken);
/// }
/// </code>
/// </example>
public abstract class PeriodicBackgroundService : BackgroundService
{
    private readonly PeriodicBackgroundServiceOptions options;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a periodic background service.</summary>
    /// <param name="options">The scheduling options.</param>
    /// <param name="applicationLifetime">The host lifetime used to await application startup.</param>
    /// <param name="timeProvider">The time provider used for deterministic delays.</param>
    protected PeriodicBackgroundService(
        PeriodicBackgroundServiceOptions options,
        IHostApplicationLifetime applicationLifetime,
        TimeProvider timeProvider = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        this.applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets whether scheduled execution is enabled.</summary>
    /// <example><code>protected override bool IsEnabled =&gt; options.Enabled;</code></example>
    protected virtual bool IsEnabled => true;

    /// <summary>Gets the time provider used by the scheduler.</summary>
    /// <example><code>var now = this.TimeProvider.GetUtcNow();</code></example>
    protected TimeProvider TimeProvider => this.timeProvider;

    /// <summary>Executes one complete monitored iteration.</summary>
    /// <param name="cancellationToken">The host shutdown token.</param>
    /// <returns>A task representing the iteration.</returns>
    /// <example><code>protected override Task ExecuteIterationAsync(CancellationToken token) =&gt; CleanupAsync(token);</code></example>
    protected abstract Task ExecuteIterationAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.IsEnabled)
        {
            return;
        }

        try
        {
            await this.WaitForApplicationStartedAsync(stoppingToken).ConfigureAwait(false);

            if (this.options.StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(this.options.StartupDelay, this.timeProvider, stoppingToken).ConfigureAwait(false);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await this.ExecuteIterationAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(this.options.Interval, this.timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is normal completion.
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = this.timeProvider.CreateTimer(
            static state => ((CancellationTokenSource)state).Cancel(),
            timeout,
            this.options.StopTimeout,
            Timeout.InfiniteTimeSpan);
        await base.StopAsync(timeout.Token).ConfigureAwait(false);
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken cancellationToken)
    {
        if (this.applicationLifetime.ApplicationStarted.IsCancellationRequested)
        {
            return;
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = this.applicationLifetime.ApplicationStarted.Register(
            static state => ((TaskCompletionSource)state).TrySetResult(),
            started);
        await started.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
