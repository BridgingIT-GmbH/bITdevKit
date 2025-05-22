namespace BridgingIT.DevKit.Application.Notifications;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class NotificationServiceBuilder
{
    protected readonly IServiceCollection Services;
    public readonly NotificationServiceOptions Options = new NotificationServiceOptions();

    public NotificationServiceBuilder(IServiceCollection services)
    {
        this.Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public virtual NotificationServiceBuilder WithSmtpSettings(Action<SmtpSettings> configure = null)
    {
        if (configure != null)
        {
            configure(this.Options.SmtpSettings);
        }
        return this;
    }

    public virtual NotificationServiceBuilder WithInMemoryProvider()
    {
        this.Services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        return this;
    }

    public virtual NotificationServiceBuilder WithStorageProvider<TProvider>()
        where TProvider : class, INotificationStorageProvider
    {
        this.Services.AddScoped<INotificationStorageProvider, TProvider>();
        return this;
    }

    public virtual NotificationServiceBuilder WithRetryer(Action<RetryerBuilder> configure)
    {
        configure?.Invoke(new RetryerBuilder());
        return this;
    }

    public virtual NotificationServiceBuilder WithTimeout(TimeSpan timeout)
    {
        this.Options.Timeout = timeout;
        return this;
    }

    protected virtual void Validate()
    {
        if (string.IsNullOrEmpty(this.Options.SmtpSettings.Host) || this.Options.SmtpSettings.Port == 0)
        {
            throw new InvalidOperationException("SMTP host and port must be configured.");
        }
    }
}

public static class NotificationServiceExtensions
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

        //builder.Validate();

        if (typeof(TMessage) == typeof(EmailNotificationMessage))
        {
            services.AddScoped<INotificationService<EmailNotificationMessage>, EmailService>();
        }

        if (!services.Any(d => d.ServiceType == typeof(INotificationStorageProvider)))
        {
            services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        }

        services.AddSingleton(builder.Options);

        return builder;
    }
}

public class RetryerBuilder
{
    public RetryerBuilder MaxRetries(int maxRetries) => this;
    public RetryerBuilder Delay(TimeSpan delay) => this;
    public RetryerBuilder UseExponentialBackoff() => this;
    public RetryerBuilder WithProgress(IProgress<RetryProgress> progress) => this;
}

public class RetryProgress { }