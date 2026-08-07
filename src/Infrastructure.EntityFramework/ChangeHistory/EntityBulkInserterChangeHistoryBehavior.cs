// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Repositories;

using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// Persists explicitly configured ChangeHistory rows in the same transaction as a native entity bulk insert.
/// </summary>
/// <typeparam name="TEntity">The entity type inserted by the decorated bulk inserter.</typeparam>
/// <typeparam name="TContext">The EF Core context that stores both entities and ChangeHistory rows.</typeparam>
/// <remarks>
/// Register this behavior explicitly on the native bulk-inserter builder. Register a transaction-owning outbox
/// behavior before this behavior when both features are used.
/// </remarks>
/// <example>
/// <code>
/// services.AddEntityFrameworkBulkInserter&lt;Customer, AppDbContext&gt;()
///     .WithBehavior&lt;EntityBulkInserterChangeHistoryBehavior&lt;Customer, AppDbContext&gt;&gt;();
/// </code>
/// </example>
public class EntityBulkInserterChangeHistoryBehavior<TEntity, TContext> : IEntityBulkInserter<TEntity>
    where TEntity : class, IEntity
    where TContext : DbContext, IChangeHistoryContext
{
    private const string RedactedValue = "\"***REDACTED***\"";
    private static readonly string[] SensitivePropertyNameParts = ["password", "secret", "token", "credential", "apikey", "api_key", "connectionstring"];
    private readonly TContext context;
    private readonly IEntityBulkInserter<TEntity> inner;
    private readonly ChangeHistoryOptions options;
    private readonly ICurrentUserAccessor currentUserAccessor;
    private readonly ISerializer serializer;

    /// <summary>
    /// Initializes the native bulk-insert ChangeHistory behavior.
    /// </summary>
    /// <param name="context">The DbContext used to persist ChangeHistory rows.</param>
    /// <param name="inner">The decorated native bulk inserter.</param>
    /// <param name="options">The ChangeHistory capture options.</param>
    /// <param name="currentUserAccessor">The optional current-user accessor.</param>
    /// <param name="serializer">The optional value serializer.</param>
    /// <example>
    /// <code>
    /// var behavior = new EntityBulkInserterChangeHistoryBehavior&lt;Customer, AppDbContext&gt;(
    ///     context,
    ///     inner,
    ///     options);
    /// </code>
    /// </example>
    public EntityBulkInserterChangeHistoryBehavior(
        TContext context,
        IEntityBulkInserter<TEntity> inner,
        ChangeHistoryOptions options = null,
        ICurrentUserAccessor currentUserAccessor = null,
        ISerializer serializer = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(inner);

        this.context = context;
        this.inner = inner;
        this.options = options ?? new ChangeHistoryOptions();
        this.options.Validate();
        this.currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();
        this.serializer = serializer ?? new SystemTextJsonSerializer();
    }

    /// <inheritdoc />
    public async Task<Result<long>> InsertAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var items = entities?.Where(entity => entity is not null).ToArray() ?? [];
        var entityOptions = this.options.GetEntityOptions(typeof(TEntity));
        if (items.Length == 0 ||
            entityOptions is null ||
            entityOptions.BulkInsertCaptureMode == ChangeHistoryBulkInsertCaptureMode.Disabled ||
            ChangeHistoryCaptureScope.IsSuppressed)
        {
            return await this.inner.InsertAsync(items, cancellationToken).AnyContext();
        }

        if (entityOptions.BulkInsertCaptureMode == ChangeHistoryBulkInsertCaptureMode.Detailed &&
            items.Length > entityOptions.BulkInsertMaxDetailedEntities)
        {
            throw new InvalidOperationException(
                $"ChangeHistory detailed bulk-insert capture for {typeof(TEntity).Name} received {items.Length} entities, exceeding the configured limit of {entityOptions.BulkInsertMaxDetailedEntities}.");
        }

        var ownsTransaction = this.context.Database.CurrentTransaction is null && this.context.Database.IsRelational();
        IDbContextTransaction transaction = null;
        var rows = new List<ChangeHistoryEntry>();

        try
        {
            if (ownsTransaction)
            {
                transaction = await this.context.Database.BeginTransactionAsync(cancellationToken).AnyContext();
            }

            var result = await this.inner.InsertAsync(items, cancellationToken).AnyContext();
            if (result.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None).AnyContext();
                }

                return result;
            }

            if (result.Value == 0)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken).AnyContext();
                }

                return result;
            }

            var affectedEntityCount = checked((int)result.Value);
            var bulkOperationId = GuidGenerator.CreateSequential();
            rows.AddRange(entityOptions.BulkInsertCaptureMode switch
            {
                ChangeHistoryBulkInsertCaptureMode.Summary =>
                    [this.CreateSummaryEntry(bulkOperationId, affectedEntityCount, entityOptions)],
                ChangeHistoryBulkInsertCaptureMode.Detailed =>
                    this.CreateDetailedEntries(items, bulkOperationId, affectedEntityCount, entityOptions),
                _ => []
            });

            if (rows.Count > 0)
            {
                this.context.Set<ChangeHistoryEntry>().AddRange(rows);
                await this.context.SaveChangesAsync(cancellationToken).AnyContext();
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).AnyContext();
            }

            return result;
        }
        catch
        {
            foreach (var row in rows)
            {
                this.context.Entry(row).State = EntityState.Detached;
            }

            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None).AnyContext();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().AnyContext();
            }
        }
    }

    private IEnumerable<ChangeHistoryEntry> CreateDetailedEntries(
        IReadOnlyCollection<TEntity> entities,
        Guid bulkOperationId,
        int affectedEntityCount,
        ChangeHistoryEntityOptions entityOptions)
    {
        var rows = new List<ChangeHistoryEntry>();
        foreach (var entity in entities)
        {
            if (IsDefaultId(entity.Id))
            {
                throw new InvalidOperationException(
                    $"ChangeHistory detailed bulk-insert capture for {typeof(TEntity).Name} requires stable entity identifiers. Native database-generated identifiers are not hydrated back into input entities; use summary capture or assign identifiers before insertion.");
            }

            var changeSetId = GuidGenerator.CreateSequential();
            var sequence = 0;
            foreach (var property in GetComparableProperties())
            {
                var valuePolicy = this.GetValuePolicy(entityOptions, property.Name);
                if (valuePolicy == ChangeHistoryValuePolicy.Exclude)
                {
                    continue;
                }

                var newValue = property.GetValue(entity);
                if (newValue is null)
                {
                    continue;
                }

                rows.Add(this.CreateEntry(
                    changeSetId,
                    sequence++,
                    entity.GetType(),
                    entity.Id,
                    property.Name,
                    newValue,
                    property.PropertyType,
                    entityOptions.CaptureStrategy ?? this.options.DefaultCaptureStrategy,
                    valuePolicy,
                    bulkOperationId,
                    affectedEntityCount));
            }
        }

        return rows.Count > 0
            ? rows
            : [this.CreateSummaryEntry(bulkOperationId, affectedEntityCount, entityOptions)];
    }

    private ChangeHistoryEntry CreateSummaryEntry(
        Guid bulkOperationId,
        int affectedEntityCount,
        ChangeHistoryEntityOptions entityOptions)
    {
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = GuidGenerator.CreateSequential(),
            ChangeSetId = bulkOperationId,
            ChangeSetSequence = 0,
            EntityType = typeof(TEntity).Name,
            EntityClrType = typeof(TEntity).AssemblyQualifiedName,
            EntityId = "*",
            EntityIdType = typeof(string).AssemblyQualifiedName,
            PropertyName = "__ChangeHistoryBulkInsertSummary",
            PathKind = "Summary",
            ValueClrType = typeof(string).AssemblyQualifiedName,
            Operation = ChangeHistoryOperation.BulkInsert.ToString(),
            CaptureStrategy = (entityOptions.CaptureStrategy ?? this.options.DefaultCaptureStrategy).ToString(),
            CaptureSource = ChangeHistoryCaptureSource.NativeBulkInsert.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Summary.ToString(),
            CaptureMessage = $"Native bulk insert captured {affectedEntityCount} {typeof(TEntity).Name} entities.",
            BulkOperationId = bulkOperationId,
            AffectedEntityCount = affectedEntityCount,
            IsRestoreable = false,
            ChangedByUserId = this.currentUserAccessor.UserId,
            ChangedByUserName = this.currentUserAccessor.UserName,
            ChangedByEmail = this.currentUserAccessor.Email,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            FlowId = Activity.Current?.RootId,
            ModuleName = GetCurrentModuleName(),
            ActivityParentId = GetCurrentActivityParentId(),
            Properties = this.CreateActivityPropertiesJson()
        };
    }

    private ChangeHistoryEntry CreateEntry(
        Guid changeSetId,
        int sequence,
        Type entityType,
        object entityId,
        string propertyName,
        object newValue,
        Type valueType,
        ChangeHistoryCaptureStrategy strategy,
        ChangeHistoryValuePolicy valuePolicy,
        Guid bulkOperationId,
        int affectedEntityCount)
    {
        var newValueCapture = this.CaptureValue(newValue, valuePolicy);
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = GuidGenerator.CreateSequential(),
            ChangeSetId = changeSetId,
            ChangeSetSequence = sequence,
            EntityType = entityType.Name,
            EntityClrType = entityType.AssemblyQualifiedName,
            EntityId = entityId.ToString(),
            EntityIdType = entityId.GetType().AssemblyQualifiedName,
            PropertyName = propertyName,
            PathKind = "Scalar",
            ValueClrType = valueType.AssemblyQualifiedName,
            NewValue = newValueCapture.StoredValue,
            NewValueHash = newValueCapture.Hash,
            Operation = ChangeHistoryOperation.BulkInsert.ToString(),
            CaptureStrategy = strategy.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.NativeBulkInsert.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            BulkOperationId = bulkOperationId,
            AffectedEntityCount = affectedEntityCount,
            IsRestoreable = false,
            ChangedByUserId = this.currentUserAccessor.UserId,
            ChangedByUserName = this.currentUserAccessor.UserName,
            ChangedByEmail = this.currentUserAccessor.Email,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            FlowId = Activity.Current?.RootId,
            ModuleName = GetCurrentModuleName(),
            ActivityParentId = GetCurrentActivityParentId(),
            Properties = this.CreateActivityPropertiesJson()
        };
    }

    private ChangeHistoryValuePolicy GetValuePolicy(
        ChangeHistoryEntityOptions entityOptions,
        string propertyName)
    {
        if (entityOptions.PropertyPolicies.TryGetValue(propertyName, out var policy))
        {
            return policy;
        }

        if (this.options.ProtectSensitivePropertyNames && IsSensitivePropertyName(propertyName))
        {
            return this.options.SensitiveValuePolicy;
        }

        return ChangeHistoryValuePolicy.Include;
    }

    private ValueCapture CaptureValue(object value, ChangeHistoryValuePolicy policy)
    {
        var serializedValue = this.serializer.SerializeToString(value);
        var storedValue = policy switch
        {
            ChangeHistoryValuePolicy.Redact => RedactedValue,
            ChangeHistoryValuePolicy.HashOnly => null,
            _ => serializedValue
        };

        return new ValueCapture(
            this.ApplyOversizedValuePolicy(storedValue, serializedValue),
            HashValue(serializedValue));
    }

    private string ApplyOversizedValuePolicy(string storedValue, string originalSerializedValue)
    {
        if (storedValue is null ||
            this.options.MaxStoredValueLength is null ||
            storedValue.Length <= this.options.MaxStoredValueLength.Value)
        {
            return storedValue;
        }

        return this.options.OversizedValuePolicy switch
        {
            ChangeHistoryOversizedValuePolicy.Include => storedValue,
            ChangeHistoryOversizedValuePolicy.Truncate => storedValue[..this.options.MaxStoredValueLength.Value],
            ChangeHistoryOversizedValuePolicy.HashOnly => null,
            ChangeHistoryOversizedValuePolicy.Reject => throw new InvalidOperationException(
                $"ChangeHistory value length {originalSerializedValue.Length} exceeds the configured limit of {this.options.MaxStoredValueLength.Value}."),
            _ => storedValue
        };
    }

    private string CreateActivityPropertiesJson()
    {
        var activityId = GetCurrentActivityParentId();
        return string.IsNullOrWhiteSpace(activityId)
            ? null
            : this.serializer.SerializeToString(new Dictionary<string, string>
            {
                [ModuleConstants.ActivityParentIdKey] = activityId
            });
    }

    private static IEnumerable<PropertyInfo> GetComparableProperties()
        => typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Where(property => property.Name != nameof(IEntity.Id))
            .Where(property => IsScalarType(property.PropertyType));

    private static bool IsScalarType(Type type)
        => type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(decimal);

    private static bool IsSensitivePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = propertyName.Replace(".", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        return SensitivePropertyNameParts.Any(part => normalized.Contains(part, StringComparison.Ordinal));
    }

    private static string HashValue(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }

    private static bool IsDefaultId(object id)
    {
        if (id is null)
        {
            return true;
        }

        var type = id.GetType();
        return type.IsValueType && Equals(id, Activator.CreateInstance(type));
    }

    private static string GetCurrentModuleName()
        => Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey) ??
            Activity.Current?.GetBaggageItem(ActivityConstants.ModuleNameTagKey);

    private static string GetCurrentActivityParentId()
        => Activity.Current?.ParentId ?? Activity.Current?.Id;

    private sealed record ValueCapture(string StoredValue, string Hash);
}