namespace Microsoft.Extensions.DependencyInjection;

using System;
using BridgingIT.DevKit.Application.Notifications;
using Microsoft.Extensions.Configuration;

public static class ServiceCollectionExtensions
{
    public static NotificationServiceBuilder AddNotificationService<TMessage>(
        this IServiceCollection services,
        IConfiguration configuration = null,
        Action<NotificationServiceBuilder> configure = null)
        where TMessage : class, INotificationMessage
    {
        var builder = new NotificationServiceBuilder(services);

        if (configuration != null)
        {
            builder.Options.SmtpSettings = configuration.GetSection("NotificationService:Email:Smtp").Get<SmtpSettings>() ?? new SmtpSettings();
            builder.Options.OutboxOptions = configuration.GetSection("NotificationService:Email:Outbox").Get<OutboxNotificationEmailOptions>() ?? new OutboxNotificationEmailOptions();
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
