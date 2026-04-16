namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Queueing;

using BridgingIT.DevKit.Application.Queueing;

internal sealed class EntityFrameworkQueueBrokerBackgroundProcessor<TContext> : IQueueBrokerBackgroundProcessor, IDisposable
    where TContext : DbContext, IQueueingContext
{
    private readonly ILogger<EntityFrameworkQueueBrokerBackgroundProcessor<TContext>> logger;
    private readonly EntityFrameworkQueueBrokerOptions options;
    private readonly Func<CancellationToken, Task> processWork;
    private PeriodicTimer processTimer;
    private SemaphoreSlim semaphore;

    public EntityFrameworkQueueBrokerBackgroundProcessor(
        ILoggerFactory loggerFactory,
        EntityFrameworkQueueBrokerOptions options,
        Func<CancellationToken, Task> processWork)
    {
        EnsureArg.IsNotNull(processWork, nameof(processWork));

        this.logger = loggerFactory?.CreateLogger<EntityFrameworkQueueBrokerBackgroundProcessor<TContext>>() ?? NullLoggerFactory.Instance.CreateLogger<EntityFrameworkQueueBrokerBackgroundProcessor<TContext>>();
        this.options = options ?? new EntityFrameworkQueueBrokerOptions();
        this.processWork = processWork;
    }

    public void Dispose()
    {
        this.processTimer?.Dispose();
        this.semaphore?.Dispose();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!this.options.Enabled)
        {
            return;
        }

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
        this.logger.LogInformation("{LogKey} entity framework queue broker background processor started", Application.Queueing.Constants.LogKey);

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
            this.logger.LogInformation("{LogKey} entity framework queue broker background processor stopped", Application.Queueing.Constants.LogKey);
        }
    }

    private async Task ProcessWorkAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await this.semaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
            {
                this.logger.LogWarning("{LogKey} entity framework queue broker background processor timed out waiting for semaphore", Application.Queueing.Constants.LogKey);
                return;
            }

            await this.processWork(cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} entity framework queue broker background processor failed: {ErrorMessage}", Application.Queueing.Constants.LogKey, ex.Message);
        }
        finally
        {
            this.semaphore?.Release();
        }
    }
}