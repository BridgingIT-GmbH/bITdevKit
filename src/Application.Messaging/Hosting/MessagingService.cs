// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public class MessagingService : BackgroundService
{
    private readonly ILogger<MessagingService> logger;
    private readonly MessagingOptions options;
    private readonly IServiceScope scope;
    private IMessageBroker broker;

    public MessagingService(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        MessagingOptions options)
    {
        EnsureArg.IsNotNull(serviceProvider, nameof(serviceProvider));

        this.logger = loggerFactory?.CreateLogger<MessagingService>() ?? NullLoggerFactory.Instance.CreateLogger<MessagingService>();
        this.options = options ?? new MessagingOptions();
        this.scope = serviceProvider.CreateScope();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("{LogKey} broker stopping (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
        this.broker?.Unsubscribe();
        this.logger.LogInformation("{LogKey} broker stopped (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);

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

        if (this.options.StartupDelay.TotalMilliseconds > 0)
        {
            this.logger.LogDebug("{LogKey} broker service startup delayed)", Constants.LogKey);

            await Task.Delay(this.options.StartupDelay, cancellationToken);
        }

        try
        {
            this.broker = this.scope.ServiceProvider.GetService(typeof(IMessageBroker)) as IMessageBroker;
            this.logger.LogInformation("{LogKey} broker service starting (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
        }
        catch (InvalidOperationException ex) // sometimes caused by many concurrent integration tests and the in process broker (messaging)
        {
            this.logger.LogError(ex, "{LogKey} broker service failed: {ErrorMessage}", Constants.LogKey, ex.Message);
        }

        if (this.broker is not null)
        {
            this.logger.LogInformation("{LogKey} broker service started (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
        }
        else
        {
            this.logger.LogWarning("{LogKey} broker service not started (broker={MessageBroker})", Constants.LogKey, this.broker?.GetType()?.Name);
        }
    }
}