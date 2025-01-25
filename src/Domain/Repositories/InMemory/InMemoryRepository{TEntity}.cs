// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;

public class InMemoryRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context)
    : InMemoryRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : InMemoryContext<TEntity>
{ }

/// <summary>
///     Represents an InMemoryRepository.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
public class InMemoryRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly ReaderWriterLockSlim @lock = new();

    public InMemoryRepository(InMemoryRepositoryOptions<TEntity> options)
    {
        EnsureArg.IsNotNull(options, nameof(options));

        this.Options = options;
        this.Logger = options.CreateLogger<IGenericRepository<TEntity>>();
        this.Options.Context ??= new InMemoryContext<TEntity>();
        this.Options.IdGenerator ??= new InMemoryEntityIdGenerator<TEntity>(this.Options.Context);
    }

    public InMemoryRepository(
        Builder<InMemoryRepositoryOptionsBuilder<TEntity>, InMemoryRepositoryOptions<TEntity>> optionsBuilder)
        : this(optionsBuilder(new InMemoryRepositoryOptionsBuilder<TEntity>()).Build()) { }

    public InMemoryRepository(ILoggerFactory loggerFactory, InMemoryContext<TEntity> context)
        : this(o => o.LoggerFactory(loggerFactory).Context(context)) { }

    protected InMemoryRepositoryOptions<TEntity> Options { get; }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; set; }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(specifications: null, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return specification is null
            ? await this.FindAllAsync(specifications: null, options, cancellationToken).AnyContext()
            : await this.FindAllAsync([specification], options, cancellationToken).AnyContext();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var result = this.Options.Context.Entities.AsEnumerable();

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(this.EnsurePredicate(specification));
        }

        return await Task.FromResult(this.FindAll(result, options, cancellationToken).ToList()).AnyContext();
    }

    public virtual async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return default;
        }

        this.@lock.EnterReadLock();
        try
        {
            this.Options.Context.TryGet(id, out var entity);

            if (this.Options.Mapper is not null && entity is not null)
            {
                return this.Options.Mapper.Map<TEntity>(entity);
            }

            return await Task.FromResult(entity).AnyContext();
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindOneAsync([specification], options, cancellationToken);
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var result = this.Options.Context.Entities.AsEnumerable();

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(this.EnsurePredicate(specification));
        }

        return await Task.FromResult(this.FindAll(result, options, cancellationToken).FirstOrDefault()).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this
            .ProjectAllAsync(specifications: null, projection, options, cancellationToken: cancellationToken)
            .AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return specification is null
            ? await this.ProjectAllAsync(specifications: null, projection, options: options, cancellationToken).AnyContext()
            : await this.ProjectAllAsync([specification], projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var result = this.Options.Context.Entities.AsEnumerable();

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(this.EnsurePredicate(specification));
        }

        return await Task.FromResult(
                this.FindAll(result, options, cancellationToken)
                    .Select(e => projection.Compile().Invoke(e)))
            .AnyContext();
    }

    public virtual Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(this.Options.Context.TryGet(id, out _));
    }

    public virtual async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();

        return result.entity;
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (default, RepositoryActionResult.None);
        }

        bool isNew;
        if (this.Options.IdGenerator.IsNew(entity.Id))
        {
            this.Options.IdGenerator.SetNew(entity);
            isNew = true;
        }
        else
        {
            isNew = !await this.ExistsAsync(entity.Id, cancellationToken).AnyContext();
        }

        this.@lock.EnterWriteLock();
        try
        {
            if (!isNew)
            {
                this.Options.Context.TryGet(entity.Id, out var existingEntity);

                if (existingEntity is IConcurrency existingConcurrency && entity is IConcurrency entityConcurrency && this.Options.EnableOptimisticConcurrency)
                {
                    if (!existingConcurrency.ConcurrencyVersion.IsEmpty() && existingConcurrency.ConcurrencyVersion != entityConcurrency.ConcurrencyVersion)
                    {
                        throw new ConcurrencyException($"Concurrency conflict detected for entity {typeof(TEntity).Name} with Id {entity.Id}")
                        {
                            EntityId = entity.Id.ToString(),
                            ExpectedVersion = entityConcurrency.ConcurrencyVersion,
                            ActualVersion = existingConcurrency.ConcurrencyVersion
                        };
                    }
                }

                this.Options.Context.TryRemove(entity.Id, out _);
            }

            if (entity is IConcurrency concurrencyEntity)
            {
                concurrencyEntity.ConcurrencyVersion = GuidGenerator.CreateSequential();
            }

            this.Options.Context.TryAdd(entity);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);
    }

    public Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return Task.FromResult(RepositoryActionResult.None);
        }

        this.@lock.EnterWriteLock();
        try
        {
            if (this.Options.Context.TryRemove(id, out _))
            {
                return Task.FromResult(RepositoryActionResult.Deleted);
            }

            return Task.FromResult(RepositoryActionResult.None);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity?.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity.Id, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([specification], cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        CancellationToken cancellationToken = default)
    {
        var result = this.Options.Context.Entities.AsEnumerable();

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(this.EnsurePredicate(specification));
        }

        return await Task.FromResult(result.Count()).AnyContext();
    }

    protected virtual Func<TEntity, bool> EnsurePredicate(ISpecification<TEntity> specification)
    {
        return specification.ToPredicate();
    }

    protected virtual IEnumerable<TEntity> FindAll(
        IEnumerable<TEntity> entities,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        this.@lock.EnterReadLock();

        try
        {
            var result = entities;

            if (options?.Distinct?.Expression is not null)
            {
                result = result.GroupBy(options.Distinct.Expression.Compile()).Select(g => g.FirstOrDefault());
            }

            if (options?.Skip.HasValue == true && options.Skip.Value > 0)
            {
                result = result.Skip(options.Skip.Value);
            }

            if (options?.Take.HasValue == true && options.Take.Value > 0)
            {
                result = result.Take(options.Take.Value);
            }

            if (options?.Distinct is not null && options.Distinct.Expression is null)
            {
                result = result.Distinct();
            }
            else if (options?.Distinct is not null && options.Distinct.Expression is not null)
            {
                result = result.GroupBy(options.Distinct.Expression.Compile())
                    .Select(g => g.FirstOrDefault());
            }

            IOrderedEnumerable<TEntity> orderedResult = null;
            foreach (var order in (options?.Orders ?? []).Insert(options?.Order))
            {
                orderedResult = orderedResult is null ? order.Direction == OrderDirection.Ascending
                        ? result.OrderBy(order.Expression.Compile())
                        : result.OrderByDescending(order.Expression.Compile()) :
                    order.Direction == OrderDirection.Ascending ? orderedResult.ThenBy(order.Expression.Compile()) :
                    orderedResult.ThenByDescending(order.Expression.Compile());
            }

            if (orderedResult is not null)
            {
                result = orderedResult;
            }

            if (this.Options.Mapper is not null && result is not null)
            {
                return result.Select(r => this.Options.Mapper.Map<TEntity>(r));
            }

            return result;
        }
        finally
        {
            this.@lock.ExitReadLock();
        }
    }
}