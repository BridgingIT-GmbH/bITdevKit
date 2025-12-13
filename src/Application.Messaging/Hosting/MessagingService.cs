// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Background service responsible for initializing and managing the message broker subscription lifecycle.
/// </summary>
/// <remarks>
/// The broker is initialized only after the application host has fully started. As startup is triggered
/// via <see cref="IHostApplicationLifetime.ApplicationStarted"/>, asynchronous work is explicitly tracked
/// and coordinated with host shutdown to avoid running after dependency injection container disposal.
/// </remarks>
public class MessagingService : BackgroundService
{
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly ILogger<MessagingService> logger;
    private readonly MessagingOptions options;
    private readonly IServiceProvider serviceProvider;

    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task startupTask;
    private IServiceScope scope;
    private IMessageBroker broker;

    public MessagingService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        MessagingOptions options)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.applicationLifetime = applicationLifetime;
        this.serviceProvider = serviceProvider;
        this.logger = loggerFactory?.CreateLogger<MessagingService>() ?? NullLoggerFactory.Instance.CreateLogger<MessagingService>();
        this.options = options ?? new MessagingOptions();
    }

    /// <summary>
    /// Registers broker startup once the application host has fully started.
    /// </summary>
    /// <param name="stoppingToken">A token triggered when the host begins shutting down.</param>
    /// <returns>A completed task.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        this.startupRegistration = this.applicationLifetime.ApplicationStarted.Register(() =>
        {
            this.startupTask = Task.Run(() => this.StartInternalAsync(this.linkedCts.Token), this.linkedCts.Token);
        });

        stoppingToken.Register(this.OnStopping);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the messaging service and unsubscribes from the broker before shutdown.
    /// </summary>
    /// <param name="cancellationToken">A token that limits shutdown waiting.</param>
    /// <returns>A task that completes once shutdown coordination has finished.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} broker message service stopping (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);

        this.linkedCts?.Cancel();

        if (this.startupTask != null)
        {
            try
            {
                await Task.WhenAny(this.startupTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            }
            catch
            {
                // Ignore shutdown-time failures
            }
        }

        this.broker?.Unsubscribe();

        this.logger.LogInformation("{LogKey} broker message service stopped (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Releases all unmanaged resources used by the messaging service.
    /// </summary>
    public override void Dispose()
    {
        this.scope?.Dispose();
        this.linkedCts?.Dispose();
        base.Dispose();
    }

    private void OnStopping()
    {
        try
        {
            this.startupRegistration?.Dispose();
            this.linkedCts?.Cancel();
        }
        catch
        {
            // Ignore shutdown-time exceptions
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay.TotalMilliseconds > 0)
            {
                this.logger.LogDebug("{LogKey} broker service startup delayed", Constants.LogKey);
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            this.scope = this.serviceProvider.CreateScope();

            try
            {
                this.broker = this.scope.ServiceProvider.GetService<IMessageBroker>();
                this.logger.LogInformation("{LogKey} broker message service starting (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
            }
            catch (InvalidOperationException ex)
            {
                this.logger.LogError(ex, "{LogKey} broker message service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
                return;
            }

            if (this.broker != null)
            {
                this.logger.LogInformation("{LogKey} broker message service started (broker={MessageBroker})", Constants.LogKey, this.broker.GetType().Name);
            }
            else
            {
                this.logger.LogWarning("{LogKey} broker message service not started (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} broker message service failed unexpectedly: {ErrorMessage}", Constants.LogKey, ex.Message);
            throw;
        }
    }
}