// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using System.Linq.Expressions;
using Common;
using Microsoft.Extensions.Logging;
using Model;
using Specifications;

public class InMemoryRepositoryWrapper<TEntity, TDatabaseEntity, TContext>(
    ILoggerFactory loggerFactory,
    TContext context,
    Func<TDatabaseEntity, object> idSelector)
    : InMemoryRepository<TEntity, TDatabaseEntity>(loggerFactory, context, idSelector)
    where TEntity : class, IEntity
    where TContext : InMemoryContext<TEntity>
    where TDatabaseEntity : class { }

/// <summary>
///     Represents an InMemoryRepository.
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity.</typeparam>
/// <typeparam name="TDatabaseEntity">The type of the destination/remote dto.</typeparam>
/// <seealso cref="Domain.InMemoryRepository{T}" />
public class InMemoryRepository<TEntity, TDatabaseEntity> : InMemoryRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly Func<TDatabaseEntity, object> idSelector;

    public InMemoryRepository(InMemoryRepositoryOptions<TEntity> options, Func<TDatabaseEntity, object> idSelector)
        : base(options)
    {
        EnsureArg.IsNotNull(idSelector, nameof(idSelector));

        this.idSelector = idSelector; // TODO: really needed?
    }

    public InMemoryRepository(
        Builder<InMemoryRepositoryOptionsBuilder<TEntity>, InMemoryRepositoryOptions<TEntity>> optionsBuilder,
        Func<TDatabaseEntity, object> idSelector)
        : this(optionsBuilder(new InMemoryRepositoryOptionsBuilder<TEntity>()).Build(), idSelector) { }

    public InMemoryRepository(
        ILoggerFactory loggerFactory,
        InMemoryContext<TEntity> context,
        Func<TDatabaseEntity, object> idSelector)
        : this(o => o.LoggerFactory(loggerFactory).Context(context), idSelector) { }

    /// <summary>
    ///     Finds all asynchronous.
    /// </summary>
    /// <param name="specifications">The specifications.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellationToken.</param>
    public override async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        var result = this.Options.Context.Entities.SafeNull()
            .Select(e => this.Options.Mapper.Map<TDatabaseEntity>(e)); // work on destination objects

        foreach (var specification in specifications.SafeNull())
        {
            result = result.Where(
                this.EnsurePredicate(specification)); // translate specification to destination predicate
        }

        return await Task.FromResult(this.FindAll(result, options)).AnyContext();
    }

    /// <summary>
    ///     Finds the by identifier asynchronous.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">id.</exception>
    public override async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        if (id == default)
        {
            return default;
        }

        var result = this.Options.Context.Entities.SafeNull()
            .Select(e => this.Options.Mapper.Map<TDatabaseEntity>(e)) // work on destination objects
            .SingleOrDefault(e =>
                this.idSelector(e)
                    .Equals(id)); // TODO: use HasIdSpecification + MapExpression (makes idSelector obsolete)
        // return (await this.FindAllAsync(new HasIdSpecification<TEntity>(id))).FirstOrDefault();

        if (this.Options.Mapper is not null && result is not null)
        {
            return await Task.FromResult(this.Options.Mapper.Map<TEntity>(result)).AnyContext();
        }

        return default;
    }

    protected new Func<TDatabaseEntity, bool> EnsurePredicate(ISpecification<TEntity> specification)
    {
        return this.Options.Mapper.MapSpecification<TEntity, TDatabaseEntity>(specification).Compile();
    }

    protected IEnumerable<TEntity> FindAll(IEnumerable<TDatabaseEntity> entities, IFindOptions<TEntity> options = null)
    {
        var result = entities;

        if (options?.Distinct?.Expression is not null)
        {
            result = result.GroupBy(this.Options.Mapper
                    .MapExpression<Expression<Func<TDatabaseEntity, object>>>(options.Distinct.Expression)
                    .Compile())
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

        IOrderedEnumerable<TDatabaseEntity> orderedResult = null;
        foreach (var order in (options?.Orders ?? new List<OrderOption<TEntity>>()).Insert(options?.Order))
        {
            orderedResult = orderedResult is null
                ? order.Direction == OrderDirection.Ascending
                    ? result.OrderBy(this.Options.Mapper
                        .MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression)
                        .Compile())
                    : result.OrderByDescending(this.Options.Mapper
                        .MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression)
                        .Compile())
                : order.Direction ==
                OrderDirection.Ascending // replace wit CompileFast()? https://github.com/dadhi/FastExpressionCompiler
                    ? orderedResult.ThenBy(this.Options.Mapper
                        .MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression)
                        .Compile())
                    : orderedResult.ThenByDescending(this.Options.Mapper
                        .MapExpression<Expression<Func<TDatabaseEntity, object>>>(order.Expression)
                        .Compile());
        }

        if (orderedResult is not null)
        {
            result = orderedResult;
        }

        if (this.Options.Mapper is not null && result is not null)
        {
            return result.Select(d => this.Options.Mapper.Map<TEntity>(d));
        }

        return null;
    }
}