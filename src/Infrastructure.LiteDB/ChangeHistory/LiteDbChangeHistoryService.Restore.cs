// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.LiteDb;

using System.Reflection;
using System.Text.Json;
using BridgingIT.DevKit.Application.Entities;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Infrastructure.LiteDb.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed partial class LiteDbChangeHistoryService<TEntity> where TEntity : class, IEntity
{
    /// <inheritdoc />
    public Task<Result<ChangeHistoryRestoreResult>> RestoreAsync(ChangeHistoryRestoreCommand<TEntity> command, CancellationToken cancellationToken = default) =>
        this.GuardAsync(async () =>
        {
            if (command?.EntityId is null || command.ChangeSetId == Guid.Empty || !Enum.IsDefined(command.RestoreMode))
            {
                return Result<ChangeHistoryRestoreResult>.Failure(new InvalidInputError("A valid entity, change set, and restore mode are required."));
            }

            await this.AuthorizeAsync("Restore", command.EntityId.ToString(), command.Reason, cancellationToken).ConfigureAwait(false);
            return await session.ExecuteAsync(async () =>
            {
                var settings = options.GetEntityOptions(typeof(TEntity))!;
                var store = new LiteDbRepositoryStore<TEntity>(new LiteDbRepositoryOptions { Session = session, CollectionName = entityCollection });
                var state = await session.NativeAsync(database =>
                {
                    var entity = store.Find(database, command.EntityId) ?? throw new LiteDbProviderException(new NotFoundError());
                    var id = entity.Id.ToString();
                    var selected = this.Limited(this.EntityQuery(database).Where(BsonExpression.Create("$.EntityId = @0 AND $.ChangeSetId = @1", id, command.ChangeSetId)))
                        .Select(d => LiteDbChangeHistoryStorage.Read(d)).OrderBy(r => r.ChangeSetSequence).ToArray();
                    if (selected.Length == 0) { throw new LiteDbProviderException(new NotFoundError("The selected change set does not belong to this entity.")); }

                    IReadOnlyList<ChangeHistoryRecord> rows = selected;
                    if (command.RestoreMode == ChangeHistoryRestoreMode.PointInTime)
                    {
                        var from = selected.Min(r => r.ChangedDateTicks);
                        rows = this.Limited(this.EntityQuery(database).Where(BsonExpression.Create("$.EntityId = @0 AND $.ChangedDateTicks >= @1", id, from)))
                            .Select(d => LiteDbChangeHistoryStorage.Read(d)).OrderBy(r => r.ChangedDateTicks).ThenBy(r => r.ChangeSetSequence).ThenBy(r => r.Id)
                            .GroupBy(r => r.PropertyPath).Select(g => g.First()).ToArray();
                    }

                    if (rows.Any(r => !r.IsRestoreable)) { throw new LiteDbProviderException(new ValidationError("The restore selection contains values that cannot be restored.")); }

                    if (entity is IConcurrency concurrency && settings.RestoreConcurrencyPolicy != ChangeHistoryRestoreConcurrencyPolicy.None)
                    {
                        if (settings.RestoreConcurrencyPolicy == ChangeHistoryRestoreConcurrencyPolicy.RequireExpectedVersion && !command.ExpectedConcurrencyVersion.HasValue)
                        {
                            throw new LiteDbProviderException(new ValidationError("Restore requires an expected concurrency version."));
                        }

                        if (command.ExpectedConcurrencyVersion.HasValue && command.ExpectedConcurrencyVersion != concurrency.ConcurrencyVersion)
                        {
                            throw new LiteDbProviderException(new ConcurrencyError("The entity changed after the restore was requested."));
                        }
                    }

                    return (Entity: entity, Rows: rows);
                }, cancellationToken).ConfigureAwait(false);
                if (settings.RestoreAuthorizerType is not null)
                {
                    var authorizer = (IChangeHistoryRestoreAuthorizer<TEntity>)services.GetRequiredService(settings.RestoreAuthorizerType);
                    var authorized = await authorizer.AuthorizeAsync(state.Entity, new(command.ChangeSetId, command.Reason), cancellationToken).ConfigureAwait(false);
                    if (authorized.IsFailure) { throw new LiteDbProviderException(authorized); }
                }

                var pathGroups = state.Rows.Where(r => r.PathKind != "Scalar").GroupBy(r => settings.CapturePaths.FirstOrDefault(p =>
                    r.PropertyPath == p.Path || r.PropertyPath.StartsWith(p.Path + ".", StringComparison.Ordinal) || r.PropertyPath.StartsWith(p.Path + "[", StringComparison.Ordinal)));
                foreach (var group in pathGroups)
                {
                    if (group.Key?.RestorePlanType is null) { throw new LiteDbProviderException(new ValidationError("A domain restore plan is required for the configured history path.")); }

                    var plan = (IChangeHistoryGraphRestorePlan<TEntity>)services.GetRequiredService(group.Key.RestorePlanType);
                    var values = group.Select(r => { var type = Type.GetType(r.ValueClrType, true)!; return new ChangeHistoryGraphRestoreValue(r.PropertyPath, JsonSerializer.Deserialize(r.OldValue, type), type); }).ToArray();
                    var applied = await plan.RestoreAsync(state.Entity, values, cancellationToken).ConfigureAwait(false);
                    if (applied.IsFailure) { throw new LiteDbProviderException(applied); }
                }

                foreach (var row in state.Rows.Where(r => r.PathKind == "Scalar"))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (row.PropertyName is "Id" or "ConcurrencyVersion" || !settings.RestorePolicies.TryGetValue(row.PropertyName, out var policy))
                    {
                        throw new LiteDbProviderException(new ValidationError("The selected property is not allowed by the current restore policy."));
                    }

                    var property = typeof(TEntity).GetProperty(row.PropertyName, BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new LiteDbProviderException(new ValidationError("The selected restore property no longer exists."));
                    var value = JsonSerializer.Deserialize(row.OldValue, property.PropertyType);
                    Result applied;
                    if (policy.ExecutionMode == ChangeHistoryRestoreExecutionMode.ValidatedSetter)
                    {
                        if (property.SetMethod?.IsPublic != true) { throw new LiteDbProviderException(new ValidationError("The restore property has no public setter.")); }

                        property.SetValue(state.Entity, value);
                        applied = Result.Success();
                    }
                    else if (policy.DomainMethod is not null)
                    {
                        var arguments = policy.DomainMethod.Method.GetParameters().Length == 3 ? new object[] { state.Entity, value, cancellationToken } : [state.Entity, value];
                        var invocation = policy.DomainMethod.DynamicInvoke(arguments);
                        applied = invocation is Task<Result> task ? await task.ConfigureAwait(false) : invocation is Result outcome ? outcome : throw new LiteDbProviderException(new ValidationError("The domain restore method must return Result or Task<Result>."));
                    }
                    else if (policy.HandlerType is not null)
                    {
                        var handler = (IChangeHistoryRestoreHandler<TEntity>)services.GetRequiredService(policy.HandlerType);
                        applied = await handler.RestoreAsync(state.Entity, new(row.PropertyName, value, property.PropertyType, command.ChangeSetId, command.Reason), cancellationToken).ConfigureAwait(false);
                    }
                    else { throw new LiteDbProviderException(new ValidationError("The restore policy does not define domain logic.")); }

                    if (applied.IsFailure) { throw new LiteDbProviderException(applied); }
                }

                var restoredSetId = Guid.NewGuid();
                var previous = session.CaptureContext;
                session.CaptureContext = new("Restore", command.Reason, command.ChangeSetId, restoredSetId);
                try
                {
                    await session.NativeAsync(database => store.Upsert(database, state.Entity, source: "Restore"), cancellationToken).ConfigureAwait(false);
                }
                finally { session.CaptureContext = previous; }

                return Result<ChangeHistoryRestoreResult>.Success(new(restoredSetId, state.Rows.Count));
            }, true, cancellationToken).ConfigureAwait(false);
        });
}
