// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using BridgingIT.DevKit.Application.Messaging;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Selects outgoing LiteDB outbox capture independently of the receiving broker.</summary>
public static class LiteDbMessageOutboxBuilderExtensions
{
    /// <summary>Preserves the selected delivery broker and registers a scoped transactional publisher for callers.</summary>
    /// <param name="context">The builder after selecting its receiving broker.</param>
    /// <param name="configure">Optional outbox database, namespace, lease, and polling settings.</param>
    /// <returns>The same builder.</returns>
    /// <example><code>services.AddMessaging().WithLiteDbBroker().WithLiteDbOutbox();</code></example>
    public static MessagingBuilderContext WithLiteDbOutbox(this MessagingBuilderContext context, Action<LiteDbMessageBrokerOptions> configure = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = context.Services;
        if (services.Any(d => d.ServiceType == typeof(LiteDbOutboxMessageWorker))) { throw new InvalidOperationException("The outgoing LiteDB message outbox is already selected."); }

        var delivery = services.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(IMessageBroker))
            ?? throw new InvalidOperationException("Select a receiving message broker before adding outgoing outbox capture.");
        var original = new LiteDbMessageBrokerOptions { CollectionPrefix = "message_outbox", MessageExpiration = null };
        configure?.Invoke(original);
        var options = new LiteDbMessageBrokerOptions();
        foreach (var property in typeof(LiteDbMessageBrokerOptions).GetProperties().Where(p => p.CanRead && p.CanWrite)) { property.SetValue(options, property.GetValue(original)); }

        if (options.BatchSize < 1 || options.BatchSize > options.MaxMaterializedRows || options.MaxDeliveryAttempts < 1 || options.LeaseDuration <= TimeSpan.Zero || options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration || options.ProcessingInterval <= TimeSpan.Zero || options.StartupDelay < TimeSpan.Zero) { throw new ArgumentException("The outgoing outbox limits and lease settings are invalid."); }

        var storage = services.RegisterLiteDbFeature("message_outbox", ["messages", "identities", "paused"], o =>
        {
            o.DatabaseName = options.DatabaseName; o.CollectionPrefix = options.CollectionPrefix; o.MaxMaterializedRows = options.MaxMaterializedRows; o.Serializer = options.Serializer;
        });
        options.Serializer ??= storage.Serializer;
        var admissionBehaviors = (options.PublisherBehaviors ?? []).ToArray();
        services.Add(new ServiceDescriptor(typeof(IMessageBroker), LiteDbOutboxMessageForwarder.DeliveryBrokerKey, (sp, _) =>
            delivery.ImplementationInstance ?? delivery.ImplementationFactory?.Invoke(sp) ?? ActivatorUtilities.CreateInstance(sp, delivery.ImplementationType!), delivery.Lifetime));
        services.AddScoped(sp => new LiteDbOutboxMessageForwarder(sp, options.Serializer));
        services.AddSingleton(sp =>
        {
            options.HandlerFactory = new ServiceProviderMessageHandlerFactory(sp);
            options.LoggerFactory ??= sp.GetService<ILoggerFactory>();
            var broker = new LiteDbMessageBroker(new LiteDbFeatureDatabase(sp.GetRequiredService<IServiceScopeFactory>(), storage), options, sp.GetService<TimeProvider>());
            broker.RegisterOutboxHandler<LiteDbOutboxDispatchMessage, LiteDbOutboxMessageForwarder>();
            return new LiteDbOutboxMessageWorker(broker, options.Serializer);
        });
        services.AddSingleton<IOutboxMessageWorker>(sp => sp.GetRequiredService<LiteDbOutboxMessageWorker>());
        services.Remove(delivery);
        services.AddScoped<IMessageBroker>(sp => new LiteDbOutboxMessagePublisher(sp.GetRequiredService<LiteDbOutboxMessageWorker>(), sp.GetRequiredKeyedService<LiteDbDatabaseSession>(options.DatabaseName), sp, admissionBehaviors.Concat(sp.GetServices<IMessagePublisherBehavior>())));
        var hosted = new OutboxMessageOptions { Enabled = options.Enabled, StartupDelay = options.StartupDelay, ProcessingInterval = options.ProcessingInterval };
        services.AddSingleton<IHostedService>(sp => new OutboxMessageService(sp.GetService<ILoggerFactory>(), sp.GetRequiredService<IOutboxMessageWorker>(), sp.GetRequiredService<IHostApplicationLifetime>(), hosted));
        return context;
    }
}
