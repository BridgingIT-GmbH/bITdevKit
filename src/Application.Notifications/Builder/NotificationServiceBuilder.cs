// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

/// <summary>
/// Provides fluent registration helpers for the notification service infrastructure.
/// </summary>
/// <example>
/// <code>
/// services.AddNotificationService&lt;EmailMessage&gt;(configuration, builder => builder
///     .WithSmtpClient()
///     .WithInMemoryStorageProvider());
/// </code>
/// </example>
public class NotificationServiceBuilder(IServiceCollection Services)
{
    /// <summary>
    /// Gets the service collection used for registration.
    /// </summary>
    public readonly IServiceCollection Services = Services ?? throw new ArgumentNullException(nameof(Services));

    /// <summary>
    /// Gets the mutable notification options assembled by the builder.
    /// </summary>
    public readonly NotificationServiceOptions Options = new();

    /// <summary>
    /// Configures the SMTP settings used by the notification service.
    /// </summary>
    /// <param name="configure">The delegate that mutates the SMTP settings.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithSmtpSettings(Action<SmtpSettings> configure = null)
    {
        configure?.Invoke(this.Options.SmtpSettings);
        return this;
    }

    /// <summary>
    /// Registers the MailKit SMTP client implementation.
    /// </summary>
    /// <param name="enabled">Controls whether the SMTP client should be registered.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithSmtpClient(bool enabled = true)
    {
        if (!enabled)
        {
            return this;
        }

        this.Services.AddSingleton<ISmtpClient, SmtpClient>();
        return this;
    }

    /// <summary>
    /// Registers the fake SMTP client using default options.
    /// </summary>
    /// <param name="enabled">Controls whether the fake SMTP client should be registered.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithFakeSmtpClient(bool enabled = true)
    {
        return this.WithFakeSmtpClient(null, enabled);
    }

    /// <summary>
    /// Registers the fake SMTP client with explicit options.
    /// </summary>
    /// <param name="options">The fake SMTP client behavior options.</param>
    /// <param name="enabled">Controls whether the fake SMTP client should be registered.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithFakeSmtpClient(FakeSmtpClientOptions options, bool enabled = true)
    {
        if (!enabled)
        {
            return this;
        }

        this.Services.AddSingleton<ISmtpClient>(sp =>
            new FakeSmtpClient(
                sp.GetRequiredService<ILogger<FakeSmtpClient>>(),
                options));
        return this;
    }

    /// <summary>
    /// Registers the in-memory notification storage provider.
    /// </summary>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithInMemoryStorageProvider()
    {
        this.Services.AddSingleton<INotificationStorageProvider, InMemoryNotificationStorageProvider>();
        return this;
    }

    /// <summary>
    /// Registers a custom notification storage provider.
    /// </summary>
    /// <typeparam name="TProvider">The notification storage provider type.</typeparam>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithStorageProvider<TProvider>()
        where TProvider : class, INotificationStorageProvider
    {
        this.Services.AddScoped<INotificationStorageProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// Configures retry behavior metadata for notification delivery.
    /// </summary>
    /// <param name="configure">The retry builder delegate.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithRetryer(Action<RetryerBuilder> configure)
    {
        configure?.Invoke(new RetryerBuilder());
        return this;
    }

    /// <summary>
    /// Sets the notification delivery timeout.
    /// </summary>
    /// <param name="timeout">The timeout value.</param>
    /// <returns>The current builder.</returns>
    public virtual NotificationServiceBuilder WithTimeout(TimeSpan timeout)
    {
        this.Options.Timeout = timeout;
        return this;
    }

    /// <summary>
    /// Validates the assembled notification configuration.
    /// </summary>
    protected virtual void Validate()
    {
        if (string.IsNullOrEmpty(this.Options.SmtpSettings.Host) || this.Options.SmtpSettings.Port == 0)
        {
            throw new InvalidOperationException("SMTP host and port must be configured.");
        }
    }
}

/// <summary>
/// Provides a placeholder fluent surface for retry-related settings.
/// </summary>
public class RetryerBuilder
{
    /// <summary>
    /// Sets the maximum retry count.
    /// </summary>
    /// <param name="maxRetries">The maximum retry count.</param>
    /// <returns>The current builder.</returns>
    public RetryerBuilder MaxRetries(int maxRetries) => this;

    /// <summary>
    /// Sets the base retry delay.
    /// </summary>
    /// <param name="delay">The retry delay.</param>
    /// <returns>The current builder.</returns>
    public RetryerBuilder Delay(TimeSpan delay) => this;

    /// <summary>
    /// Enables exponential backoff.
    /// </summary>
    /// <returns>The current builder.</returns>
    public RetryerBuilder UseExponentialBackoff() => this;

    /// <summary>
    /// Registers a retry progress callback.
    /// </summary>
    /// <param name="progress">The retry progress receiver.</param>
    /// <returns>The current builder.</returns>
    public RetryerBuilder WithProgress(IProgress<RetryProgress> progress) => this;
}

/// <summary>
/// Represents retry progress notifications emitted by <see cref="RetryerBuilder" />.
/// </summary>
public class RetryProgress;
