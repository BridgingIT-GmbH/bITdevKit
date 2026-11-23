// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Queueing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Selects durable LiteDB queueing through the existing queueing builder.</summary>
public static class LiteDbQueueingBuilderExtensions
{
    /// <summary>Registers the native broker, operational service, and existing queueing background processor.</summary>
    /// <param name="context">The queueing builder.</param>
    /// <param name="configure">Optional database, collection, and delivery settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddQueueing().WithLiteDbBroker();</code></example>
    public static QueueingBuilderContext WithLiteDbBroker(this QueueingBuilderContext context, Action<LiteDbQueueBrokerOptions> configure = null)
    {
        var original = new LiteDbQueueBrokerOptions { CollectionPrefix = "queueing" };
        configure?.Invoke(original);
        var options = new LiteDbQueueBrokerOptions();
        foreach (var property in typeof(LiteDbQueueBrokerOptions).GetProperties().Where(p => p.CanRead && p.CanWrite)) { property.SetValue(options, property.GetValue(original)); }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDeliveryAttempts);
        if (options.BatchSize > options.MaxMaterializedRows || options.LeaseDuration <= TimeSpan.Zero || options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration || options.ProcessingInterval <= TimeSpan.Zero || options.StartupDelay < TimeSpan.Zero) { throw new ArgumentException("Durable broker timing and batch settings are invalid."); }

        var storage = context.Services.RegisterLiteDbFeature("queueing", ["messages", "identities", "paused"], o => { o.DatabaseName = options.DatabaseName; o.CollectionPrefix = options.CollectionPrefix; o.MaxMaterializedRows = options.MaxMaterializedRows; o.Serializer = options.Serializer; });
        context.Services.AddSingleton(sp =>
        {
            options.Serializer ??= storage.Serializer;
            options.LoggerFactory ??= sp.GetService<ILoggerFactory>();
            options.HandlerFactory ??= new ServiceProviderQueueMessageHandlerFactory(sp);
            options.EnqueuerBehaviors = (options.EnqueuerBehaviors ?? []).Concat(sp.GetServices<IQueueEnqueuerBehavior>()).ToArray();
            options.HandlerBehaviors = (options.HandlerBehaviors ?? []).Concat(sp.GetServices<IQueueHandlerBehavior>()).ToArray();
            return new LiteDbQueueBroker(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), storage), options, sp.GetService<TimeProvider>());
        });
        context.Services.Replace(ServiceDescriptor.Singleton<IQueueBrokerRuntime>(sp => sp.GetRequiredService<LiteDbQueueBroker>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IQueueBroker>(sp => sp.GetRequiredService<LiteDbQueueBroker>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IQueueBrokerService>(sp => sp.GetRequiredService<LiteDbQueueBroker>()));
        context.Services.Replace(ServiceDescriptor.Singleton<IQueueBrokerBackgroundProcessor>(sp => sp.GetRequiredService<LiteDbQueueBroker>()));
        return context;
    }
}
