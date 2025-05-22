namespace BridgingIT.DevKit.Infrastructure.Notifications;

using System;
using BridgingIT.DevKit.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public class NotificationServiceInfrastructureBuilder : NotificationServiceBuilder
{
    public NotificationServiceInfrastructureBuilder(IServiceCollection services)
        : base(services)
    {
    }

    public NotificationServiceInfrastructureBuilder WithEntityFrameworkProvider<TContext>()
        where TContext : DbContext, INotificationEmailContext
    {
        this.Services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationStorageProvider<TContext>>();
        return this;
    }

    public NotificationServiceInfrastructureBuilder WithOutbox<TContext>(
        Builder<OutboxNotificationEmailOptionsBuilder, OutboxNotificationEmailOptions> optionsBuilder = null)
        where TContext : DbContext, INotificationEmailContext
    {
        var options = optionsBuilder?.Invoke(new OutboxNotificationEmailOptionsBuilder()).Build() ?? new OutboxNotificationEmailOptions();
        this.Services.AddSingleton(options);
        this.Services.AddSingleton<IOutboxNotificationEmailWorker, OutboxNotificationEmailWorker>();
        this.Services.AddSingleton<IOutboxNotificationEmailQueue>(sp => new OutboxNotificationEmailQueue(
            sp.GetRequiredService<ILoggerFactory>(),
            id => sp.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(id)));
        this.Services.AddHostedService<OutboxNotificationEmailService>();
        this.Options.IsOutboxConfigured = true;
        return this;
    }
}