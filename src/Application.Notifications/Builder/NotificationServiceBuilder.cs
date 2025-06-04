// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Notifications;

using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

public class NotificationServiceBuilder(IServiceCollection Services)
{
    public readonly IServiceCollection Services = Services ?? throw new ArgumentNullException(nameof(Services));
    public readonly NotificationServiceOptions Options = new();

    public virtual NotificationServiceBuilder WithSmtpSettings(Action<SmtpSettings> configure = null)
    {
        configure?.Invoke(this.Options.SmtpSettings);
        return this;
    }

    public virtual NotificationServiceBuilder WithSmtpClient(bool enabled = true)
    {
        if (!enabled)
        {
            return this;
        }

        this.Services.AddSingleton<ISmtpClient, SmtpClient>();
        return this;
    }

    public virtual NotificationServiceBuilder WithFakeSmtpClient(bool enabled = true)
    {
        return this.WithFakeSmtpClient(null, enabled);
    }

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

    public virtual NotificationServiceBuilder WithInMemoryStorageProvider()
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

public class RetryerBuilder
{
    public RetryerBuilder MaxRetries(int maxRetries) => this;

    public RetryerBuilder Delay(TimeSpan delay) => this;

    public RetryerBuilder UseExponentialBackoff() => this;

    public RetryerBuilder WithProgress(IProgress<RetryProgress> progress) => this;
}

public class RetryProgress;