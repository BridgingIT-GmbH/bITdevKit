namespace Microsoft.Extensions.DependencyInjection;

using System;
using BridgingIT.DevKit.Application.Notifications;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Registers notification services and fluent builders on the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds notification services for the specified message type.
    /// </summary>
    /// <typeparam name="TMessage">The notification message type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The optional configuration root used to bind SMTP and outbox options.</param>
    /// <param name="configure">The optional fluent builder delegate.</param>
    /// <returns>The fluent notification service builder.</returns>
    /// <example>
    /// <code>
    /// services.AddNotificationService&lt;EmailMessage&gt;(configuration, builder => builder
    ///     .WithSmtpClient()
    ///     .WithSmtpSettings(settings => settings.Sender("DoFiesta", "noreply@example.com")));
    /// </code>
    /// </example>
    public static NotificationServiceBuilder AddNotificationService<TMessage>(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<NotificationServiceBuilder> configure = null)
        where TMessage : class, INotificationMessage
    {
        var builder = new NotificationServiceBuilder(services);

        if (configuration != null)
        {
            builder.Options.SmtpSettings = configuration.GetSection("Notifications:Email:Smtp").Get<SmtpSettings>() ?? new SmtpSettings();
            builder.Options.OutboxOptions = configuration.GetSection("Notifications:Email:Outbox").Get<OutboxNotificationEmailOptions>() ?? new OutboxNotificationEmailOptions();
        }

        configure?.Invoke(builder);

        // builder.Validate();

        if (typeof(TMessage) == typeof(EmailMessage))
        {
            services.AddScoped<INotificationService<EmailMessage>, NotificationEmailService>();
        }

        if (!services.Any(d => d.ServiceType == typeof(INotificationStorageProvider)))
        {
            services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        }

        services.AddSingleton(builder.Options);

        return builder;
    }
}
