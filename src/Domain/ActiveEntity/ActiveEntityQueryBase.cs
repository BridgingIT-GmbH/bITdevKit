// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using BridgingIT.DevKit.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

/// <summary>
/// Provides a fluent DSL for building queries against an ActiveEntity entity.
/// This DSL wraps the underlying ActiveEntity static methods (FindOne, FindAll, Exists, Count, ProjectAll, etc.)
/// and ensures all results are returned as <see cref="Result{T}"/> or <see cref="ResultPaged{T}"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TId">The entity ID type.</typeparam>
public abstract class ActiveEntityQueryBase<TEntity, TId>
    where TEntity : ActiveEntity<TEntity, TId>
{
    private readonly List<ISpecification<TEntity>> specifications = [];
    protected readonly FindOptions<TEntity> options = new();

    /// <summary>
    /// Adds a filter to the query using a LINQ expression.
    /// </summary>
    /// <param name="predicate">The filter expression.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Where(c => c.LastName == "Doe")
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> Where(Expression<Func<TEntity, bool>> predicate)
    {
        this.specifications.Add(new Specification<TEntity>(predicate));
        return this;
    }

    /// <summary>
    /// Adds a filter to the query using a specification.
    /// </summary>
    /// <param name="specification">The specification to apply.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Where(Customer.Specifications.IsActiveEquals(true))
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> Where(ISpecification<TEntity> specification)
    {
        this.specifications.Add(specification);
        return this;
    }

    /// <summary>
    /// Adds multiple filters to the query using specifications.
    /// </summary>
    /// <param name="specifications">The specifications to apply.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Where(
    ///         Customer.Specifications.LastNameEquals("Doe"),
    ///         Customer.Specifications.IsActiveEquals(true))
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> Where(params ISpecification<TEntity>[] specifications)
    {
        if (specifications?.Length > 0)
        {
            this.specifications.AddRange(specifications);
        }
        return this;
    }

    /// <summary>
    /// Combines the last filter with another specification using logical AND.
    /// </summary>
    /// <param name="specification">The specification to combine.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Where(Customer.Specifications.LastNameEquals("Doe"))
    ///     .And(Customer.Specifications.IsActiveEquals(true))
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> And(ISpecification<TEntity> specification)
    {
        if (this.specifications.Count == 0)
        {
            this.specifications.Add(specification);
        }
        else
        {
            this.specifications[^1] = this.specifications[^1].And(specification);
        }

        return this;
    }

    /// <summary>
    /// Combines the last filter with another specification using logical OR.
    /// </summary>
    /// <param name="specification">The specification to combine.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Where(Customer.Specifications.LastNameEquals("Doe"))
    ///     .Or(Customer.Specifications.LastNameEquals("Smith"))
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> Or(ISpecification<TEntity> specification)
    {
        if (this.specifications.Count == 0)
        {
            this.specifications.Add(specification);
        }
        else
        {
            this.specifications[^1] = this.specifications[^1].Or(specification);
        }

        return this;
    }

    /// <summary>
    /// Adds an include for eager loading of a navigation property.
    /// </summary>
    /// <param name="navigation">The navigation property to include.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .Include(c => c.Orders)
    ///     .Include(c => c.Reviews)
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> Include(Expression<Func<TEntity, object>> navigation)
    {
        var include = new IncludeOption<TEntity>(navigation);
        this.options.Includes ??= [];
        this.options.Includes.Add(include);
        this.options.Include = include;
        this.options.Include ??= include;
        return this;
    }

    /// <summary>
    /// Orders the query results ascending by the given key selector.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .OrderBy(c => c.LastName)
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> OrderBy(Expression<Func<TEntity, object>> keySelector)
    {
        var order = new OrderOption<TEntity>(keySelector, OrderDirection.Ascending);
        this.options.Orders ??= [];
        this.options.Orders.Add(order);
        this.options.Order ??= order;
        return this;
    }

    /// <summary>
    /// Orders the query results descending by the given key selector.
    /// </summary>
    /// <param name="keySelector">The key selector expression.</param>
    /// <returns>The query instance for chaining.</returns>
    /// <example>
    /// var result = await Customer.Query()
    ///     .OrderByDescending(c => c.LastName)
    ///     .ToListAsync();
    /// </example>
    public ActiveEntityQueryBase<TEntity, TId> OrderByDescending(Expression<Func<TEntity, object>> keySelector)
    {
        var order = new OrderOption<TEntity>(keySelector, OrderDirection.Descending);
        this.options.Orders ??= [];
        this.options.Orders.Add(order);
        this.options.Order ??= order;
        return this;
    }

    /// <summary>
    /// Skips the given number of results.
    /// </summary>
    public ActiveEntityQueryBase<TEntity, TId> Skip(int count)
    {
        this.options.Skip = count;
        return this;
    }

    /// <summary>
    /// Takes the given number of results.
    /// </summary>
    public ActiveEntityQueryBase<TEntity, TId> Take(int count)
    {
        this.options.Take = count;
        return this;
    }

    // --- Abstract methods to be implemented by generated per-entity query classes ---

    protected abstract Task<Result<IEnumerable<TEntity>>> FindAllInternal(
        IEnumerable<ISpecification<TEntity>> specs,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<ResultPaged<TEntity>> FindAllPagedInternal(
        IEnumerable<ISpecification<TEntity>> specs,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<Result<TEntity>> FindOneInternal(
        IEnumerable<ISpecification<TEntity>> specs,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<Result<bool>> ExistsInternal(
        IEnumerable<ISpecification<TEntity>> specs,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<Result<long>> CountInternal(
        IEnumerable<ISpecification<TEntity>> specs,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<Result<IEnumerable<TProjection>>> ProjectAllInternal<TProjection>(
        IEnumerable<ISpecification<TEntity>> specs,
        Expression<Func<TEntity, TProjection>> selector,
        FindOptions<TEntity> options,
        CancellationToken ct);

    protected abstract Task<ResultPaged<TProjection>> ProjectAllPagedInternal<TProjection>(
        IEnumerable<ISpecification<TEntity>> specs,
        Expression<Func<TEntity, TProjection>> selector,
        FindOptions<TEntity> options,
        CancellationToken ct);

    // --- Execution methods (all return Result<T>) ---

    /// <summary>
    /// Executes the query and returns all matching entities.
    /// </summary>
    public async Task<Result<IEnumerable<TEntity>>> ToListAsync(CancellationToken ct = default) =>
        await this.FindAllInternal(this.specifications, this.options, ct);

    /// <summary>
    /// Executes the query and returns a paged result of matching entities.
    /// </summary>
    public async Task<ResultPaged<TEntity>> ToPagedListAsync(CancellationToken ct = default) =>
        await this.FindAllPagedInternal(this.specifications, this.options, ct);

    /// <summary>
    /// Returns the first matching entity or null if none found.
    /// </summary>
    public async Task<Result<TEntity>> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        this.options.Take = 1;
        return await this.FindOneInternal(this.specifications, this.options, ct);
    }

    /// <summary>
    /// Returns the first matching entity or fails with <see cref="NotFoundError"/> if none found.
    /// </summary>
    public async Task<Result<TEntity>> FirstAsync(CancellationToken ct = default)
    {
        this.options.Take = 1;
        var result = await this.FindOneInternal(this.specifications, this.options, ct);
        if (result.IsSuccess && result.Value == null)
        {
            return Result<TEntity>.Failure(new NotFoundError(typeof(TEntity).Name));
        }
        return result;
    }

    /// <summary>
    /// Returns true if any entity matches the query.
    /// </summary>
    public async Task<Result<bool>> AnyAsync(CancellationToken ct = default) =>
        await this.ExistsInternal(this.specifications, this.options, ct);

    /// <summary>
    /// Returns the count of entities matching the query.
    /// </summary>
    public async Task<Result<long>> CountAsync(CancellationToken ct = default) =>
        await this.CountInternal(this.specifications, this.options, ct);

    /// <summary>
    /// Projects all matching entities into a new shape.
    /// </summary>
    public async Task<Result<IEnumerable<TProjection>>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        CancellationToken ct = default) =>
        await this.ProjectAllInternal(this.specifications, projection, this.options, ct);

    /// <summary>
    /// Projects all matching entities into a new shape with paging.
    /// </summary>
    public async Task<ResultPaged<TProjection>> ProjectAllPagedAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        CancellationToken ct = default) =>
        await this.ProjectAllPagedInternal(this.specifications, projection, this.options, ct);
}