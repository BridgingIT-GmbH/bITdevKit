// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Restores scalar ChangeHistory rows using configured domain restore policies.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <example>
/// <code>
/// var handler = new ChangeHistoryRestoreCommandHandler&lt;Customer, CustomerDbContext&gt;(context, repository, options, services);
/// await handler.HandleAsync(new ChangeHistoryRestoreCommand&lt;Customer&gt;(id, changeSetId));
/// </code>
/// </example>
public class ChangeHistoryRestoreCommandHandler<TEntity, TContext>
    where TEntity : class, IEntity
    where TContext : DbContext
{
    private const string RestoredFromChangeSetIdProperty = "RestoredFromChangeSetId";
    private readonly TContext context;
    private readonly IGenericRepository<TEntity> repository;
    private readonly ChangeHistoryOptions options;
    private readonly IServiceProvider serviceProvider;
    private readonly ISerializer serializer;
    private readonly ICurrentUserAccessor currentUserAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeHistoryRestoreCommandHandler{TEntity,TContext}" /> class.
    /// </summary>
    /// <param name="context">The EF Core context containing ChangeHistory rows.</param>
    /// <param name="repository">The entity repository.</param>
    /// <param name="options">The ChangeHistory options.</param>
    /// <param name="serviceProvider">The service provider used to resolve typed restore handlers.</param>
    /// <param name="serializer">The value serializer.</param>
    /// <param name="currentUserAccessor">The current user accessor.</param>
    public ChangeHistoryRestoreCommandHandler(
        TContext context,
        IGenericRepository<TEntity> repository,
        ChangeHistoryOptions options,
        IServiceProvider serviceProvider = null,
        ISerializer serializer = null,
        ICurrentUserAccessor currentUserAccessor = null)
    {
        EnsureArg.IsNotNull(context, nameof(context));
        EnsureArg.IsNotNull(repository, nameof(repository));

        this.context = context;
        this.repository = repository;
        this.options = options ?? new ChangeHistoryOptions();
        this.serviceProvider = serviceProvider;
        this.serializer = serializer ?? new SystemTextJsonSerializer();
        this.currentUserAccessor = currentUserAccessor ?? new NullCurrentUserAccessor();
    }

    /// <summary>
    /// Restores the selected change set.
    /// </summary>
    /// <param name="command">The restore command.</param>
    /// <param name="cancellationToken">A token to observe while restoring.</param>
    /// <returns>A restore result with the new restore change set id.</returns>
    public async Task<Result<ChangeHistoryRestoreResult>> HandleAsync(
        ChangeHistoryRestoreCommand<TEntity> command,
        CancellationToken cancellationToken = default)
    {
        if (command is null || command.EntityId is null || command.ChangeSetId == Guid.Empty)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError("A valid entity id and change set id are required."));
        }

        var entityOptions = this.options.GetEntityOptions(typeof(TEntity));
        if (entityOptions is null)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError($"ChangeHistory is not configured for {typeof(TEntity).Name}."));
        }

        var entity = await this.repository.FindOneAsync(command.EntityId, new FindOptions<TEntity> { NoTracking = false }, cancellationToken).AnyContext();
        if (entity is null)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new NotFoundError($"{typeof(TEntity).Name} ({command.EntityId}) was not found."));
        }

        var concurrencyResult = this.ValidateConcurrency(entity, entityOptions, command);
        if (concurrencyResult.IsFailure)
        {
            return Result<ChangeHistoryRestoreResult>.Failure()
                .WithErrors(concurrencyResult.Errors)
                .WithMessages(concurrencyResult.Messages);
        }

        var selectedRows = await this.context.Set<ChangeHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.ChangeSetId == command.ChangeSetId)
            .Where(e => e.EntityType == typeof(TEntity).Name)
            .Where(e => e.EntityId == command.EntityId.ToString())
            .OrderBy(e => e.ChangeSetSequence)
            .ToListAsync(cancellationToken).AnyContext();
        if (selectedRows.Count == 0)
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new NotFoundError($"Change set {command.ChangeSetId} was not found for {typeof(TEntity).Name} ({command.EntityId})."));
        }

        var rows = command.RestoreMode == ChangeHistoryRestoreMode.PointInTime
            ? await this.LoadPointInTimeRowsAsync(command, selectedRows, cancellationToken).AnyContext()
            : selectedRows;
        if (rows.Any(e => !e.IsRestoreable))
        {
            return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError($"Restore selection for change set {command.ChangeSetId} contains non-restoreable rows."));
        }

        var authorizationResult = await this.AuthorizeRestoreAsync(entity, entityOptions, command, cancellationToken).AnyContext();
        if (authorizationResult.IsFailure)
        {
            return Result<ChangeHistoryRestoreResult>.Failure()
                .WithErrors(authorizationResult.Errors)
                .WithMessages(authorizationResult.Messages);
        }

        var restoreRows = new List<ChangeHistoryEntry>();
        var appliedValues = new List<(PropertyInfo Property, object Value)>();
        var restoreSnapshot = this.CreateRestoreSnapshot(entity);
        var restoreChangeSetId = GuidGenerator.CreateSequential();
        var sequence = 0;
        foreach (var graphGroup in rows.Where(r => r.PathKind == ChangeHistoryCapturePathKind.Graph.ToString()).GroupBy(r => r.RestorePlanName))
        {
            var graphRows = graphGroup.ToArray();
            var graphResult = string.IsNullOrWhiteSpace(graphGroup.Key)
                ? this.ApplyBuiltInGraphRestore(entity, entityOptions, graphRows)
                : await this.ApplyGraphRestorePlanAsync(entity, entityOptions, graphGroup.Key, graphRows, cancellationToken).AnyContext();
            if (graphResult.IsFailure)
            {
                this.RollbackRestoreSnapshot(entity, restoreSnapshot);

                return Result<ChangeHistoryRestoreResult>.Failure()
                    .WithErrors(graphResult.Errors)
                    .WithMessages(graphResult.Messages);
            }

            foreach (var row in graphGroup)
            {
                restoreRows.Add(this.CreateGraphRestoreEntry(restoreChangeSetId, sequence++, entity, row, command.Reason));
            }
        }

        var pathRows = rows
            .Where(r => r.PathKind == ChangeHistoryCapturePathKind.Owned.ToString() || r.PathKind == ChangeHistoryCapturePathKind.Collection.ToString())
            .ToArray();
        foreach (var pathGroup in pathRows.GroupBy(r => new { r.PathKind, r.RestorePlanName }))
        {
            var groupedRows = pathGroup.ToArray();
            var pathResult = pathGroup.Key.PathKind == ChangeHistoryCapturePathKind.Collection.ToString() && string.IsNullOrWhiteSpace(pathGroup.Key.RestorePlanName)
                ? this.ApplyBuiltInCollectionRestore(entity, entityOptions, groupedRows)
                : await this.ApplyPathRestorePlanAsync(entity, entityOptions, pathGroup.Key.PathKind, pathGroup.Key.RestorePlanName, groupedRows, cancellationToken).AnyContext();
            if (pathResult.IsFailure)
            {
                this.RollbackRestoreSnapshot(entity, restoreSnapshot);

                return Result<ChangeHistoryRestoreResult>.Failure()
                    .WithErrors(pathResult.Errors)
                    .WithMessages(pathResult.Messages);
            }

            foreach (var row in pathGroup)
            {
                restoreRows.Add(this.CreateGraphRestoreEntry(restoreChangeSetId, sequence++, entity, row, command.Reason));
            }
        }

        foreach (var row in rows.Where(r => r.PathKind == "Scalar" || string.IsNullOrWhiteSpace(r.PathKind)))
        {
            if (!entityOptions.RestorePolicies.TryGetValue(row.PropertyName, out var restorePolicy))
            {
                this.RollbackRestoreSnapshot(entity, restoreSnapshot);

                return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError($"Restore is not configured for {typeof(TEntity).Name}.{row.PropertyName}."));
            }

            var property = typeof(TEntity).GetProperty(row.PropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                this.RollbackRestoreSnapshot(entity, restoreSnapshot);

                return Result<ChangeHistoryRestoreResult>.Failure(new ValidationError($"Restore property {typeof(TEntity).Name}.{row.PropertyName} was not found."));
            }

            var currentValue = property.GetValue(entity);
            var restoredValue = this.DeserializeHistoryValue(row.OldValue, property.PropertyType);
            var restoreResult = await this.ApplyRestoreAsync(entity, restorePolicy, property, restoredValue, row.ChangeSetId, command.Reason, cancellationToken).AnyContext();
            if (restoreResult.IsFailure)
            {
                this.RollbackAppliedValues(entity, appliedValues);
                this.RollbackRestoreSnapshot(entity, restoreSnapshot);

                return Result<ChangeHistoryRestoreResult>.Failure()
                    .WithErrors(restoreResult.Errors)
                    .WithMessages(restoreResult.Messages);
            }

            appliedValues.Add((property, currentValue));

            restoreRows.Add(this.CreateRestoreEntry(
                restoreChangeSetId,
                sequence++,
                entity,
                row,
                currentValue,
                restoredValue,
                property.PropertyType,
                restorePolicy,
                command.Reason));
        }

        this.context.Set<ChangeHistoryEntry>().AddRange(restoreRows);
        using (ChangeHistoryCaptureScope.Suppress())
        {
            await this.repository.UpdateAsync(entity, cancellationToken).AnyContext();
        }

        await this.context.SaveChangesAsync(cancellationToken).AnyContext();

        return Result<ChangeHistoryRestoreResult>.Success(new ChangeHistoryRestoreResult(restoreChangeSetId, restoreRows.Count));
    }

    private async Task<List<ChangeHistoryEntry>> LoadPointInTimeRowsAsync(
        ChangeHistoryRestoreCommand<TEntity> command,
        IReadOnlyList<ChangeHistoryEntry> selectedRows,
        CancellationToken cancellationToken)
    {
        var changedDate = selectedRows.Min(e => e.ChangedDate);
        var rows = await this.context.Set<ChangeHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.EntityType == typeof(TEntity).Name)
            .Where(e => e.EntityId == command.EntityId.ToString())
            .Where(e => e.ChangeSetId == command.ChangeSetId || e.ChangedDate >= changedDate)
            .OrderBy(e => e.ChangedDate)
            .ThenBy(e => e.ChangeSetSequence)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken).AnyContext();

        var scalarRows = rows
            .Where(IsScalarRow)
            .GroupBy(GetRestorePathKey)
            .Select(g => g.First())
            .ToArray();
        var nonScalarRows = rows
            .Where(row => !IsScalarRow(row))
            .ToArray();

        return nonScalarRows
            .Concat(scalarRows)
            .OrderBy(e => e.ChangedDate)
            .ThenBy(e => e.ChangeSetSequence)
            .ThenBy(e => e.Id)
            .ToList();
    }

    private static bool IsScalarRow(ChangeHistoryEntry row)
        => row.PathKind == "Scalar" || string.IsNullOrWhiteSpace(row.PathKind);

    private static string GetRestorePathKey(ChangeHistoryEntry row)
        => string.IsNullOrWhiteSpace(row.PropertyPath) ? row.PropertyName : row.PropertyPath;

    private Result ValidateConcurrency(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryRestoreCommand<TEntity> command)
    {
        if (entity is not IConcurrency concurrency)
        {
            return Result.Success();
        }

        if (entityOptions.RestoreConcurrencyPolicy == ChangeHistoryRestoreConcurrencyPolicy.None)
        {
            return Result.Success();
        }

        if (entityOptions.RestoreConcurrencyPolicy == ChangeHistoryRestoreConcurrencyPolicy.RequireExpectedVersion && !command.ExpectedConcurrencyVersion.HasValue)
        {
            return Result.Failure(new ConcurrencyError($"{typeof(TEntity).Name} ({command.EntityId}) requires an expected concurrency version for restore."));
        }

        if (command.ExpectedConcurrencyVersion.HasValue && concurrency.ConcurrencyVersion != command.ExpectedConcurrencyVersion.Value)
        {
            return Result.Failure(new ConcurrencyError($"{typeof(TEntity).Name} ({command.EntityId}) has changed since the expected version."));
        }

        return Result.Success();
    }

    private async Task<Result> AuthorizeRestoreAsync(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryRestoreCommand<TEntity> command,
        CancellationToken cancellationToken)
    {
        if (entityOptions.RestoreAuthorizerType is null)
        {
            return Result.Success();
        }

        var authorizer = this.serviceProvider?.GetService(entityOptions.RestoreAuthorizerType) as IChangeHistoryRestoreAuthorizer<TEntity>;
        if (authorizer is null)
        {
            return Result.Failure(new ValidationError($"Restore authorizer {entityOptions.RestoreAuthorizerType.Name} is not registered."));
        }

        return await authorizer.AuthorizeAsync(
            entity,
            new ChangeHistoryRestoreAuthorizationContext(command.ChangeSetId, command.Reason),
            cancellationToken).AnyContext();
    }

    private async Task<Result> ApplyGraphRestorePlanAsync(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        string restorePlanName,
        IReadOnlyList<ChangeHistoryEntry> rows,
        CancellationToken cancellationToken)
    {
        var graphOptions = entityOptions.CapturePaths
            .Where(p => p.Kind == ChangeHistoryCapturePathKind.Graph)
            .FirstOrDefault(p => p.RestorePlanName == restorePlanName);
        if (graphOptions?.RestorePlanType is null)
        {
            return Result.Failure(new ValidationError($"Graph restore plan '{restorePlanName}' is not configured for {typeof(TEntity).Name}."));
        }

        var plan = this.serviceProvider?.GetService(graphOptions.RestorePlanType) as IChangeHistoryGraphRestorePlan<TEntity>;
        if (plan is null)
        {
            return Result.Failure(new ValidationError($"Graph restore plan {graphOptions.RestorePlanType.Name} is not registered."));
        }

        var values = rows.Select(row =>
        {
            var valueType = Type.GetType(row.ValueClrType) ?? typeof(object);
            var value = this.DeserializeHistoryValue(row.OldValue, valueType);

            return new ChangeHistoryGraphRestoreValue(row.PropertyPath ?? row.PropertyName, value, valueType);
        }).ToArray();

        return await plan.RestoreAsync(entity, values, cancellationToken).AnyContext();
    }

    private async Task<Result> ApplyPathRestorePlanAsync(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        string pathKind,
        string restorePlanName,
        IReadOnlyList<ChangeHistoryEntry> rows,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(restorePlanName))
        {
            return Result.Failure(new ValidationError($"{pathKind} restore plan is not configured for {typeof(TEntity).Name}."));
        }

        var pathOptions = entityOptions.CapturePaths
            .Where(p => p.Kind.ToString() == pathKind)
            .FirstOrDefault(p => p.RestorePlanName == restorePlanName);
        if (pathOptions?.RestorePlanType is null)
        {
            return Result.Failure(new ValidationError($"{pathKind} restore plan '{restorePlanName}' is not configured for {typeof(TEntity).Name}."));
        }

        var plan = this.serviceProvider?.GetService(pathOptions.RestorePlanType) as IChangeHistoryGraphRestorePlan<TEntity>;
        if (plan is null)
        {
            return Result.Failure(new ValidationError($"Restore plan {pathOptions.RestorePlanType.Name} is not registered."));
        }

        var values = rows.Select(row =>
        {
            var valueType = Type.GetType(row.ValueClrType) ?? typeof(object);
            var value = this.DeserializeHistoryValue(row.OldValue, valueType);

            return new ChangeHistoryGraphRestoreValue(row.PropertyPath ?? row.PropertyName, value, valueType);
        }).ToArray();

        return await plan.RestoreAsync(entity, values, cancellationToken).AnyContext();
    }

    private Result ApplyBuiltInCollectionRestore(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        IReadOnlyList<ChangeHistoryEntry> rows)
    {
        foreach (var row in rows.GroupBy(r => r.CollectionItemId).SelectMany(g => g))
        {
            var collectionPath = GetCollectionPath(row.PropertyPath ?? row.PropertyName);
            var pathOptions = entityOptions.CapturePaths.FirstOrDefault(p => p.Kind == ChangeHistoryCapturePathKind.Collection && p.Path == collectionPath);
            if (pathOptions?.CollectionItemIdentity is null)
            {
                return Result.Failure(new ValidationError($"Collection restore for {typeof(TEntity).Name}.{collectionPath} requires an identity rule."));
            }

            var collection = GetValueByPath(entity, collectionPath) as IList;
            if (collection is null)
            {
                return Result.Failure(new ValidationError($"Collection restore for {typeof(TEntity).Name}.{collectionPath} requires a mutable IList collection."));
            }

            var item = FindCollectionItem(collection, pathOptions.CollectionItemIdentity, row.CollectionItemId);
            if (row.CollectionAction == "Added")
            {
                if (item is not null)
                {
                    collection.Remove(item);
                }

                continue;
            }

            if (item is null)
            {
                if (row.CollectionAction != "Removed")
                {
                    return Result.Failure(new ValidationError($"Collection item {row.CollectionItemId} was not found for {typeof(TEntity).Name}.{collectionPath}."));
                }

                item = Activator.CreateInstance(pathOptions.CollectionItemType);
                SetConventionalId(item, row.CollectionItemId);
                collection.Add(item);
                this.MarkCollectionItemAdded(item);
            }

            var propertyName = GetCollectionItemPropertyName(row.PropertyPath ?? row.PropertyName);
            var property = item.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanWrite)
            {
                return Result.Failure(new ValidationError($"Collection restore property {propertyName} was not found or is not settable."));
            }

            property.SetValue(item, this.DeserializeHistoryValue(row.OldValue, property.PropertyType));
        }

        return Result.Success();
    }

    private Result ApplyBuiltInGraphRestore(
        TEntity entity,
        ChangeHistoryEntityOptions entityOptions,
        IReadOnlyList<ChangeHistoryEntry> rows)
    {
        foreach (var membershipGroup in rows
                     .Where(r => !string.IsNullOrWhiteSpace(r.CollectionAction))
                     .GroupBy(r => new { r.CollectionAction, r.CollectionItemId, CollectionPath = GetGraphCollectionPath(r.PropertyPath ?? r.PropertyName) }))
        {
            var firstRow = membershipGroup.First();
            var collectionResult = this.ResolveGraphCollectionByHistoryPath(entity, entityOptions, firstRow);
            if (collectionResult.Result.IsFailure)
            {
                return collectionResult.Result;
            }

            var item = FindCollectionItem(collectionResult.Collection, collectionResult.Identity, firstRow.CollectionItemId);
            if (firstRow.CollectionAction == "Added")
            {
                if (item is not null)
                {
                    collectionResult.Collection.Remove(item);
                }

                continue;
            }

            if (firstRow.CollectionAction == "Removed" && item is null)
            {
                item = Activator.CreateInstance(collectionResult.ItemType);
                SetConventionalId(item, firstRow.CollectionItemId);
                collectionResult.Collection.Add(item);
                this.MarkCollectionItemAdded(item);
            }

            foreach (var row in membershipGroup)
            {
                var result = SetSimplePropertyFromHistoryRow(item, row, row.OldValue, this.serializer);
                if (result.IsFailure)
                {
                    return result;
                }
            }
        }

        foreach (var row in rows.Where(r => string.IsNullOrWhiteSpace(r.CollectionAction)))
        {
            var result = this.SetGraphValueByHistoryPath(entity, entityOptions, row, row.OldValue);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    private void MarkCollectionItemAdded(object item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            this.context.Entry(item).State = EntityState.Added;
        }
        catch (InvalidOperationException)
        {
            // Non-EF or unmapped collection items are still handled by the owning aggregate.
        }
    }

    private void RollbackAppliedValues(TEntity entity, IEnumerable<(PropertyInfo Property, object Value)> appliedValues)
    {
        foreach (var (property, value) in appliedValues.Reverse())
        {
            if (property.CanWrite)
            {
                property.SetValue(entity, value);
            }
        }
    }

    private Dictionary<PropertyInfo, object> CreateRestoreSnapshot(TEntity entity)
        => typeof(TEntity).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p, p => CloneSnapshotValue(p.GetValue(entity), depth: 0));

    private void RollbackRestoreSnapshot(TEntity entity, IReadOnlyDictionary<PropertyInfo, object> snapshot)
    {
        foreach (var (property, value) in snapshot)
        {
            if (property.CanWrite)
            {
                property.SetValue(entity, value);
            }
        }
    }

    private object DeserializeHistoryValue(string serializedValue, Type valueType)
        => DeserializeHistoryValue(serializedValue, valueType, this.serializer);

    private static object DeserializeHistoryValue(string serializedValue, Type valueType, ISerializer serializer)
    {
        if (serializedValue is null)
        {
            return null;
        }

        if (typeof(IEnumeration).IsAssignableFrom(valueType))
        {
            return DeserializeEnumerationValue(serializedValue, valueType);
        }

        return serializer.Deserialize(serializedValue, valueType);
    }

    private static object DeserializeEnumerationValue(string serializedValue, Type valueType)
    {
        using var document = JsonDocument.Parse(serializedValue);
        var root = document.RootElement;

        if (TryGetEnumerationId(root, out var id) && TryResolveEnumerationById(valueType, id, out var byId))
        {
            return byId;
        }

        if (TryGetEnumerationValue(root, out var value) && TryResolveEnumerationByValue(valueType, value, out var byValue))
        {
            return byValue;
        }

        throw new InvalidOperationException($"Could not restore enumeration value for {valueType.FullName} from ChangeHistory data.");
    }

    private static bool TryResolveEnumerationById(Type valueType, int id, out object value)
    {
        var getByIdMethod = valueType.GetMethod("GetById", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy, null, [typeof(int)], null);
        if (getByIdMethod is not null && valueType.IsAssignableFrom(getByIdMethod.ReturnType))
        {
            value = getByIdMethod.Invoke(null, [id]);
            if (value is not null)
            {
                return true;
            }
        }

        foreach (var item in GetStaticEnumerationValues(valueType))
        {
            if (item.Id == id)
            {
                value = item;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryResolveEnumerationByValue(Type valueType, string comparisonValue, out object value)
    {
        foreach (var item in GetStaticEnumerationValues(valueType))
        {
            if (string.Equals(item.Value, comparisonValue, StringComparison.OrdinalIgnoreCase))
            {
                value = item;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IEnumerable<IEnumeration> GetStaticEnumerationValues(Type valueType)
        => valueType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => valueType.IsAssignableFrom(f.FieldType))
            .Select(f => f.GetValue(null))
            .OfType<IEnumeration>();

    private static bool TryGetEnumerationId(JsonElement element, out int id)
    {
        if (element.ValueKind == JsonValueKind.Object && TryGetJsonProperty(element, "Id", out var idProperty))
        {
            return TryGetEnumerationId(idProperty, out id);
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out id))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out id))
        {
            return true;
        }

        id = default;
        return false;
    }

    private static bool TryGetEnumerationValue(JsonElement element, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object && TryGetJsonProperty(element, "Value", out var valueProperty))
        {
            return TryGetEnumerationValue(valueProperty, out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static bool TryGetJsonProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static object CloneSnapshotValue(object value, int depth)
    {
        var valueType = value?.GetType();
        if (value is null || depth > 6 || IsScalarType(valueType))
        {
            return value;
        }

        if (value is IList sourceList)
        {
            var cloneList = Activator.CreateInstance(value.GetType()) as IList;
            if (cloneList is null)
            {
                return value;
            }

            foreach (var item in sourceList)
            {
                cloneList.Add(CloneSnapshotValue(item, depth + 1));
            }

            return cloneList;
        }

        if (valueType.GetConstructor(Type.EmptyTypes) is null)
        {
            return value;
        }

        var clone = Activator.CreateInstance(valueType);
        foreach (var property in valueType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(p => p.CanRead && p.CanWrite)
                     .Where(p => p.GetIndexParameters().Length == 0))
        {
            property.SetValue(clone, CloneSnapshotValue(property.GetValue(value), depth + 1));
        }

        return clone;
    }

    private async Task<Result> ApplyRestoreAsync(
        TEntity entity,
        ChangeHistoryRestorePropertyOptions restorePolicy,
        PropertyInfo property,
        object restoredValue,
        Guid originalChangeSetId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (restorePolicy.ExecutionMode == ChangeHistoryRestoreExecutionMode.ValidatedSetter)
        {
            if (!property.CanWrite)
            {
                return Result.Failure(new ValidationError($"Restore property {typeof(TEntity).Name}.{property.Name} is not settable."));
            }

            property.SetValue(entity, restoredValue);

            return Result.Success();
        }

        if (restorePolicy.DomainMethod is Func<TEntity, object, Result> objectMethod)
        {
            return objectMethod(entity, restoredValue);
        }

        if (restorePolicy.DomainMethod is not null)
        {
            var result = restorePolicy.DomainMethod.DynamicInvoke(this.BuildDomainMethodArguments(restorePolicy.DomainMethod, entity, restoredValue, cancellationToken));
            if (result is Task<Result> taskResult)
            {
                return await taskResult.AnyContext();
            }

            if (result is Result syncResult)
            {
                return syncResult;
            }
        }

        if (restorePolicy.HandlerType is not null)
        {
            var handler = this.serviceProvider?.GetService(restorePolicy.HandlerType) as IChangeHistoryRestoreHandler<TEntity>;
            if (handler is null)
            {
                return Result.Failure(new ValidationError($"Restore handler {restorePolicy.HandlerType.Name} is not registered."));
            }

            return await handler.RestoreAsync(
                entity,
                new ChangeHistoryRestoreContext(property.Name, restoredValue, property.PropertyType, originalChangeSetId, reason),
                cancellationToken).AnyContext();
        }

        return Result.Failure(new ValidationError($"Restore policy for {typeof(TEntity).Name}.{property.Name} does not define domain logic."));
    }

    private object[] BuildDomainMethodArguments(Delegate domainMethod, TEntity entity, object restoredValue, CancellationToken cancellationToken)
    {
        var parameters = domainMethod.Method.GetParameters();

        return parameters.Length switch
        {
            2 => [entity, restoredValue],
            3 => [entity, restoredValue, cancellationToken],
            _ => []
        };
    }

    private ChangeHistoryEntry CreateRestoreEntry(
        Guid restoreChangeSetId,
        int sequence,
        TEntity entity,
        ChangeHistoryEntry originalRow,
        object oldValue,
        object newValue,
        Type valueType,
        ChangeHistoryRestorePropertyOptions restorePolicy,
        string reason)
    {
        var oldValueJson = this.SerializeValue(oldValue);
        var newValueJson = this.SerializeValue(newValue);
        var entityId = entity.Id;
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = GuidGenerator.CreateSequential(),
            ChangeSetId = restoreChangeSetId,
            ChangeSetSequence = sequence,
            EntityType = typeof(TEntity).Name,
            EntityClrType = typeof(TEntity).AssemblyQualifiedName,
            EntityId = entityId?.ToString(),
            EntityIdType = entityId?.GetType().AssemblyQualifiedName,
            PropertyName = originalRow.PropertyName,
            PropertyPath = originalRow.PropertyPath,
            PathKind = originalRow.PathKind,
            ValueClrType = valueType.AssemblyQualifiedName,
            OldValue = oldValueJson,
            NewValue = newValueJson,
            OldValueHash = HashValue(oldValueJson),
            NewValueHash = HashValue(newValueJson),
            Operation = ChangeHistoryOperation.Restore.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.EntityChangeOnly.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.Restore.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            RestoreExecutionMode = restorePolicy.ExecutionMode.ToString(),
            DomainRestoreHandlerName = restorePolicy.HandlerName,
            ChangedByUserId = this.currentUserAccessor.UserId,
            ChangedByUserName = this.currentUserAccessor.UserName,
            ChangedByEmail = this.currentUserAccessor.Email,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks,
            Reason = reason,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            FlowId = Activity.Current?.RootId,
            ModuleName = GetCurrentModuleName(),
            ActivityParentId = GetCurrentActivityParentId(),
            Properties = this.CreateRestorePropertiesJson(originalRow.ChangeSetId)
        };
    }

    private ChangeHistoryEntry CreateGraphRestoreEntry(
        Guid restoreChangeSetId,
        int sequence,
        TEntity entity,
        ChangeHistoryEntry originalRow,
        string reason)
    {
        var entityId = entity.Id;
        var changedDate = DateTimeOffset.UtcNow;

        return new ChangeHistoryEntry
        {
            Id = GuidGenerator.CreateSequential(),
            ChangeSetId = restoreChangeSetId,
            ChangeSetSequence = sequence,
            EntityType = typeof(TEntity).Name,
            EntityClrType = typeof(TEntity).AssemblyQualifiedName,
            EntityId = entityId?.ToString(),
            EntityIdType = entityId?.GetType().AssemblyQualifiedName,
            PropertyName = originalRow.PropertyName,
            PropertyPath = originalRow.PropertyPath,
            PathKind = originalRow.PathKind,
            CollectionAction = originalRow.CollectionAction,
            CollectionItemId = originalRow.CollectionItemId,
            ValueClrType = originalRow.ValueClrType,
            OldValue = originalRow.NewValue,
            NewValue = originalRow.OldValue,
            OldValueHash = HashValue(originalRow.NewValue),
            NewValueHash = HashValue(originalRow.OldValue),
            Operation = ChangeHistoryOperation.Restore.ToString(),
            CaptureStrategy = ChangeHistoryCaptureStrategy.EntityChangeOnly.ToString(),
            CaptureSource = ChangeHistoryCaptureSource.Restore.ToString(),
            CaptureStatus = ChangeHistoryCaptureStatus.Captured.ToString(),
            IsRestoreable = true,
            RestorePlanName = originalRow.RestorePlanName,
            RestoreExecutionMode = ChangeHistoryRestoreExecutionMode.RestorePlan.ToString(),
            DomainRestoreHandlerName = originalRow.RestorePlanName,
            ChangedByUserId = this.currentUserAccessor.UserId,
            ChangedByUserName = this.currentUserAccessor.UserName,
            ChangedByEmail = this.currentUserAccessor.Email,
            ChangedDate = changedDate,
            ChangedDateTicks = changedDate.UtcTicks,
            Reason = reason,
            CorrelationId = Activity.Current?.TraceId.ToString(),
            FlowId = Activity.Current?.RootId,
            ModuleName = GetCurrentModuleName(),
            ActivityParentId = GetCurrentActivityParentId(),
            Properties = this.CreateRestorePropertiesJson(originalRow.ChangeSetId)
        };
    }

    private string CreateRestorePropertiesJson(Guid originalChangeSetId)
    {
        var properties = new Dictionary<string, string>
        {
            [RestoredFromChangeSetIdProperty] = originalChangeSetId.ToString()
        };
        var activityId = GetCurrentActivityParentId();
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            properties[ModuleConstants.ActivityParentIdKey] = activityId;
        }

        return this.serializer.SerializeToString(properties);
    }

    private Func<object, string> ResolveGraphIdentity(
        ChangeHistoryEntityOptions entityOptions,
        string identityPath,
        object collection)
    {
        var explicitIdentity = entityOptions.CapturePaths
            .Where(p => p.Kind == ChangeHistoryCapturePathKind.Graph)
            .Select(p => p.GraphIdentities.TryGetValue(identityPath, out var identityOptions) ? identityOptions.Identity : null)
            .FirstOrDefault(identity => identity is not null);
        if (explicitIdentity is not null)
        {
            return explicitIdentity;
        }

        var item = FirstCollectionItem(collection);
        return item is null ? null : this.CreateEfPrimaryKeyIdentityAccessor(item.GetType());
    }

    private Func<object, string> CreateEfPrimaryKeyIdentityAccessor(Type itemType)
    {
        var keyProperty = this.context.Model.FindEntityType(itemType)?.FindPrimaryKey()?.Properties.SingleOrDefault();
        if (keyProperty is null)
        {
            return null;
        }

        var property = itemType.GetProperty(keyProperty.Name, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanRead)
        {
            return null;
        }

        return item => property.GetValue(item)?.ToString();
    }

    private Result SetGraphValueByHistoryPath(
        object entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryEntry row,
        string serializedValue)
    {
        var target = entity;
        var parts = SplitHistoryPath(row.PropertyPath ?? row.PropertyName);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var segment = ParsePathSegment(parts[i]);
            var property = target.GetType().GetProperty(segment.PropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} could not resolve {segment.PropertyName}."));
            }

            target = property.GetValue(target);
            if (!string.IsNullOrWhiteSpace(segment.ItemId))
            {
                var identity = this.ResolveGraphIdentity(entityOptions, string.Join('.', parts.Take(i + 1).Select(p => ParsePathSegment(p).PropertyName)), target);
                if (identity is null)
                {
                    return Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} requires an unambiguous identity rule."));
                }

                target = FindCollectionItem(target as IEnumerable, identity, segment.ItemId);
            }

            if (target is null)
            {
                return Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} could not resolve target object."));
            }
        }

        var finalPropertyName = ParsePathSegment(parts[^1]).PropertyName;
        var finalProperty = target.GetType().GetProperty(finalPropertyName, BindingFlags.Instance | BindingFlags.Public);
        if (finalProperty is null || !finalProperty.CanWrite)
        {
            return Result.Failure(new ValidationError($"Graph restore property {finalPropertyName} was not found or is not settable."));
        }

        finalProperty.SetValue(target, this.DeserializeHistoryValue(serializedValue, finalProperty.PropertyType));

        return Result.Success();
    }

    private GraphCollectionResolution ResolveGraphCollectionByHistoryPath(
        object entity,
        ChangeHistoryEntityOptions entityOptions,
        ChangeHistoryEntry row)
    {
        var target = entity;
        var parts = SplitHistoryPath(row.PropertyPath ?? row.PropertyName);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var segment = ParsePathSegment(parts[i]);
            var property = target.GetType().GetProperty(segment.PropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null)
            {
                return new GraphCollectionResolution(Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} could not resolve {segment.PropertyName}.")), null, null, null);
            }

            var value = property.GetValue(target);
            if (string.Equals(segment.ItemId, row.CollectionItemId, StringComparison.Ordinal))
            {
                if (value is not IList collection)
                {
                    return new GraphCollectionResolution(Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} requires a mutable IList collection.")), null, null, null);
                }

                var identityPath = string.Join('.', parts.Take(i + 1).Select(p => ParsePathSegment(p).PropertyName));
                var identity = this.ResolveGraphIdentity(entityOptions, identityPath, collection);
                var itemType = GetCollectionItemType(collection.GetType()) ?? FirstCollectionItem(collection)?.GetType();
                if (identity is null || itemType is null)
                {
                    return new GraphCollectionResolution(Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} requires unambiguous identity and item type metadata.")), null, null, null);
                }

                return new GraphCollectionResolution(Result.Success(), collection, identity, itemType);
            }

            target = value;
            if (!string.IsNullOrWhiteSpace(segment.ItemId))
            {
                var identityPath = string.Join('.', parts.Take(i + 1).Select(p => ParsePathSegment(p).PropertyName));
                var identity = this.ResolveGraphIdentity(entityOptions, identityPath, target);
                target = FindCollectionItem(target as IEnumerable, identity, segment.ItemId);
            }

            if (target is null)
            {
                return new GraphCollectionResolution(Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} could not resolve target collection.")), null, null, null);
            }
        }

        return new GraphCollectionResolution(Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} does not contain collection membership metadata.")), null, null, null);
    }

    private static Result SetSimplePropertyFromHistoryRow(
        object target,
        ChangeHistoryEntry row,
        string serializedValue,
        ISerializer serializer)
    {
        if (target is null)
        {
            return Result.Failure(new ValidationError($"Graph restore path {row.PropertyPath} could not resolve target object."));
        }

        var propertyName = ParsePathSegment(SplitHistoryPath(row.PropertyPath ?? row.PropertyName).LastOrDefault() ?? row.PropertyName).PropertyName;
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return Result.Failure(new ValidationError($"Graph restore property {propertyName} was not found or is not settable."));
        }

        property.SetValue(target, DeserializeHistoryValue(serializedValue, property.PropertyType, serializer));

        return Result.Success();
    }

    private static string GetCollectionPath(string propertyPath)
    {
        var bracketIndex = propertyPath?.IndexOf('[', StringComparison.Ordinal) ?? -1;
        return bracketIndex < 0 ? propertyPath : propertyPath[..bracketIndex];
    }

    private static string GetCollectionItemPropertyName(string propertyPath)
    {
        var dotIndex = propertyPath?.LastIndexOf('.') ?? -1;
        return dotIndex < 0 ? propertyPath : propertyPath[(dotIndex + 1)..];
    }

    private static string GetGraphCollectionPath(string propertyPath)
        => string.Join('.', SplitHistoryPath(propertyPath).SkipLast(1).Select(p => ParsePathSegment(p).PropertyName));

    private static object GetValueByPath(object instance, string path)
    {
        var value = instance;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value is null)
            {
                return null;
            }

            value = value.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        }

        return value;
    }

    private static object FindCollectionItem(IEnumerable collection, Func<object, string> identity, string itemId)
    {
        if (collection is null || identity is null || string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        foreach (var item in collection)
        {
            if (item is not null && string.Equals(identity(item), itemId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static void SetConventionalId(object item, string itemId)
    {
        if (item is null || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        var idProperty = item.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (idProperty is null || !idProperty.CanWrite)
        {
            return;
        }

        object value = idProperty.PropertyType == typeof(Guid) && Guid.TryParse(itemId, out var guid)
            ? guid
            : idProperty.PropertyType == typeof(string)
                ? itemId
                : null;
        if (value is not null)
        {
            idProperty.SetValue(item, value);
        }
    }

    private static object FirstCollectionItem(object collection)
    {
        if (collection is not IEnumerable enumerable || collection is string)
        {
            return null;
        }

        foreach (var item in enumerable)
        {
            if (item is not null)
            {
                return item;
            }
        }

        return null;
    }

    private static Type GetCollectionItemType(Type collectionType)
    {
        if (collectionType.IsGenericType)
        {
            return collectionType.GetGenericArguments().SingleOrDefault();
        }

        return collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments().Single())
            .FirstOrDefault();
    }

    private static bool IsScalarType(Type type)
        => type.IsPrimitive ||
           type.IsEnum ||
           typeof(IEnumeration).IsAssignableFrom(type) ||
           type == typeof(string) ||
           type == typeof(Guid) ||
           type == typeof(DateTime) ||
           type == typeof(DateTimeOffset) ||
           type == typeof(decimal);

    private static string[] SplitHistoryPath(string propertyPath)
        => propertyPath?.Split('.', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static PathSegment ParsePathSegment(string segment)
    {
        var bracketIndex = segment.IndexOf('[', StringComparison.Ordinal);
        if (bracketIndex < 0)
        {
            return new PathSegment(segment, null);
        }

        var endBracketIndex = segment.IndexOf(']', bracketIndex + 1);
        return new PathSegment(
            segment[..bracketIndex],
            endBracketIndex < 0 ? null : segment[(bracketIndex + 1)..endBracketIndex]);
    }

    private static string GetCurrentModuleName()
        => Activity.Current?.GetBaggageItem(ModuleConstants.ModuleNameKey) ??
            Activity.Current?.GetBaggageItem(ActivityConstants.ModuleNameTagKey);

    private static string GetCurrentActivityParentId()
        => Activity.Current?.ParentId ?? Activity.Current?.Id;

    private string SerializeValue(object value) => value is null ? null : this.serializer.SerializeToString(value);

    private static string HashValue(string value)
    {
        if (value is null)
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }

    private sealed record PathSegment(string PropertyName, string ItemId);

    private sealed record GraphCollectionResolution(Result Result, IList Collection, Func<object, string> Identity, Type ItemType);
}
