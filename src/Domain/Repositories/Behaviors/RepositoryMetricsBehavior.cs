// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using BridgingIT.DevKit.Common;

/// <summary>
/// Emits repository read, write, and delete metrics for generic repository operations.
/// </summary>
/// <typeparam name="TEntity">The entity type handled by the repository.</typeparam>
/// <example>
/// <code>
/// services.AddRepository&lt;Order&gt;()
///     .WithBehavior&lt;RepositoryMetricsBehavior&lt;Order&gt;&gt;();
/// </code>
/// </example>
public class RepositoryMetricsBehavior<TEntity>(IMeterFactory meterFactory, IGenericRepository<TEntity> inner) : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly string entityName = Metrics.NormalizeTypeName(typeof(TEntity));

    /// <summary>
    /// Gets the decorated repository.
    /// </summary>
    protected IGenericRepository<TEntity> Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "insert", cancellationToken, () => this.Inner.InsertAsync(entity, cancellationToken));
    }

    /// <inheritdoc />
    public Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "update", cancellationToken, () => this.Inner.UpdateAsync(entity, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> UpdateSetAsync(Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "update_set", cancellationToken, () => this.Inner.UpdateSetAsync(set, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> UpdateSetAsync(ISpecification<TEntity> specification, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "update_set", cancellationToken, () => this.Inner.UpdateSetAsync(specification, set, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> UpdateSetAsync(IEnumerable<ISpecification<TEntity>> specifications, Action<IEntityUpdateSet<TEntity>> set, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "update_set", cancellationToken, () => this.Inner.UpdateSetAsync(specifications, set, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_write", "upsert", cancellationToken, () => this.Inner.UpsertAsync(entity, cancellationToken));
    }

    /// <inheritdoc />
    public Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_delete", "delete", cancellationToken, () => this.Inner.DeleteAsync(id, cancellationToken));
    }

    /// <inheritdoc />
    public Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_delete", "delete", cancellationToken, () => this.Inner.DeleteAsync(entity, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> DeleteSetAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_delete", "delete_set", cancellationToken, () => this.Inner.DeleteSetAsync(options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> DeleteSetAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_delete", "delete_set", cancellationToken, () => this.Inner.DeleteSetAsync(specification, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> DeleteSetAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_delete", "delete_set", cancellationToken, () => this.Inner.DeleteSetAsync(specifications, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<TEntity> FindOneAsync(object id, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_one", cancellationToken, () => this.Inner.FindOneAsync(id, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<TEntity> FindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_one", cancellationToken, () => this.Inner.FindOneAsync(specification, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<TEntity> FindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_one", cancellationToken, () => this.Inner.FindOneAsync(specifications, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "exists", cancellationToken, () => this.Inner.ExistsAsync(id, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TEntity>> FindAllAsync(IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_all", cancellationToken, () => this.Inner.FindAllAsync(options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TEntity>> FindAllAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_all", cancellationToken, () => this.Inner.FindAllAsync(specification, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TEntity>> FindAllAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "find_all", cancellationToken, () => this.Inner.FindAllAsync(specifications, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "project_all", cancellationToken, () => this.Inner.ProjectAllAsync(projection, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "project_all", cancellationToken, () => this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options = null, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "project_all", cancellationToken, () => this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "count", cancellationToken, () => this.Inner.CountAsync(specification, cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "count", cancellationToken, () => this.Inner.CountAsync(cancellationToken));
    }

    /// <inheritdoc />
    public Task<long> CountAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default)
    {
        return this.TrackAsync("repositories_read", "count", cancellationToken, () => this.Inner.CountAsync(specifications, cancellationToken));
    }

    private async Task<TResult> TrackAsync<TResult>(string family, string operation, CancellationToken cancellationToken, Func<Task<TResult>> action)
    {
        if (meterFactory is null || cancellationToken.IsCancellationRequested)
        {
            return await action().AnyContext();
        }

        var series = Metrics.Series(family);
        var typedSeries = Metrics.Series(family, this.entityName, operation);
        var currentSeries = Metrics.CurrentSeries(series);
        var currentTypedSeries = Metrics.CurrentSeries(typedSeries);
        var startedTimestamp = Metrics.StartTimestamp();

        Metrics.Increment(meterFactory, series);
        Metrics.Increment(meterFactory, typedSeries);
        Metrics.ChangeCurrent(meterFactory, currentSeries, 1);
        Metrics.ChangeCurrent(meterFactory, currentTypedSeries, 1);

        try
        {
            return await action().AnyContext();
        }
        catch
        {
            Metrics.Increment(meterFactory, Metrics.FailureSeries(series));
            Metrics.Increment(meterFactory, Metrics.FailureSeries(typedSeries));
            throw;
        }
        finally
        {
            Metrics.ChangeCurrent(meterFactory, currentSeries, -1);
            Metrics.ChangeCurrent(meterFactory, currentTypedSeries, -1);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(series), startedTimestamp);
            Metrics.RecordDuration(meterFactory, Metrics.DurationSeries(typedSeries), startedTimestamp);
        }
    }
}