// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Outbox;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Registers atomic domain-event capture and durable background delivery for a selected LiteDB database.</summary>
public static class LiteDbDomainEventOutboxServiceCollectionExtensions
{
    /// <summary>Adds domain-event capture, the existing worker contract, and a background processing service.</summary>
    /// <param name="services">The host registrations, including the selected database and DevKit notifier.</param>
    /// <param name="databaseName">The named database registration.</param>
    /// <param name="configure">Optional bounded processing and serialization settings.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbDomainEventOutbox(configure: o => o.ProcessingCount = 50);</code></example>
    public static IServiceCollection AddLiteDbDomainEventOutbox(this IServiceCollection services, string databaseName = "default",
        Action<LiteDbDomainEventOutboxOptions> configure = null)
    {
        var configured = new LiteDbDomainEventOutboxOptions();
        configure?.Invoke(configured);
        var options = new LiteDbDomainEventOutboxOptions();
        foreach (var property in typeof(LiteDbDomainEventOutboxOptions).GetProperties().Where(p => p.CanRead && p.CanWrite))
        {
            property.SetValue(options, property.GetValue(configured));
        }

        options.Serializer ??= new SystemTextJsonSerializer();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.BatchSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.BatchSize, 10000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ProcessingCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.RetryCount);
        if (options.LeaseDuration <= TimeSpan.Zero || options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration)
        {
            throw new ArgumentException("Lease renewal must be positive and shorter than the lease duration.");
        }

        if (options.ProcessingInterval <= TimeSpan.Zero || options.StartupDelay < TimeSpan.Zero || options.ProcessingDelay < TimeSpan.Zero || options.ProcessingJitter < TimeSpan.Zero || options.AutoArchiveAfter < TimeSpan.Zero)
        {
            throw new ArgumentException("Outbox processing intervals must be valid positive durations.");
        }

        if (!Regex.IsMatch(options.CollectionName, "^[A-Za-z_][A-Za-z0-9_]{0,59}$", RegexOptions.CultureInvariant)) { throw new ArgumentException("The outbox collection name is invalid."); }

        if (services.Any(d => d.IsKeyedService && d.ServiceType == typeof(LiteDbDomainEventOutboxWorker) && Equals(d.ServiceKey, databaseName)))
        {
            throw new InvalidOperationException("The database already has a LiteDB domain-event outbox.");
        }

        LiteDbDatabaseRegistry.GetOrAdd(services).Get(databaseName).AddEntity(typeof(LiteDbOutboxDomainEvent), options.CollectionName, _ => { }, "DomainEventOutbox");
        services.AddKeyedSingleton<LiteDbDomainEventOutboxWorker>(databaseName, (sp, _) => new LiteDbDomainEventOutboxWorker(
            sp.GetRequiredService<IServiceScopeFactory>(), databaseName, options, sp.GetService<TimeProvider>() ?? TimeProvider.System,
            sp.GetRequiredService<ILogger<LiteDbDomainEventOutboxWorker>>()));
        services.AddKeyedSingleton<IOutboxDomainEventWorker>(databaseName, (sp, _) => sp.GetRequiredKeyedService<LiteDbDomainEventOutboxWorker>(databaseName));
        if (databaseName == "default") { services.AddSingleton<IOutboxDomainEventWorker>(sp => sp.GetRequiredKeyedService<LiteDbDomainEventOutboxWorker>(databaseName)); }

        services.AddKeyedScoped<ILiteDbEntityMutationObserver>(databaseName, (sp, _) => new LiteDbDomainEventCapture(options, sp.GetRequiredKeyedService<LiteDbDomainEventOutboxWorker>(databaseName)));
        services.AddSingleton<IHostedService>(sp => new OutboxDomainEventService(sp.GetService<ILoggerFactory>(),
            sp.GetRequiredKeyedService<LiteDbDomainEventOutboxWorker>(databaseName), sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetService<IDatabaseReadyService>(), options));
        services.AddSingleton<IHostedService>(sp => new LiteDbDomainEventOutboxService(sp.GetRequiredKeyedService<LiteDbDomainEventOutboxWorker>(databaseName), options, sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetRequiredService<ILogger<LiteDbDomainEventOutboxService>>(), sp.GetService<IDatabaseReadyService>()));
        return services;
    }
}
