// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using BridgingIT.DevKit.Common;

/// <summary>Profiles repository calls, joining an existing operation or owning an independent operation.</summary>
/// <typeparam name="TEntity">The entity type handled by the repository.</typeparam>
/// <param name="inner">The repository whose execution is measured.</param>
/// <param name="profiling">The optional operation profiler; omission preserves ordinary repository execution.</param>
/// <example><code>services.AddInMemoryRepository&lt;Order&gt;().WithBehavior&lt;RepositoryProfilingBehavior&lt;Order&gt;&gt;();</code></example>
public sealed class RepositoryProfilingBehavior<TEntity>(
    IGenericRepository<TEntity> inner,
    IOperationProfiler profiling = null
) : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private static readonly string entityType = typeof(TEntity).FullName ?? typeof(TEntity).Name;
    private static readonly string keyPrefix = "repository:" + typeof(TEntity).Name + ":";
    private readonly IGenericRepository<TEntity> inner =
        inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    /// <example><code>await repository.InsertAsync(entity, cancellationToken);</code></example>
    public Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.ProfileAsync(
            "Insert",
            cancellationToken,
            () => this.inner.InsertAsync(entity, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.InsertSetAsync(entities, cancellationToken);</code></example>
    public Task<IEnumerable<TEntity>> InsertSetAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default
    )
    {
        return this.ProfileAsync(
            "InsertSet",
            cancellationToken,
            () => this.inner.InsertSetAsync(entities, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.UpdateAsync(entity, cancellationToken);</code></example>
    public Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.ProfileAsync(
            "Update",
            cancellationToken,
            () => this.inner.UpdateAsync(entity, cancellationToken)
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
            "Upsert",
            cancellationToken,
            () => this.inner.UpsertAsync(entity, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteAsync(id, cancellationToken);</code></example>
    public Task<RepositoryActionResult> DeleteAsync(
        object id,
        CancellationToken cancellationToken = default
    )
    {
        return this.ProfileAsync(
            "Delete",
            cancellationToken,
            () => this.inner.DeleteAsync(id, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteAsync(entity, cancellationToken);</code></example>
    public Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default
    )
    {
        return this.ProfileAsync(
            "Delete",
            cancellationToken,
            () => this.inner.DeleteAsync(entity, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.DeleteSetAsync(options, cancellationToken);</code></example>
    public Task<long> DeleteSetAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default
    )
    {
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
            "FindOne",
            cancellationToken,
            () => this.inner.FindOneAsync(specifications, options, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.ExistsAsync(id, cancellationToken);</code></example>
    public Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
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
        return this.ProfileAsync(
            "Count",
            cancellationToken,
            () => this.inner.CountAsync(specification, cancellationToken)
        );
    }

    /// <inheritdoc />
    /// <example><code>await repository.CountAsync(cancellationToken);</code></example>
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return this.ProfileAsync(
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
        return this.ProfileAsync(
            "Count",
            cancellationToken,
            () => this.inner.CountAsync(specifications, cancellationToken)
        );
    }

    private Task<TResult> ProfileAsync<TResult>(
        string operation,
        CancellationToken cancellationToken,
        Func<Task<TResult>> next
    )
    {
        if (profiling is null)
        {
            return next();
        }

        return profiling.JoinOrStartAsync(
            new OperationProfilingStartRequest { Key = keyPrefix + operation, Kind = "Repository" },
            (scope, _) =>
            {
                try
                {
                    if (scope.IsRecording)
                    {
                        scope.SetDimension("repository.entityType", entityType);
                        scope.SetDimension("repository.operation", operation);
                    }
                }
                catch (Exception)
                { /* Observation faults cannot change repository execution. */
                }

                return next();
            },
            cancellationToken
        );
    }
}
