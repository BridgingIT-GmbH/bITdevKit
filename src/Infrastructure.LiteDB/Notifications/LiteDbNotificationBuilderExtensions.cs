// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Notifications;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Selects native notification storage while reusing the existing email transport and outbox pipeline.</summary>
public static class LiteDbNotificationBuilderExtensions
{
    /// <summary>Registers email persistence, operational queries, and optionally the existing outbox hosted pipeline.</summary>
    /// <param name="builder">The notification service builder.</param>
    /// <param name="configure">Optional database, namespace, and bounded storage settings.</param>
    /// <param name="outbox">Optional existing outbox settings; supplying them enables background delivery.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>builder.WithLiteDbStorageProvider(outbox: new OutboxNotificationEmailOptions());</code></example>
    public static NotificationServiceBuilder WithLiteDbStorageProvider(this NotificationServiceBuilder builder, Action<LiteDbFeatureOptions> configure = null, OutboxNotificationEmailOptions outbox = null)
    {
        var options = builder.Services.RegisterLiteDbFeature("notifications", ["emails"], configure);
        var workerOptions = new OutboxNotificationEmailOptions();
        if (outbox is not null)
        {
            foreach (var property in typeof(OutboxNotificationEmailOptions).GetProperties().Where(p => p.CanRead && p.CanWrite)) { property.SetValue(workerOptions, property.GetValue(outbox)); }
        }

        if (workerOptions.LeaseDuration <= TimeSpan.Zero || workerOptions.RetryCount < 1 || workerOptions.ProcessingCount < 1) { throw new ArgumentException("Notification lease, retry, and processing settings must be positive."); }

        builder.Services.AddSingleton(sp => new LiteDbNotificationStorageProvider(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), options), workerOptions, sp.GetService<TimeProvider>() ?? TimeProvider.System));
        builder.Services.Replace(ServiceDescriptor.Singleton<INotificationStorageProvider>(sp => sp.GetRequiredService<LiteDbNotificationStorageProvider>()));
        builder.Services.Replace(ServiceDescriptor.Singleton<INotificationEmailOutboxService>(sp => sp.GetRequiredService<LiteDbNotificationStorageProvider>()));
        if (outbox is not null)
        {
            builder.Services.AddSingleton(workerOptions);
            builder.Services.AddSingleton(sp => new LiteDbNotificationEmailOutboxWorker(sp.GetRequiredService<LiteDbNotificationStorageProvider>(),
                sp.GetRequiredService<IServiceScopeFactory>(), workerOptions, sp.GetService<TimeProvider>() ?? TimeProvider.System, sp.GetService<ILoggerFactory>()));
            builder.Services.Replace(ServiceDescriptor.Singleton<IOutboxNotificationEmailWorker>(sp => sp.GetRequiredService<LiteDbNotificationEmailOutboxWorker>()));
            builder.Services.TryAddSingleton<IOutboxNotificationEmailQueue>(sp => new OutboxNotificationEmailQueue(sp.GetService<ILoggerFactory>(), id => sp.GetRequiredService<IOutboxNotificationEmailWorker>().ProcessAsync(id)));
            builder.Services.AddHostedService<OutboxNotificationEmailService>();
            builder.Options.OutboxOptions = workerOptions;
            builder.Options.IsOutboxConfigured = true;
        }

        return builder;
    }
}
