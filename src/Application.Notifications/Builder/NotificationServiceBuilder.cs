namespace BridgingIT.DevKit.Application.Notifications;

using System;
using BridgingIT.DevKit.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

public class NotificationServiceBuilder
{
    private readonly IServiceCollection services;
    private readonly NotificationServiceOptions options = new NotificationServiceOptions();

    public NotificationServiceBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public NotificationServiceBuilder WithSmtpSettings(Action<SmtpSettings> configure = null)
    {
        if (configure != null)
        {
            configure(this.options.SmtpSettings);
        }
        return this;
    }

    public NotificationServiceBuilder WithEntityFrameworkProvider<TContext>()
        where TContext : DbContext, INotificationEmailContext
    {
        this.services.AddScoped<INotificationStorageProvider, EntityFrameworkNotificationStorageProvider<TContext>>();
        return this;
    }

    public NotificationServiceBuilder WithInMemoryProvider()
    {
        this.services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        return this;
    }

    public NotificationServiceBuilder WithOutbox<TContext>(
        Builder<OutboxNotificationEmailOptionsBuilder, OutboxNotificationEmailOptions> optionsBuilder = null)
        where TContext : DbContext, INotificationEmailContext
    {
        this.services.AddOutboxNotificationEmailService<TContext>(optionsBuilder);
        this.options.IsOutboxConfigured = true;
        return this;
    }

    public NotificationServiceBuilder WithRetryer(Action<RetryerBuilder> configure)
    {
        configure?.Invoke(new RetryerBuilder());
        return this;
    }

    public NotificationServiceBuilder WithThrottler(Action<ThrottlerBuilder> configure)
    {
        configure?.Invoke(new ThrottlerBuilder());
        return this;
    }

    public NotificationServiceBuilder WithTimeout(TimeSpan timeout)
    {
        this.options.Timeout = timeout;
        return this;
    }

    private void Validate()
    {
        if (string.IsNullOrEmpty(this.options.SmtpSettings.Host) || this.options.SmtpSettings.Port == 0)
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
            builder.options.SmtpSettings = configuration.GetSection("NotificationService:Email:Smtp").Get<SmtpSettings>() ?? new SmtpSettings();
            builder.options.OutboxOptions = configuration.GetSection("NotificationService:Email:Outbox").Get<OutboxNotificationEmailOptions>() ?? new OutboxNotificationEmailOptions();
        }

        configure?.Invoke(builder);

        builder.Validate();

        if (typeof(TMessage) == typeof(EmailNotificationMessage))
        {
            services.AddScoped<INotificationService<EmailNotificationMessage>, EmailService>();
        }

        if (!services.Any(d => d.ServiceType == typeof(INotificationStorageProvider)))
        {
            services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        }

        services.AddSingleton(builder.options);

        return builder;
    }

    public static IServiceCollection AddOutboxNotificationEmailService<TContext>(
        this IServiceCollection services,
        Builder<OutboxNotificationEmailOptionsBuilder, OutboxNotificationEmailOptions> optionsBuilder = null)
        where TContext : DbContext, INotificationEmailContext
    {
        var options = optionsBuilder?.Invoke(new OutboxNotificationEmailOptionsBuilder()).Build() ?? new OutboxNotificationEmailOptions();
        services.AddSingleton(options);
        services.AddSingleton<IOutboxNotificationEmailWorker, OutboxNotificationEmailWorker>();
        services.AddSingleton<IOutboxNotificationEmailQueue>(sp => new OutboxNotificationEmailQueue(
            sp.GetRequiredService<ILoggerFactory>(),
            id => sp.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(id)));
        services.AddHostedService<OutboxNotificationEmailService>();
        return services;
    }
}

public class RetryerBuilder
{
    public RetryerBuilder MaxRetries(int maxRetries) => this;
    public RetryerBuilder Delay(TimeSpan delay) => this;
    public RetryerBuilder UseExponentialBackoff() => this;
    public RetryerBuilder WithProgress(IProgress<RetryProgress> progress) => this;
}

public class ThrottlerBuilder
{
    public ThrottlerBuilder Interval(TimeSpan interval) => this;
    public ThrottlerBuilder WithProgress(IProgress<ThrottlerProgress> progress) => this;
}

public class RetryProgress { }

public class ThrottlerProgress { }