namespace BridgingIT.DevKit.Application.Queueing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Applies collected queue subscriptions to the configured broker once the host has started.
/// </summary>
public class QueueingService : BackgroundService
{
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly ILogger<QueueingService> logger;
    private readonly QueueingOptions options;
    private readonly QueueingRegistrationStore registrationStore;
    private readonly IServiceProvider serviceProvider;

    private IDisposable startupRegistration;
    private CancellationTokenSource linkedCts;
    private Task startupTask;
    private IServiceScope scope;
    private IQueueBroker broker;
    private IQueueBrokerBackgroundProcessor backgroundProcessor;

    /// <summary>
    /// Initializes a new queueing hosted service.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="applicationLifetime">The application lifetime.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="registrationStore">The shared registration store.</param>
    /// <param name="options">The queueing options.</param>
    public QueueingService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        QueueingRegistrationStore registrationStore,
        QueueingOptions options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(registrationStore);

        this.applicationLifetime = applicationLifetime;
        this.serviceProvider = serviceProvider;
        this.registrationStore = registrationStore;
        this.logger = loggerFactory?.CreateLogger<QueueingService>() ?? NullLoggerFactory.Instance.CreateLogger<QueueingService>();
        this.options = options ?? new QueueingOptions();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} queueing service stopping (broker={QueueBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
        this.linkedCts?.Cancel();

        if (this.startupTask is not null)
        {
            try
            {
                await Task.WhenAny(this.startupTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            }
            catch
            {
            }
        }

        if (this.broker is not null)
        {
            await this.broker.Unsubscribe();
        }

        await base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
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
        }
    }

    private async Task StartInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (this.options.StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(this.options.StartupDelay, cancellationToken);
            }

            this.scope = this.serviceProvider.CreateScope();
            this.broker = this.scope.ServiceProvider.GetService<IQueueBroker>();
            if (this.broker is null)
            {
                this.logger.LogWarning("{LogKey} queueing service not started because no broker is registered", Constants.LogKey);
                return;
            }

            foreach (var subscription in this.registrationStore.Subscriptions)
            {
                await this.broker.Subscribe(subscription.MessageType, subscription.HandlerType);
            }

            this.backgroundProcessor = this.scope.ServiceProvider.GetService<IQueueBrokerBackgroundProcessor>();

            this.logger.LogInformation("{LogKey} queueing service started (broker={QueueBroker}, subscriptions={SubscriptionCount})", Constants.LogKey, this.broker.GetType().Name, this.registrationStore.Subscriptions.Count);

            if (this.backgroundProcessor is not null)
            {
                await this.backgroundProcessor.RunAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "{LogKey} queueing service failed unexpectedly: {ErrorMessage}", Constants.LogKey, ex.Message);
            throw;
        }
    }
}
