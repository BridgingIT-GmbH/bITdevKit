// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain.Repositories;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[Obsolete("Use GenericRepositoryLoggingBehavior instead")]
public class GenericRepositoryLoggingDecorator<TEntity>(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
    : RepositoryLoggingBehavior<TEntity>(loggerFactory, inner)
    where TEntity : class, IEntity
{ }

/// <summary>
///     <para>Decorates an <see cref="IGenericRepository{TEntity}" />.</para>
///     <para>
///         .-----------.
///         | Decorator |
///         .-----------.        .------------.
///         `------------> | decoratee  |
///         (forward)    .------------.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <seealso cref="IGenericRepository{TEntity}" />
public partial class RepositoryLoggingBehavior<TEntity>(ILoggerFactory loggerFactory, IGenericRepository<TEntity> inner)
    : IGenericRepository<TEntity>
    where TEntity : class, IEntity
{
    private readonly string type = typeof(TEntity).Name;

    protected ILogger<IGenericRepository<TEntity>> Logger { get; } =
        loggerFactory?.CreateLogger<IGenericRepository<TEntity>>() ??
        NullLoggerFactory.Instance.CreateLogger<IGenericRepository<TEntity>>();

    protected IGenericRepository<TEntity> Inner { get; } = inner;

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
        TypedLogger.LogCount(this.Logger, Constants.LogKey, this.type);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("{LogKey} repository specification: {Specification} -> {SpecificationExpression}",
                Constants.LogKey,
                specification.GetType().PrettyName(),
                specification.ToExpressionString());
        }

        return await this.Inner.CountAsync(specifications, cancellationToken).AnyContext();
    }

    public async Task<RepositoryActionResult> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.type, id);

        return await this.Inner.DeleteAsync(id, cancellationToken).AnyContext();
    }

    public async Task<RepositoryActionResult> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.Logger, Constants.LogKey, this.type, entity?.Id);

        return await this.Inner.DeleteAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<bool> ExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.Logger, Constants.LogKey, this.type, id);

        return await this.Inner.ExistsAsync(id, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return await this.Inner.FindAllAsync(options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        if (specification is not null)
        {
            this.Logger.LogDebug("{LogKey} repository specification: {Specification} -> {SpecificationExpression}",
                Constants.LogKey,
                specification.GetType().PrettyName(),
                specification.ToExpressionString());
        }

        return await this.Inner.FindAllAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TEntity>> FindAllAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("{LogKey} repository specification: {Specification} -> {SpecificationExpression}",
                Constants.LogKey,
                specification.GetType().PrettyName(),
                specification.ToExpressionString());
        }

        return await this.Inner.FindAllAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return await this.Inner.ProjectAllAsync(projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        ISpecification<TEntity> specification,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        if (specification is not null)
        {
            this.Logger.LogDebug("{LogKey} repository specification: {Specification} -> {SpecificationExpression}",
                Constants.LogKey,
                specification.GetType().PrettyName(),
                specification.ToExpressionString());
        }

        return await this.Inner.ProjectAllAsync(specification, projection, options, cancellationToken).AnyContext();
    }

    public async Task<IEnumerable<TProjection>> ProjectAllAsync<TProjection>(
        IEnumerable<ISpecification<TEntity>> specifications,
        Expression<Func<TEntity, TProjection>> projection,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        if (projection is not null)
        {
            this.Logger.LogDebug("{LogKey} repository projection: {projection}", Constants.LogKey, projection);
        }

        return await this.Inner.ProjectAllAsync(specifications, projection, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        object id,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOneId(this.Logger, Constants.LogKey, this.type, id);
        this.LogOptions(options);

        return await this.Inner.FindOneAsync(id, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        ISpecification<TEntity> specification,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);
        this.Logger.LogDebug("{LogKey} repository: specification={Specification}",
            Constants.LogKey,
            specification.GetType().PrettyName());

        return await this.Inner.FindOneAsync(specification, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> FindOneAsync(
        IEnumerable<ISpecification<TEntity>> specifications,
        IFindOptions<TEntity> options = null,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.Logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        foreach (var specification in specifications.SafeNull())
        {
            this.Logger.LogDebug("{LogKey} repository: specification={Specification}",
                Constants.LogKey,
                specification.GetType().PrettyName());
        }

        return await this.Inner.FindOneAsync(specifications, options, cancellationToken).AnyContext();
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogInsert(this.Logger, Constants.LogKey, this.type, entity?.Id);

        return await this.Inner.InsertAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpdate(this.Logger, Constants.LogKey, this.type, entity?.Id);

        return await this.Inner.UpdateAsync(entity, cancellationToken).AnyContext();
    }

    public async Task<(TEntity entity, RepositoryActionResult action)> UpsertAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        TypedLogger.LogUpsert(this.Logger, Constants.LogKey, this.type, entity?.Id);

        return await this.Inner.UpsertAsync(entity, cancellationToken).AnyContext();
    }

    private void LogOptions(IFindOptions<TEntity> options)
    {
        if (options?.Distinct?.Expression is not null)
        {
            this.Logger.LogDebug("{LogKey} repository: distinct={DistinctExpression}",
                Constants.LogKey,
                options.Distinct.Expression);
        }

        foreach (var order in
                 (options?.Orders.EmptyToNull() ?? new List<OrderOption<TEntity>>()).Insert(options?.Order))
        {
            if (order.Expression != null)
            {
                this.Logger.LogDebug("{LogKey} repository: order={OrderExpression} [{OrderDirection}]",
                    Constants.LogKey,
                    order.Expression,
                    order.Direction);
            }

            if (order.Ordering != null)
            {
                this.Logger.LogDebug("{LogKey} repository: order={Ordering}",
                    Constants.LogKey,
                    order.Ordering); // includes direction
            }
        }

        foreach (var include in (options?.Includes.EmptyToNull() ?? new List<IncludeOption<TEntity>>()).Insert(
                     options?.Include))
        {
            if (include.Expression is not null)
            {
                this.Logger.LogDebug("{LogKey} repository: include={IncludeExpression}",
                    Constants.LogKey,
                    include.Expression);
            }

            if (include.Path is not null)
            {
                this.Logger.LogDebug("{LogKey} repository: include={IncludePath}", Constants.LogKey, include.Path);
            }
        }

        this.Logger.LogDebug("{LogKey} repository: notracking={NoTracking}",
            Constants.LogKey,
            options is not null && options.NoTracking);

        if (options?.Skip.HasValue == true && options?.Take.HasValue == true)
        {
            this.Logger.LogDebug("{LogKey} repository: skip={Skip}, take={Take}",
                Constants.LogKey,
                options.Skip.Value,
                options.Take.Value);
        }
        else if (options?.Skip.HasValue == true && options?.Take.HasValue == false)
        {
            this.Logger.LogDebug("{LogKey} repository: skip={Skip}", Constants.LogKey, options.Skip.Value);
        }
        else if (options?.Skip.HasValue == false && options?.Take.HasValue == true)
        {
            this.Logger.LogDebug("{LogKey} repository: take={Take}", Constants.LogKey, options.Take.Value);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} repository: count (type={EntityType})")]
        public static partial void LogCount(ILogger logger, string logKey, string entityType);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} repository: delete (type={EntityType}, id={EntityId})")]
        public static partial void LogDelete(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} repository: exists (type={EntityType}, id={EntityId})")]
        public static partial void LogExists(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} repository: findall (type={EntityType})")]
        public static partial void LogFindAll(ILogger logger, string logKey, string entityType);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} repository: projectall (type={EntityType})")]
        public static partial void LogProjectAll(ILogger logger, string logKey, string entityType);

        [LoggerMessage(5, LogLevel.Information, "{LogKey} repository: findone (type={EntityType}, id={EntityId})")]
        public static partial void LogFindOneId(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(6, LogLevel.Information, "{LogKey} repository: findone (type={EntityType})")]
        public static partial void LogFindOne(ILogger logger, string logKey, string entityType);

        [LoggerMessage(7, LogLevel.Information, "{LogKey} repository: insert (type={EntityType}, id={EntityId})")]
        public static partial void LogInsert(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(8, LogLevel.Information, "{LogKey} repository: update (type={EntityType}, id={EntityId})")]
        public static partial void LogUpdate(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(9, LogLevel.Information, "{LogKey} repository: upsert (type={EntityType}, id={EntityId})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string entityType, object entityId);
    }
}