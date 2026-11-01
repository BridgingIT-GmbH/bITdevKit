// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Records selected repository boundaries without inferring property changes or transaction commits.</summary>
/// <typeparam name="TEntity">The repository entity type.</typeparam>
/// <example><code>services.AddInMemoryRepository&lt;Customer&gt;().WithAuditing();</code></example>
public sealed class RepositoryAuditingBehavior<TEntity>(
    IGenericRepository<TEntity> inner,
    IAuditContext audit = null,
    RepositoryAuditOptions options = null,
    IServiceProvider services = null,
    IServiceScopeFactory scopes = null
) : IGenericRepository<TEntity> where TEntity : class, IEntity
{
    private readonly IGenericRepository<TEntity> inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    /// <example><code>await repository.InsertAsync(entity, cancellationToken);</code></example>
    public Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.AuditAsync(
            "Insert",
            cancellationToken,
            () => this.inner.InsertAsync(entity, cancellationToken), entity: entity
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.InsertSetAsync(entities, cancellationToken);</code></example>
    public Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "InsertSet",
            cancellationToken,
            () => this.inner.InsertSetAsync(entities, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpdateAsync(entity, cancellationToken);</code></example>
    public Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.AuditAsync(
            "Update",
            cancellationToken,
            () => this.inner.UpdateAsync(entity, cancellationToken), entity: entity
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpdateSetAsync(set, options, cancellationToken);</code></example>
    public Task<long> UpdateSetAsync(
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "UpdateSet",
            cancellationToken,
            () => this.inner.UpdateSetAsync(set, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpdateSetAsync(specification, set, options, cancellationToken);</code></example>
    public Task<long> UpdateSetAsync(
        ISpecification<TEntity> specification,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "UpdateSet",
            cancellationToken,
            () => this.inner.UpdateSetAsync(specification, set, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpdateSetAsync(specifications, set, options, cancellationToken);</code></example>
    public Task<long> UpdateSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        Action<IEntityUpdateSet<TEntity>> set,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "UpdateSet",
            cancellationToken,
            () => this.inner.UpdateSetAsync(specifications, set, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpsertAsync(entity, cancellationToken);</code></example>
    public Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "Upsert",
            cancellationToken,
            () => this.inner.UpsertAsync(entity, cancellationToken), entity: entity
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteAsync(id, cancellationToken);</code></example>
    public Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "Delete",
            cancellationToken,
            () => this.inner.DeleteAsync(id, cancellationToken), id: id
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteAsync(entity, cancellationToken);</code></example>
    public Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "Delete",
            cancellationToken,
            () => this.inner.DeleteAsync(entity, cancellationToken), entity: entity
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteSetAsync(options, cancellationToken);</code></example>
    public Task<long> DeleteSetAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "DeleteSet",
            cancellationToken,
            () => this.inner.DeleteSetAsync(options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteSetAsync(specification, options, cancellationToken);</code></example>
    public Task<long> DeleteSetAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "DeleteSet",
            cancellationToken,
            () => this.inner.DeleteSetAsync(specification, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteSetAsync(specifications, options, cancellationToken);</code></example>
    public Task<long> DeleteSetAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "DeleteSet",
            cancellationToken,
            () => this.inner.DeleteSetAsync(specifications, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindOneAsync(id, options, cancellationToken);</code></example>
    public Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindOne",
            cancellationToken,
            () => this.inner.FindOneAsync(id, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindOneAsync(specification, options, cancellationToken);</code></example>
    public Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindOne",
            cancellationToken,
            () => this.inner.FindOneAsync(specification, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindOneAsync(specifications, options, cancellationToken);</code></example>
    public Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindOne",
            cancellationToken,
            () => this.inner.FindOneAsync(specifications, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.ExistsAsync(id, cancellationToken);</code></example>
    public Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return this.AuditAsync(
            "Exists",
            cancellationToken,
            () => this.inner.ExistsAsync(id, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindAllAsync(options, cancellationToken);</code></example>
    public Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindAll",
            cancellationToken,
            () => this.inner.FindAllAsync(options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindAllAsync(specification, options, cancellationToken);</code></example>
    public Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindAll",
            cancellationToken,
            () => this.inner.FindAllAsync(specification, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.FindAllAsync(specifications, options, cancellationToken);</code></example>
    public Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "FindAll",
            cancellationToken,
            () => this.inner.FindAllAsync(specifications, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.ProjectAllAsync&lt;TProjection&gt;(projection, options, cancellationToken);</code></example>
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "ProjectAll",
            cancellationToken,
            () => this.inner.ProjectAllAsync(projection, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.ProjectAllAsync&lt;TProjection&gt;(specification, projection, options, cancellationToken);</code></example>
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "ProjectAll",
            cancellationToken,
            () => this.inner.ProjectAllAsync(specification, projection, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.ProjectAllAsync&lt;TProjection&gt;(specifications, projection, options, cancellationToken);</code></example>
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "ProjectAll",
            cancellationToken,
            () => this.inner.ProjectAllAsync(specifications, projection, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.CountAsync(specification, cancellationToken);</code></example>
    public Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "Count",
            cancellationToken,
            () => this.inner.CountAsync(specification, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.CountAsync(cancellationToken);</code></example>
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return this.AuditAsync(
            "Count",
            cancellationToken,
            () => this.inner.CountAsync(cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.CountAsync(specifications, cancellationToken);</code></example>
    public Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default
    )
    {
        return this.AuditAsync(
            "Count",
            cancellationToken,
            () => this.inner.CountAsync(specifications, cancellationToken)
        );
    }

    private Task<TResult> AuditAsync<TResult>(string operation, CancellationToken cancellationToken, Func<Task<TResult>> next, TEntity entity = null, object id = null)
    {
        if (audit is null && scopes is not null) return this.InScopeAsync(operation, cancellationToken, next, entity, id);
        if (audit?.IsEnabled != true) return next();
        if (IsRead(operation))
        {
            var selected = false;
            try { selected = options?.SelectRead?.Invoke(operation) == true; }
            catch (Exception) { (audit as IAuditCaptureDiagnostics)?.ReportFailure("AuditRepositorySelectionFailed"); }

            if (!selected) return next();
        }

        return AuditBehaviorExecutor.RunAsync(
            () => entity is null ? audit.BeginScope(builder => builder.For<TEntity>(operation).Kind("Repository"))
                : audit.BeginScopeFor(entity, services, builder => builder.For<TEntity>(operation).Kind("Repository")),
            next, new() { Feature = "Repository", ExecutionStatus = "Invoked" }, (scope, result) =>
        {
            if (!scope.IsRecording) return;
            AddResource(scope, entity?.Id ?? id);
            if (result is TEntity returned) AddResource(scope, returned.Id);
            if (result is ValueTuple<TEntity, RepositoryActionResult> upsert)
            {
                AddResource(scope, upsert.Item1?.Id);
                scope.SetDetail("repository.action", AuditValue.From(upsert.Item2.ToString()));
            }

            if (result is RepositoryActionResult action)
            {
                scope.SetDetail("repository.action", AuditValue.From(action.ToString()));
                if (action is RepositoryActionResult.None or RepositoryActionResult.NotFound) (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "NoChange" });
            }

            if (result is long count) scope.SetDetail("repository.observedCount", AuditValue.From(count));
            if (result is IReadOnlyCollection<TEntity> collection)
            {
                scope.SetDetail("repository.observedCount", AuditValue.From((long)collection.Count));
                foreach (var item in collection.Take(100)) AddResource(scope, item?.Id);
            }
        }, starting: scope =>
        {
            scope.SetDetail("repository.operation", AuditValue.From(operation));
            scope.SetDetail("repository.invocationId", AuditValue.From(Guid.NewGuid().ToString("D")));
            AddResource(scope, entity?.Id ?? id);
        });
    }
    private async Task<TResult> InScopeAsync<TResult>(string operation, CancellationToken cancellationToken, Func<Task<TResult>> next, TEntity entity, object id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetService<IAuditContext>();
        var observer = new RepositoryAuditingBehavior<TEntity>(this.inner, context, options, scope.ServiceProvider);
        return await observer.AuditAsync(operation, cancellationToken, next, entity, id).ConfigureAwait(false);
    }
    private static bool IsRead(string operation) => operation is "FindOne" or "FindAll" or "FindAllPaged" or "ProjectAll" or "Count";
    private static void AddResource(IAuditScope scope, object id)
    {
        if (id is null || id is Guid guid && guid == Guid.Empty || id is int number && number == 0 || id is long large && large == 0) return;
        scope.AddResource<TEntity>(id);
    }
}
