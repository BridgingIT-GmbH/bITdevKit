// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

public class NotificationServiceInfrastructureBuilder(IServiceCollection services) : NotificationServiceBuilder(services)
{
    public NotificationServiceInfrastructureBuilder WithEntityFrameworkStorageProvider<TContext>()
        where TContext : DbContext, INotificationEmailContext
    {
        this.Services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationEmailStorageProvider<TContext>>();
        return this;
    }

    public NotificationServiceInfrastructureBuilder WithOutbox<TContext>(
        Builder<OutboxNotificationEmailOptionsBuilder, OutboxNotificationEmailOptions> optionsBuilder = null)
        where TContext : DbContext, INotificationEmailContext
    {
        var options = optionsBuilder?.Invoke(new OutboxNotificationEmailOptionsBuilder()).Build() ?? new OutboxNotificationEmailOptions();
        this.Services.AddSingleton(options);

        this.Services.AddSingleton<IOutboxNotificationEmailWorker, OutboxNotificationEmailWorker>();
        this.Services.AddSingleton<IOutboxNotificationEmailQueue>(sp =>
            new OutboxNotificationEmailQueue(
                sp.GetRequiredService<ILoggerFactory>(),
                id => sp.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(id)));
        this.Services.AddHostedService<OutboxNotificationEmailService>();
        this.Options.IsOutboxConfigured = true;

        return this;
    }
}

public static class NotificationServiceBuilderExtensions
{
    public static NotificationServiceBuilder WithEntityFrameworkStorageProvider<TContext>(this NotificationServiceBuilder serviceBuilder)
        where TContext : DbContext, INotificationEmailContext
    {
        serviceBuilder.Services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationEmailStorageProvider<TContext>>();
        return serviceBuilder;
    }

    public static NotificationServiceBuilder WithOutbox<TContext>(
        this NotificationServiceBuilder serviceBuilder,
        Builder<OutboxNotificationEmailOptionsBuilder, OutboxNotificationEmailOptions> optionsBuilder = null)
        where TContext : DbContext, INotificationEmailContext
    {
        var options = optionsBuilder?.Invoke(new OutboxNotificationEmailOptionsBuilder()).Build() ?? new OutboxNotificationEmailOptions();
        serviceBuilder.Services.AddSingleton(options);

        serviceBuilder.Services.AddSingleton<IOutboxNotificationEmailWorker, OutboxNotificationEmailWorker>();
        serviceBuilder.Services.AddSingleton<IOutboxNotificationEmailQueue>(sp =>
            new OutboxNotificationEmailQueue(
                sp.GetRequiredService<ILoggerFactory>(),
                id => sp.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(id)));
        serviceBuilder.Services.AddHostedService<OutboxNotificationEmailService>();
        serviceBuilder.Options.IsOutboxConfigured = true;

        return serviceBuilder;
    }
}