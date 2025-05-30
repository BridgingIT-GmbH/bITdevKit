namespace BridgingIT.DevKit.Application.Notifications;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class OutboxNotificationEmailService(
    ILoggerFactory loggerFactory,
    IOutboxNotificationEmailWorker worker,
    IHostApplicationLifetime applicationLifetime,
    OutboxNotificationEmailOptions options = null) : BackgroundService
{
    private readonly ILogger<OutboxNotificationEmailService> logger = loggerFactory?.CreateLogger<OutboxNotificationEmailService>() ?? NullLoggerFactory.Instance.CreateLogger<OutboxNotificationEmailService>();
    private readonly IOutboxNotificationEmailWorker worker = worker ?? throw new ArgumentNullException(nameof(worker));
    private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
    private readonly OutboxNotificationEmailOptions options = options ?? new OutboxNotificationEmailOptions();
    private PeriodicTimer processTimer;
    private SemaphoreSlim semaphore;

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            this.logger.LogInformation("{LogKey} Outbox notification email service is disabled", Constants.LogKey);
            return Task.CompletedTask;
        }

        var registration = this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            try
            {
                if (this.options.StartupDelay.TotalMilliseconds > 0)
                {
                    this.logger.LogDebug("{LogKey} Delaying outbox notification email service startup by {StartupDelay}ms", Constants.LogKey, this.options.StartupDelay.TotalMilliseconds);
                    await Task.Delay(this.options.StartupDelay, cancellationToken);
                }

                if (this.options.PurgeOnStartup)
                {
                    this.logger.LogInformation("{LogKey} Purging all outbox notification emails on startup", Constants.LogKey);
                    await this.worker.PurgeAsync(false, cancellationToken);
                }
                else if (this.options.PurgeProcessedOnStartup)
                {
                    this.logger.LogInformation("{LogKey} Purging processed outbox notification emails on startup", Constants.LogKey);
                    await this.worker.PurgeAsync(true, cancellationToken);
                }

                this.semaphore = new SemaphoreSlim(1);
                this.logger.LogInformation("{LogKey} Outbox notification email service started", Constants.LogKey);
                this.processTimer = new PeriodicTimer(this.options.ProcessingInterval);

                while (await this.processTimer.WaitForNextTickAsync(cancellationToken))
                {
                    await this.ProcessWorkAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogInformation("{LogKey} Outbox notification email service stopped due to cancellation", Constants.LogKey);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "{LogKey} Outbox notification email service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
            }
        });

        return Task.CompletedTask;
    }

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await this.semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
            {
                this.logger.LogWarning("{LogKey} Outbox notification email service timed out waiting for semaphore", Constants.LogKey);
                return;
            }

            this.logger.LogDebug("{LogKey} Processing outbox notification emails", Constants.LogKey);
            await this.worker.ProcessAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} Outbox notification email service failed to process: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
        finally
        {
            this.semaphore?.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} Outbox notification email service stopping", Constants.LogKey);
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();
        base.Dispose();
    }
}