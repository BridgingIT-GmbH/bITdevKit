// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Specifications;
using EnsureThat;
using Microsoft.Extensions.Logging;

public class InMemoryRepositoryWrapper<TEntity, TContext>(ILoggerFactory loggerFactory, TContext context) : InMemoryRepository<TEntity>(loggerFactory, context)
    where TEntity : class, IEntity
    where TContext : InMemoryContext<TEntity>
{
}

/// <summary>
/// Represents an InMemoryRepository.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
/// <seealso cref="Domain.IRepository{T, TId}" />
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
        : this(optionsBuilder(new InMemoryRepositoryOptionsBuilder<TEntity>()).Build())
    {
    }

    public InMemoryRepository(ILoggerFactory loggerFactory, InMemoryContext<TEntity> context)
        : this(o => o.LoggerFactory(loggerFactory).Context(context))
    {
    }

    protected InMemoryRepositoryOptions<TEntity> Options { get; }

    protected ILogger<IGenericRepository<TEntity>> Logger { get; set; }

    /// <summary>
    /// Finds all asynchronous.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.FindAllAsync(specifications: null, options: options, cancellationToken: cancellationToken)
            .AnyContext();
    }

    /// <summary>
    /// Finds all asynchronous.
    /// </summary>
    /// <param name="specification">The specification.</param>
    /// <param name="options">The options.</param>
    /// /// <param name="cancellationToken">The cancellationToken.</param>
    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return specification is null
            ? await this.FindAllAsync(specifications: null, options: options, cancellationToken).AnyContext()
            : await this.FindAllAsync(new[] { specification }, options, cancellationToken).AnyContext();
    }

    /// <summary>
    /// Finds all asynchronous.
    /// </summary>
    /// <param name="specifications">The specifications.</param>
    /// <param name="options">The options.</param>
    /// /// <param name="cancellationToken">The cancellationToken.</param>
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

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        return await this.ProjectAllAsync(specifications: null, projection, options, cancellationToken: cancellationToken)
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
            : await this.ProjectAllAsync(new[] { specification }, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
       IEnumerable<ISpecification<TEntity>> specifications,
       Expression<Func<TEntity, TProjection>> projection,
       IFindOptions<TEntity> options = null,
       CancellationToken cancellationToken = default)
    {
        return (await this.FindAllAsync(specifications, options, cancellationToken).AnyContext())
                .Select(e => projection.Compile().Invoke(e));
    }

    /// <summary>
    /// Finds the by identifier asynchronous.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">id.</exception>
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
            var result = this.Options.Context.Entities.FirstOrDefault(x => x.Id.Equals(id));

            if (this.Options.Mapper is not null && result is not null)
            {
                return this.Options.Mapper.Map<TEntity>(result);
            }

            return await Task.FromResult(result).AnyContext();
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
        return await this.FindOneAsync(new[] { specification }, options, cancellationToken);
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

    /// <summary>
    /// Asynchronous checks if element exists.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public virtual async Task<bool> ExistsAsync(
        object id,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return false;
        }

        return await this.FindOneAsync(id, cancellationToken: cancellationToken).AnyContext() is not null;
    }

    /// <summary>
    /// Inserts the provided entity.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    public virtual async Task<TEntity> InsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();
        return result.entity;
    }

    /// <summary>
    /// Updates the provided entity.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public virtual async Task<TEntity> UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        var result = await this.UpsertAsync(entity, cancellationToken).AnyContext();
        return result.entity;
    }

    /// <summary>
    /// Insert or updates the entity.
    /// </summary>
    /// <param name="entity">The entity to insert or update.</param>
    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity is null)
        {
            return (default, RepositoryActionResult.None);
        }

        var isNew = false;
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
            if (this.Options.Context.Entities.Contains(entity))
            {
                this.Options.Context.Entities.Remove(entity);
            }

            this.Options.Context.Entities.Add(entity);
        }
        finally
        {
            this.@lock.ExitWriteLock();
        }

        return isNew ? (entity, RepositoryActionResult.Inserted) : (entity, RepositoryActionResult.Updated);
    }

    /// <summary>
    /// Deletes asynchronous.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">id.</exception>
    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return RepositoryActionResult.None;
        }

        return await Task.Run(() =>
        {
            var entity = this.Options.Context.Entities.FirstOrDefault(x => x.Id.Equals(id));
            if (entity is not null)
            {
                this.@lock.EnterWriteLock();
                try
                {
                    this.Options.Context.Entities.Remove(entity);
                }
                finally
                {
                    this.@lock.ExitWriteLock();
                }

                return RepositoryActionResult.Deleted;
            }

            return RepositoryActionResult.None;
        }).AnyContext();
    }

    /// <summary>
    /// Deletes asynchronous.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <exception cref="ArgumentOutOfRangeException">Id.</exception>
    public async Task<RepositoryActionResult> DeleteAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        if (entity?.Id == default)
        {
            return RepositoryActionResult.None;
        }

        return await this.DeleteAsync(entity.Id, cancellationToken).AnyContext();
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

        var count = result.Count();
        return await Task.FromResult(count).AnyContext();
    }

    public async Task<long> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await this.CountAsync(new[] { specification }, cancellationToken).AnyContext();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await this.CountAsync([], cancellationToken).AnyContext();
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
                result = result.GroupBy(options.Distinct.Expression.Compile())
                    .Select(g => g.FirstOrDefault());
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
                result = result
                    .GroupBy(options.Distinct.Expression.Compile())
                    .Select(g => g.FirstOrDefault())
                    .AsQueryable();
            }

            IOrderedEnumerable<TEntity> orderedResult = null;
            foreach (var order in (options?.Orders ?? new List<OrderOption<TEntity>>()).Insert(options?.Order))
            {
                orderedResult = orderedResult is null
                    ? order.Direction == OrderDirection.Ascending
                        ? result.OrderBy(order.Expression
                            .Compile()) // replace wit CompileFast()? https://github.com/dadhi/FastExpressionCompiler
                        : result.OrderByDescending(order.Expression.Compile())
                    : order.Direction == OrderDirection.Ascending
                        ? orderedResult.ThenBy(order.Expression.Compile())
                        : orderedResult.ThenByDescending(order.Expression.Compile());
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