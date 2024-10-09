// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class MessagingService : BackgroundService
{
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly ILogger<MessagingService> logger;
    private readonly MessagingOptions options;
    private readonly IServiceScope scope;
    private IMessageBroker broker;

    public MessagingService(
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        MessagingOptions options)
    {
        this.applicationLifetime = applicationLifetime;
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<MessagingService>() ??
            NullLoggerFactory.Instance.CreateLogger<MessagingService>();
        this.options = options ?? new MessagingOptions();
        this.scope = serviceProvider.CreateScope();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} broker message service stopping (broker={MessageBroker})",
            Constants.LogKey,
            this.broker?.GetType()?.Name);
        this.broker?.Unsubscribe();
        this.logger.LogInformation("{LogKey} broker message service stopped (broker={MessageBroker})",
            Constants.LogKey,
            this.broker?.GetType()?.Name);

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        this.scope?.Dispose();
        //this.broker?.Dispose();

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!this.options.Enabled)
        {
            return;
        }

        // Wait "indefinitely", until ApplicationStarted is triggered
        await Task.Delay(Timeout.InfiniteTimeSpan, this.applicationLifetime.ApplicationStarted)
            .ContinueWith(_ =>
                {
                    this.logger.LogDebug("{LogKey} broker message service - application started", Constants.LogKey);
                },
                TaskContinuationOptions.OnlyOnCanceled)
            .ConfigureAwait(false);

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} broker service startup delayed", Constants.LogKey);

            await Task.Delay(this.options.StartupDelay, cancellationToken);
        }

        try
        {
            this.broker = this.scope.ServiceProvider.GetService(typeof(IMessageBroker)) as IMessageBroker;
            this.logger.LogInformation(
                "{LogKey} broker message service starting (broker={MessageBroker})",
                Constants.LogKey,
                this.broker?.GetType()?.Name);
        }
        catch (InvalidOperationException ex) // sometimes caused by many concurrent integration tests and the in process broker (messaging)
        {
            this.logger.LogError(ex, "{LogKey} broker message service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }

        if (this.broker is not null)
        {
            this.logger.LogInformation(
                "{LogKey} broker message service started (broker={MessageBroker})",
                Constants.LogKey,
                this.broker?.GetType()?.Name);
        }
        else
        {
            this.logger.LogWarning(
                "{LogKey} broker message service not started (broker={MessageBroker})",
                Constants.LogKey,
                this.broker?.GetType()?.Name);
        }
    }
}