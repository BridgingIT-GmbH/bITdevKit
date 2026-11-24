// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace Microsoft.Extensions.DependencyInjection;

using System.Text.RegularExpressions;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.LiteDb;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;

/// <summary>Registers portable history capture and read/restore services for explicitly tracked LiteDB entities.</summary>
public static class LiteDbChangeHistoryServiceCollectionExtensions
{
    /// <summary>Registers history in the same named database as the tracked repositories or ActiveEntity providers.</summary>
    /// <param name="services">The service collection with the entity providers already registered.</param>
    /// <param name="configure">The tracked entities, capture policies, and restore allowlist.</param>
    /// <param name="databaseName">The named database registration.</param>
    /// <returns>The service collection.</returns>
    /// <example><code>services.AddLiteDbChangeHistory(o => o.Track&lt;LedgerEntry&gt;().CaptureChanges().AllowRestore(e => e.Name).UseValidatedSetter());</code></example>
    public static IServiceCollection AddLiteDbChangeHistory(this IServiceCollection services, Action<LiteDbChangeHistoryOptions> configure,
        string databaseName = "default")
    {
        ArgumentNullException.ThrowIfNull(configure);
        var configured = new LiteDbChangeHistoryOptions();
        configure(configured);
        configured.Validate();
        var options = Snapshot(configured);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxMaterializedRows);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(options.MaxMaterializedRows, int.MaxValue);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.DefaultUpdateSetMaxAffectedRows);
        if (options.MaxStoredValueLength < 1) { throw new ArgumentException("MaxStoredValueLength must be positive when specified."); }

        if (!Regex.IsMatch(options.CollectionName, "^[A-Za-z_][A-Za-z0-9_]{0,59}$", RegexOptions.CultureInvariant)) { throw new ArgumentException("The history collection name is invalid."); }

        if (options.TrackedEntities.Count == 0) { throw new ArgumentException("At least one history entity must be tracked."); }

        var registration = LiteDbDatabaseRegistry.GetOrAdd(services).Get(databaseName);
        registration.AddEntity(typeof(ChangeHistoryRecord), options.CollectionName, _ => { }, "ChangeHistory");
        foreach (var tracked in options.TrackedEntities.Values)
        {
            if ((tracked.CaptureStrategy ?? options.DefaultCaptureStrategy) == ChangeHistoryCaptureStrategy.EfChangeTracker)
            {
                throw new ArgumentException("LiteDB history supports EntityChangeOnly and RepositorySnapshot capture strategies.");
            }

            if (tracked.UpdateSetMaxAffectedRows < 1) { throw new ArgumentException("Update-set history limits must be positive."); }

            var collection = registration.GetCollection(tracked.EntityType);
            var serviceType = typeof(IChangeHistoryService<,>).MakeGenericType(tracked.EntityType, typeof(LiteDbContext));
            var implementation = typeof(LiteDbChangeHistoryService<>).MakeGenericType(tracked.EntityType);
            if (services.Any(d => d.IsKeyedService && d.ServiceType == serviceType && Equals(d.ServiceKey, databaseName)))
            {
                throw new InvalidOperationException("History is already registered for this entity and database.");
            }

            services.AddKeyedScoped(serviceType, databaseName, (sp, _) => ActivatorUtilities.CreateInstance(sp, implementation,
                sp.GetRequiredKeyedService<LiteDbDatabaseSession>(databaseName), options, collection));
            if (databaseName == "default") { services.AddScoped(serviceType, sp => sp.GetRequiredKeyedService(serviceType, databaseName)); }
        }

        services.AddKeyedScoped<ILiteDbEntityMutationObserver>(databaseName, (sp, _) => new LiteDbChangeHistoryCapture(options,
            sp.GetService<ICurrentUserAccessor>(), sp.GetService<TimeProvider>() ?? TimeProvider.System));
        return services;
    }

    private static LiteDbChangeHistoryOptions Snapshot(LiteDbChangeHistoryOptions source)
    {
        var copy = new LiteDbChangeHistoryOptions
        {
            CollectionName = source.CollectionName, MaxMaterializedRows = source.MaxMaterializedRows,
            DefaultCaptureStrategy = source.DefaultCaptureStrategy, DefaultUpdateSetMaxAffectedRows = source.DefaultUpdateSetMaxAffectedRows,
            MaxStoredValueLength = source.MaxStoredValueLength, OversizedValuePolicy = source.OversizedValuePolicy,
            SensitiveValuePolicy = source.SensitiveValuePolicy, ProtectSensitivePropertyNames = source.ProtectSensitivePropertyNames,
            ReadAuthorizationPolicy = source.ReadAuthorizationPolicy, RestoreAuthorizationPolicy = source.RestoreAuthorizationPolicy
        };
        foreach (var entity in source.TrackedEntities.Values)
        {
            var target = new ChangeHistoryEntityOptions(entity.EntityType)
            {
                CaptureStrategy = entity.CaptureStrategy, CaptureDirectMutations = entity.CaptureDirectMutations, DirectMutationMode = entity.DirectMutationMode,
                CaptureCreates = entity.CaptureCreates, CaptureUpdateSet = entity.CaptureUpdateSet, UpdateSetMode = entity.UpdateSetMode,
                UpdateSetMaxAffectedRows = entity.UpdateSetMaxAffectedRows, BulkInsertCaptureMode = entity.BulkInsertCaptureMode,
                BulkInsertMaxDetailedEntities = entity.BulkInsertMaxDetailedEntities, RestoreConcurrencyPolicy = entity.RestoreConcurrencyPolicy,
                RestoreAuthorizerType = entity.RestoreAuthorizerType
            };
            foreach (var pair in entity.PropertyPolicies) { target.PropertyPolicies[pair.Key] = pair.Value; }

            foreach (var pair in entity.RestorePolicies)
            {
                target.RestorePolicies[pair.Key] = new(pair.Key)
                {
                    ExecutionMode = pair.Value.ExecutionMode, DomainMethod = pair.Value.DomainMethod, HandlerType = pair.Value.HandlerType, HandlerName = pair.Value.HandlerName
                };
            }

            foreach (var path in entity.CapturePaths)
            {
                var targetPath = new ChangeHistoryCapturePathOptions(path.Path, path.Kind)
                {
                    IncludePath = path.IncludePath, CollectionItemType = path.CollectionItemType, CollectionItemIdentity = path.CollectionItemIdentity,
                    RestorePlanName = path.RestorePlanName, RestorePlanType = path.RestorePlanType, RequireExplicitGraphIdentities = path.RequireExplicitGraphIdentities
                };
                foreach (var pair in path.GraphIdentities) { targetPath.GraphIdentities[pair.Key] = new(pair.Key, pair.Value.Identity); }

                target.CapturePaths.Add(targetPath);
            }

            copy.TrackedEntities[entity.EntityType] = target;
        }

        return copy;
    }
}
