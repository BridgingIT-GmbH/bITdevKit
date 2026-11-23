// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;

/// <summary>Captures portable history from confirmed native mutations in the same transaction.</summary>
/// <param name="options">The tracked entities and value policies.</param>
/// <param name="currentUser">The current user accessor, when supplied by the host.</param>
/// <param name="timeProvider">The capture clock.</param>
public sealed class LiteDbChangeHistoryCapture(LiteDbChangeHistoryOptions options, ICurrentUserAccessor currentUser, TimeProvider timeProvider) : ILiteDbEntityMutationObserver
{
    private static readonly string[] SensitiveNames = ["password", "secret", "token", "credential", "apikey", "api_key", "connectionstring"];

    /// <inheritdoc />
    public void Capture(LiteDbDatabaseSession session, LiteDatabase database, LiteDbEntityMutation mutation)
    {
        var settings = options.GetEntityOptions(mutation.EntityType);
        if (settings is null) { return; }

        var strategy = settings.CaptureStrategy ?? options.DefaultCaptureStrategy;
        var source = mutation.Source == "Restore" ? "Restore" : mutation.Source == "BulkInsert" ? "NativeBulkInsert" : mutation.Source == "UpdateSet" ? "UpdateSet" : mutation.Before is null ? "Create" : "RepositorySnapshot";
        if (mutation.Source == "BulkInsert")
        {
            if (settings.BulkInsertCaptureMode == ChangeHistoryBulkInsertCaptureMode.Disabled) { return; }

            if (settings.BulkInsertCaptureMode == ChangeHistoryBulkInsertCaptureMode.Summary)
            {
                this.Summary(database, mutation, strategy, source, "Summary", "Native bulk insertion was captured as a summary.");
                return;
            }

            if (mutation.AffectedEntityCount > settings.BulkInsertMaxDetailedEntities) { throw Failure("The detailed bulk-insert history limit was exceeded."); }
        }
        else if (mutation.Source == "UpdateSet")
        {
            if (!settings.CaptureUpdateSet || settings.UpdateSetMode == ChangeHistoryCaptureMode.Disabled) { return; }

            if (mutation.AffectedEntityCount > (settings.UpdateSetMaxAffectedRows ?? options.DefaultUpdateSetMaxAffectedRows))
            {
                if (settings.UpdateSetMode == ChangeHistoryCaptureMode.Required) { throw Failure("The update-set history row limit was exceeded."); }

                this.Summary(database, mutation, strategy, source, "Skipped", "The configured update-set history limit was exceeded.");
                return;
            }
        }
        else if (mutation.Before is null && !settings.CaptureCreates) { return; }

        var pending = mutation.Source == "Restore" ? [] : EntityChangeHistoryAccessor.GetPendingChangeSets(mutation.Entity).ToArray();
        var capturedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var changeSet in pending)
        {
            foreach (var change in changeSet.PropertyChanges)
            {
                var path = change.PropertyPath ?? change.PropertyName;
                this.Append(database, mutation, settings, strategy, "EntityChange", changeSet.ChangeSetId, change.Sequence, path,
                    change.OldValue, change.NewValue, Type.GetType(change.ValueClrType ?? "") ?? change.NewValue?.GetType() ?? change.OldValue?.GetType() ?? typeof(object), null, null);
                capturedPaths.Add(path);
            }
        }

        if (pending.Length != 0)
        {
            // Pending buffers expose only whole-buffer acknowledgement. Consume only when no later change sets were added.
            session.OnCommitted(() =>
            {
                if (EntityChangeHistoryAccessor.GetPendingChangeSets(mutation.Entity).All(s => pending.Any(p => p.ChangeSetId == s.ChangeSetId)))
                {
                    EntityChangeHistoryAccessor.ConsumePendingChangeSets(mutation.Entity);
                }
            });
        }

        if (strategy == ChangeHistoryCaptureStrategy.EntityChangeOnly && mutation.Source is not ("Restore" or "BulkInsert" or "UpdateSet")) { return; }

