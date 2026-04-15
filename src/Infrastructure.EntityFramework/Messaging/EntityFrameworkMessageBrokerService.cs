// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Messaging;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that periodically processes persisted broker messages for the Entity Framework transport.
/// </summary>
public class EntityFrameworkMessageBrokerService : BackgroundService
{
    private readonly ILogger<EntityFrameworkMessageBrokerService> logger;
    private readonly EntityFrameworkMessageBrokerOptions options;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly Func<CancellationToken, Task> processWork;
    private PeriodicTimer processTimer;
    private SemaphoreSlim semaphore;

    /// <summary>
    /// Initializes a new hosted service instance.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="applicationLifetime">The host application lifetime.</param>
    /// <param name="options">The broker runtime options.</param>
    /// <param name="processWork">The processing delegate invoked each cycle.</param>
    public EntityFrameworkMessageBrokerService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        EntityFrameworkMessageBrokerOptions options,
        Func<CancellationToken, Task> processWork)
    {
        EnsureArg.IsNotNull(applicationLifetime, nameof(applicationLifetime));
        EnsureArg.IsNotNull(processWork, nameof(processWork));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkMessageBrokerService>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkMessageBrokerService>();
        this.applicationLifetime = applicationLifetime;
        this.options = options ?? new EntityFrameworkMessageBrokerOptions();
        this.processWork = processWork;
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} entity framework message broker service stopped", Constants.LogKey);
        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();

        base.Dispose();
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return Task.CompletedTask;
        }

        this.applicationLifetime.ApplicationStarted.Register(async () =>
        {
            if (this.options.StartupDelay > TimeSpan.Zero && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.options.StartupDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }

            this.semaphore = new SemaphoreSlim(1, 1);
            this.processTimer = new PeriodicTimer(this.options.ProcessingInterval);
            this.logger.LogInformation("{LogKey} entity framework message broker service started", Constants.LogKey);

            try
            {
                while (await this.processTimer.WaitForNextTickAsync(cancellationToken))
                {
                    if (this.options.ProcessingDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(this.options.ProcessingDelay, cancellationToken);
                    }

                    await this.ProcessWorkAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                this.logger.LogInformation("{LogKey} entity framework message broker service stopped", Constants.LogKey);
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
                this.logger.LogWarning("{LogKey} entity framework message broker service timed out waiting for semaphore", Constants.LogKey);
                return;
            }

            await this.processWork(cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} entity framework message broker service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }
        finally
        {
            this.semaphore?.Release();
        }
    }
}