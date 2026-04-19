// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Reflection;
using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Configures Entity Framework persistence for the notification service.
/// </summary>
public class NotificationServiceInfrastructureBuilder(IServiceCollection services) : NotificationServiceBuilder(services)
{
    /// <summary>
    /// Registers the Entity Framework backed notification email store and operational outbox service.
    /// </summary>
    /// <typeparam name="TContext">The database context type implementing <see cref="INotificationEmailContext"/>.</typeparam>
    /// <returns>The current notification service builder.</returns>
    public NotificationServiceInfrastructureBuilder WithEntityFrameworkStorageProvider<TContext>()
        where TContext : DbContext, INotificationEmailContext
    {
        this.Services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationEmailStorageProvider<TContext>>();
        this.Services.AddScoped<INotificationEmailOutboxService, EntityFrameworkNotificationEmailOutboxService<TContext>>();
        return this;
    }

    /// <summary>
    /// Enables the hosted outbox processing pipeline for Entity Framework stored notification emails.
    /// </summary>
    /// <typeparam name="TContext">The database context type implementing <see cref="INotificationEmailContext"/>.</typeparam>
    /// <param name="optionsBuilder">Optional builder used to customize outbox processing behavior.</param>
    /// <returns>The current notification service builder.</returns>
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

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            this.Services.AddHostedService<OutboxNotificationEmailService>();
        }

        this.Options.IsOutboxConfigured = true;

        return this;
    }

    private static bool IsBuildTimeOpenApiGeneration() // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&tabs=visual-studio%2Cvisual-studio-code#customizing-run-time-behavior-during-build-time-document-generation
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}

/// <summary>
/// Adds Entity Framework based notification registration helpers to <see cref="NotificationServiceBuilder"/>.
/// </summary>
public static class NotificationServiceBuilderExtensions
{
    /// <summary>
    /// Registers the Entity Framework backed notification email store and operational outbox service.
    /// </summary>
    /// <typeparam name="TContext">The database context type implementing <see cref="INotificationEmailContext"/>.</typeparam>
    /// <param name="serviceBuilder">The notification service builder.</param>
    /// <returns>The current notification service builder.</returns>
    public static NotificationServiceBuilder WithEntityFrameworkStorageProvider<TContext>(this NotificationServiceBuilder serviceBuilder)
        where TContext : DbContext, INotificationEmailContext
    {
        serviceBuilder.Services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationEmailStorageProvider<TContext>>();
        serviceBuilder.Services.AddScoped<INotificationEmailOutboxService, EntityFrameworkNotificationEmailOutboxService<TContext>>();
        return serviceBuilder;
    }

    /// <summary>
    /// Enables the hosted outbox processing pipeline for Entity Framework stored notification emails.
    /// </summary>
    /// <typeparam name="TContext">The database context type implementing <see cref="INotificationEmailContext"/>.</typeparam>
    /// <param name="serviceBuilder">The notification service builder.</param>
    /// <param name="optionsBuilder">Optional builder used to customize outbox processing behavior.</param>
    /// <returns>The current notification service builder.</returns>
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

        if (!IsBuildTimeOpenApiGeneration()) // avoid hosted service during build-time openapi generation
        {
            serviceBuilder.Services.AddHostedService<OutboxNotificationEmailService>();
        }

        serviceBuilder.Options.IsOutboxConfigured = true;

        return serviceBuilder;
    }

    private static bool IsBuildTimeOpenApiGeneration() // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0&tabs=visual-studio%2Cvisual-studio-code#customizing-run-time-behavior-during-build-time-document-generation
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}
