// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Selects durable LiteDB messaging through the existing messaging builder.</summary>
public static class LiteDbMessagingBuilderExtensions
{
    /// <summary>Registers the native broker, operational service, and bounded hosted delivery worker.</summary>
    /// <param name="context">The messaging builder.</param>
    /// <param name="configure">Optional database, collection, and delivery settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddMessaging().WithLiteDbBroker();</code></example>
    public static MessagingBuilderContext WithLiteDbBroker(this MessagingBuilderContext context, Action<LiteDbMessageBrokerOptions> configure = null)
    {
        var original = new LiteDbMessageBrokerOptions { CollectionPrefix = "messaging" };
        configure?.Invoke(original);
        var options = new LiteDbMessageBrokerOptions();
        foreach (var property in typeof(LiteDbMessageBrokerOptions).GetProperties().Where(p => p.CanRead && p.CanWrite)) { property.SetValue(options, property.GetValue(original)); }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDeliveryAttempts);
        if (options.BatchSize > options.MaxMaterializedRows || options.LeaseDuration <= TimeSpan.Zero || options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration || options.ProcessingInterval <= TimeSpan.Zero || options.StartupDelay < TimeSpan.Zero) { throw new ArgumentException("Durable broker timing and batch settings are invalid."); }

        var storage = context.Services.RegisterLiteDbFeature("messaging", ["messages", "identities", "paused"], o => { o.DatabaseName = options.DatabaseName; o.CollectionPrefix = options.CollectionPrefix; o.MaxMaterializedRows = options.MaxMaterializedRows; o.Serializer = options.Serializer; });
        context.Services.AddSingleton(sp =>
        {
            options.Serializer ??= storage.Serializer;
            options.LoggerFactory ??= sp.GetService<ILoggerFactory>();
            options.HandlerFactory ??= new ServiceProviderMessageHandlerFactory(sp);
            options.PublisherBehaviors = (options.PublisherBehaviors ?? []).Concat(sp.GetServices<IMessagePublisherBehavior>()).ToArray();
            options.HandlerBehaviors = (options.HandlerBehaviors ?? []).Concat(sp.GetServices<IMessageHandlerBehavior>()).ToArray();
            return new LiteDbMessageBroker(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), storage), options, sp.GetService<TimeProvider>());
        });
        context.Services.Replace(ServiceDescriptor.Singleton<IMessageBrokerRuntime>(sp => sp.GetRequiredService<LiteDbMessageBroker>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IMessageBroker>(sp => sp.GetRequiredService<LiteDbMessageBroker>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IMessageBrokerService>(sp => sp.GetRequiredService<LiteDbMessageBroker>()));
        context.Services.AddSingleton<IHostedService>(sp => new LiteDbMessageBrokerWorker(sp.GetRequiredService<LiteDbMessageBroker>(), options));
        return context;
    }
}
