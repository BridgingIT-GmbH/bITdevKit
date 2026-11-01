// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System.Collections.Immutable;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;

/// <summary>Observes complete Active Entity operations and their lifecycle hooks without inferring commits or property deltas.</summary>
/// <example><code>builder.WithBehavior&lt;ActiveEntityAuditingBehavior&lt;Customer&gt;&gt;();</code></example>
public sealed class ActiveEntityAuditingBehavior<TEntity>(IAuditContext audit = null, RepositoryAuditOptions options = null)
    : ActiveEntityBehaviorBase<TEntity>, IActiveEntityOperationBehavior<TEntity> where TEntity : class, IEntity
{
    private readonly AsyncLocal<ImmutableStack<IAuditScope>> scopes = new();

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync<TResult>(string operation, Func<Task<TResult>> next, CancellationToken cancellationToken = default)
    {
        if (audit?.IsEnabled != true) return next();
        var name = operation?.EndsWith("Async", StringComparison.Ordinal) == true ? operation[..^5] : operation;
        if (name is not ("Insert" or "InsertSet" or "Update" or "UpdateSet" or "Upsert" or "Delete" or "DeleteSet"))
        {
            var selected = false;
            try { selected = options?.SelectRead?.Invoke(name) == true; }
            catch (Exception) { (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditActiveEntitySelectionFailed"); }

            if (!selected) return next();
        }

        return this.RunAsync(name, next);
    }

    private async Task<TResult> RunAsync<TResult>(string operation, Func<Task<TResult>> next)
    {
        var previous = this.scopes.Value ?? ImmutableStack<IAuditScope>.Empty;
        try
        {
            return await AuditBehaviorExecutor.RunAsync(() => audit.BeginScope(builder => builder.For<TEntity>(operation).Kind("ActiveEntity")),
                next, new() { Feature = "ActiveEntity" }, (scope, result) =>
                {
                    if (result is TEntity entity) Add(scope, entity.Id);
                    if (result is IResult<TEntity> found && found.IsSuccess) Add(scope, found.Value?.Id);
                    if (result is IResult<long> total && total.IsSuccess) scope.SetDetail("activeEntity.observedCount", AuditValue.From(total.Value));
                    if (result is long count) scope.SetDetail("activeEntity.observedCount", AuditValue.From(count));
                }, starting: scope =>
                {
                    this.scopes.Value = previous.Push(scope);
                    scope.SetDetail("activeEntity.operation", AuditValue.From(operation));
                    scope.SetDetail("activeEntity.invocationId", AuditValue.From(Guid.NewGuid().ToString("D")));
                }).ConfigureAwait(false);
        }
        finally { this.scopes.Value = previous; }
    }
    private Task<Result> Resource(Func<object> identity)
    {
        var scope = this.scopes.Value?.FirstOrDefault();
        try { if (scope?.IsRecording == true) Add(scope, identity()); }
        catch (Exception) { (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditActiveEntityMetadataFailed"); }

        return Task.FromResult(Result.Success());
    }
    private static void Add(IAuditScope scope, object id)
    {
        if (id is null || id is Guid guid && guid == Guid.Empty || id is int integer && integer == 0 || id is long number && number == 0) return;
        scope.AddResource<TEntity>(id);
    }

    /// <inheritdoc />
    public override Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> AfterInsertAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> AfterUpdateAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> AfterUpsertAsync(TEntity entity, RepositoryActionResult action, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> AfterDeleteAsync(TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> BeforeDeleteAsync(object id, CancellationToken cancellationToken = default) => this.Resource(() => id);

    /// <inheritdoc />
    public override Task<Result> AfterDeleteAsync(object id, bool success, CancellationToken cancellationToken = default) => this.Resource(() => id);

    /// <inheritdoc />
    public override Task<Result> AfterFindOneAsync(object id, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id ?? id);

    /// <inheritdoc />
    public override Task<Result> AfterFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);

    /// <inheritdoc />
    public override Task<Result> AfterFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, TEntity entity, bool success, CancellationToken cancellationToken = default) => this.Resource(() => entity?.Id);
}