        if (mutation.Before is not null && (!settings.CaptureDirectMutations || settings.DirectMutationMode == ChangeHistoryCaptureMode.Disabled) && mutation.Source is not ("Restore" or "BulkInsert" or "UpdateSet")) { return; }

        var before = mutation.Before is null ? null : database.Mapper.ToObject(mutation.EntityType, mutation.Before);
        var after = mutation.After is null ? null : database.Mapper.ToObject(mutation.EntityType, mutation.After);
        var sequence = 0;
        var setId = mutation.Source == "Restore" ? mutation.OperationId : Guid.NewGuid();
        foreach (var property in mutation.EntityType.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsScalar(p.PropertyType)))
        {
            if (capturedPaths.Contains(property.Name)) { continue; }

            this.Append(database, mutation, settings, strategy, source, setId, sequence++, property.Name,
                before is null ? null : property.GetValue(before), after is null ? null : property.GetValue(after), property.PropertyType, null, null);
        }

        foreach (var path in settings.CapturePaths)
        {
            try
            {
                var oldValues = Flatten(before, path, options.MaxMaterializedRows);
                var newValues = Flatten(after, path, options.MaxMaterializedRows);
                foreach (var key in oldValues.Keys.Union(newValues.Keys).Order(StringComparer.Ordinal))
                {
                    if (capturedPaths.Contains(key)) { continue; }

                    oldValues.TryGetValue(key, out var oldValue);
                    newValues.TryGetValue(key, out var newValue);
                    this.Append(database, mutation, settings, strategy, source, setId, sequence++, key,
                        oldValue?.Value, newValue?.Value, newValue?.Type ?? oldValue!.Type, path,
                        oldValue is null ? "Added" : newValue is null ? "Removed" : "Modified");
                    if (sequence > options.MaxMaterializedRows) { throw Failure("A history capture exceeds the configured materialization limit."); }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var required = mutation.Source == "UpdateSet" ? settings.UpdateSetMode == ChangeHistoryCaptureMode.Required : settings.DirectMutationMode == ChangeHistoryCaptureMode.Required;
                if (required || mutation.Source == "Restore") { throw; }

                this.Summary(database, mutation, strategy, source, "Failed", "A configured history path could not be captured.");
            }
        }

        if (mutation.Source == "Restore" && !database.GetCollection<BsonDocument>(options.CollectionName).Query().Where(BsonExpression.Create("$.ChangeSetId = @0", mutation.OperationId)).Exists())
        {
            this.Summary(database, mutation, strategy, "Restore", "Summary", "Restore completed with no persisted value changes.");
        }
    }

    private void Summary(LiteDatabase database, LiteDbEntityMutation mutation, ChangeHistoryCaptureStrategy strategy, string source, string status, string message)
    {
        var collection = database.GetCollection<BsonDocument>(options.CollectionName);
        if (collection.Query().Where(BsonExpression.Create("$.ChangeSetId = @0", mutation.OperationId)).Exists()) { return; }

        var row = this.Row(mutation, strategy, source, mutation.OperationId, 0, "$summary", typeof(object), null, null, null, null,
            null, null, false, null, null, status, message);
        LiteDbChangeHistoryStorage.Insert(database, options.CollectionName, mutation.EntityType, source == "NativeBulkInsert" ? row with { EntityId = null } : row);
    }

    private void Append(LiteDatabase database, LiteDbEntityMutation mutation, ChangeHistoryEntityOptions settings, ChangeHistoryCaptureStrategy strategy,
        string source, Guid setId, int sequence, string path, object before, object after, Type valueType, ChangeHistoryCapturePathOptions capturePath, string collectionAction)
    {
        var root = path.Split('.', '[')[0];
        // Storage identity and concurrency tokens are never editable history values.
        if (root is "Id" or "ConcurrencyVersion" or "DomainEvents") { return; }

        var policy = settings.PropertyPolicies.TryGetValue(path, out var explicitPolicy) || settings.PropertyPolicies.TryGetValue(root, out explicitPolicy)
            ? explicitPolicy : options.ProtectSensitivePropertyNames && SensitiveNames.Any(n => path.Contains(n, StringComparison.OrdinalIgnoreCase)) ? options.SensitiveValuePolicy : ChangeHistoryValuePolicy.Include;
        if (policy == ChangeHistoryValuePolicy.Exclude) { return; }

        var oldJson = before is null ? "null" : JsonSerializer.Serialize(before, valueType);
        var newJson = after is null ? "null" : JsonSerializer.Serialize(after, valueType);
        if (oldJson == newJson) { return; }

        var oldValue = this.Protect(oldJson, policy);
        var newValue = this.Protect(newJson, policy);
        settings.RestorePolicies.TryGetValue(path, out var restore);
        var restoreable = mutation.Before is not null && mutation.After is not null && oldValue.Restoreable && newValue.Restoreable && (capturePath is null ? restore is not null : capturePath.RestorePlanType is not null);
        var row = this.Row(mutation, strategy, source, setId, sequence, path, valueType,
            oldValue.Stored, newValue.Stored, oldValue.Hash, newValue.Hash, capturePath, collectionAction, restoreable,
            restore?.ExecutionMode.ToString(), restore?.HandlerName ?? restore?.HandlerType?.Name, "Captured", null);
        if (source == "EntityChange")
        {
            var identityBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{mutation.EntityType.AssemblyQualifiedNameShort()}:{mutation.Entity.Id}:{setId}:{sequence}"));
            row = row with { Id = new Guid(identityBytes.AsSpan(0, 16)) };
            if (database.GetCollection<BsonDocument>(options.CollectionName).FindById(row.Id) is not null) { return; }
        }

        LiteDbChangeHistoryStorage.Insert(database, options.CollectionName, mutation.EntityType, row);
    }

    private ChangeHistoryRecord Row(LiteDbEntityMutation mutation, ChangeHistoryCaptureStrategy strategy, string source, Guid setId, int sequence,
        string path, Type valueType, string before, string after, string beforeHash, string afterHash, ChangeHistoryCapturePathOptions capturePath,
        string collectionAction, bool restoreable, string executionMode, string handler, string status, string message)
    {
        var now = timeProvider.GetUtcNow();
        var properties = mutation.RestoredChangeSetId.HasValue ? JsonSerializer.Serialize(new { RestoredFromChangeSetId = mutation.RestoredChangeSetId.Value }) : null;
        return new ChangeHistoryRecord(
            Id: Guid.NewGuid(), ChangeSetId: setId, ChangeSetSequence: sequence, EntityType: mutation.EntityType.Name,
            EntityClrType: mutation.EntityType.AssemblyQualifiedNameShort(), EntityId: mutation.Entity.Id?.ToString(), EntityIdType: mutation.Entity.Id?.GetType().AssemblyQualifiedNameShort(),
            PropertyName: path, PropertyPath: path, PathKind: capturePath?.Kind.ToString() ?? "Scalar", CollectionAction: collectionAction,
            CollectionItemId: path.Contains('[') ? path[(path.IndexOf('[') + 1)..path.IndexOf(']')] : null, ValueClrType: valueType.AssemblyQualifiedNameShort(),
            OldValue: before, NewValue: after, OldValueHash: beforeHash, NewValueHash: afterHash,
            Operation: source == "Restore" ? "Restore" : source == "UpdateSet" ? "BulkUpdate" : source == "NativeBulkInsert" ? "BulkInsert" : mutation.Operation == "Insert" ? "Create" : mutation.Operation,
            CaptureStrategy: strategy.ToString(), CaptureSource: source, CaptureStatus: status, CaptureMessage: message,
            BulkOperationId: mutation.Source is "BulkInsert" or "UpdateSet" or "DeleteSet" or "InsertSet" ? mutation.OperationId : null,
            AffectedEntityCount: mutation.AffectedEntityCount, IsRestoreable: restoreable, RestorePlanName: capturePath?.RestorePlanName,
            RestoreExecutionMode: executionMode, DomainRestoreHandlerName: handler,
            ChangedByUserId: currentUser?.UserId, ChangedByUserName: currentUser?.UserName, ChangedByEmail: currentUser?.Email,
            ChangedDate: now, ChangedDateTicks: now.UtcTicks, Reason: mutation.Reason,
            CorrelationId: Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey) ?? Activity.Current?.GetBaggageItem("CorrelationId"), FlowId: Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey) ?? Activity.Current?.GetBaggageItem("FlowId"),
            ModuleName: Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey), ActivityParentId: Activity.Current?.Id, Properties: properties);
    }

    private (string Stored, string Hash, bool Restoreable) Protect(string json, ChangeHistoryValuePolicy policy)
    {
        var hash = HashHelper.Compute(json);
        if (policy == ChangeHistoryValuePolicy.Redact) { return ("[REDACTED]", hash, false); }

        if (policy == ChangeHistoryValuePolicy.HashOnly) { return (null, hash, false); }

        if (options.MaxStoredValueLength is { } max && json.Length > max)
        {
            return options.OversizedValuePolicy switch
            {
                ChangeHistoryOversizedValuePolicy.Reject => throw Failure("A history value exceeds MaxStoredValueLength."),
                ChangeHistoryOversizedValuePolicy.HashOnly => (null, hash, false),
                ChangeHistoryOversizedValuePolicy.Truncate => (json[..max], hash, false),
                _ => (json, hash, true)
            };
        }

        return (json, hash, true);
    }

    private static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan) || type == typeof(byte[]);
    }

    private static Dictionary<string, CapturedValue> Flatten(object entity, ChangeHistoryCapturePathOptions path, int maximum)
    {
        var values = new Dictionary<string, CapturedValue>(StringComparer.Ordinal);
        var root = entity;
        foreach (var part in path.Path.Split('.'))
        {
            if (root is null) { break; }

            var property = root.GetType().GetProperty(part) ?? throw Failure("A configured history path does not exist.");
            root = property.GetValue(root);
        }

        Walk(root, root?.GetType(), path.Path, 0);
        return values;

        void Walk(object value, Type declaredType, string currentPath, int depth)
        {
            if (values.Count > maximum) { throw Failure("A history graph exceeds the configured row limit."); }

            if (depth > 16) { throw Failure("A configured history graph exceeds the depth limit."); }

            if (declaredType is not null && IsScalar(declaredType)) { values[currentPath] = new(value, declaredType); return; }

            if (value is IEnumerable sequence && value is not string && value is not byte[])
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var identity = currentPath == path.Path ? path.CollectionItemIdentity : path.GraphIdentities.GetValueOrDefault(currentPath)?.Identity;
                if (identity is null && (path.Kind == ChangeHistoryCapturePathKind.Collection || !path.RequireExplicitGraphIdentities))
                {
                    identity = item => item.GetType().GetProperty("Id")?.GetValue(item)?.ToString();
                }

                if (identity is null) { throw Failure("Collection history requires an explicit stable item identity."); }

                foreach (var item in sequence)
                {
                    var id = item is null ? null : identity(item);
                    if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) { throw Failure("Collection history requires unique nonempty item identities."); }

                    var itemPath = currentPath + "[" + Uri.EscapeDataString(id) + "]";
                    values[itemPath + ".__presence"] = new(true, typeof(bool?));
                    foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
                    {
                        Walk(property.GetValue(item), property.PropertyType, itemPath + "." + property.Name, depth + 1);
                    }
                }

                return;
            }

            if (value is null) { return; }

            foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            {
                Walk(property.GetValue(value), property.PropertyType, currentPath + "." + property.Name, depth + 1);
            }
        }
    }

    private static LiteDbProviderException Failure(string message) => new(new InvalidOperationError(message));

    private sealed record CapturedValue(object Value, Type Type);
}
