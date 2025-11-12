// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Domain;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public partial class ActiveEntityLoggingBehavior<TEntity>(ILoggerFactory loggerFactory) : ActiveEntityBehaviorBase<TEntity>
    where TEntity : class, IEntity
{
    private readonly ILogger<ActiveEntityLoggingBehavior<TEntity>> logger = loggerFactory?.CreateLogger<ActiveEntityLoggingBehavior<TEntity>>() ??
        NullLoggerFactory.Instance.CreateLogger<ActiveEntityLoggingBehavior<TEntity>>();
    private readonly string type = typeof(TEntity).Name;

    public override Task<Result> BeforeInsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity != null)
        {
            TypedLogger.LogInsert(this.logger, Constants.LogKey, this.type, entity.Id);
        }

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeUpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity != null)
        {
            TypedLogger.LogUpdate(this.logger, Constants.LogKey, this.type, entity.Id);
        }

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeUpsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity != null)
        {
            TypedLogger.LogUpsert(this.logger, Constants.LogKey, this.type, entity.Id);
        }

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity != null)
        {
            TypedLogger.LogDelete(this.logger, Constants.LogKey, this.type, entity.Id);
        }

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeDeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogDelete(this.logger, Constants.LogKey, this.type, id);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindOneAsync(object id, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOneId(this.logger, Constants.LogKey, this.type, id);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindOneAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindOneAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.logger, Constants.LogKey, this.type);
        foreach (var specification in specifications.SafeNull())
        {
            this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindOneAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindOne(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAll(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllPaged(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeProjectAllAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.logger, Constants.LogKey, this.type);
        if (projection != null)
        {
            this.logger.LogDebug("{LogKey} active entity: projection {Projection}", Constants.LogKey, projection);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeProjectAllAsync<TProjection>(ISpecification<TEntity> specification, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        if (projection != null)
        {
            this.logger.LogDebug("{LogKey} active entity: projection {Projection}", Constants.LogKey, projection);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeProjectAllAsync<TProjection>(IEnumerable<ISpecification<TEntity>> specifications, Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAll(this.logger, Constants.LogKey, this.type);
        foreach (var specification in specifications.SafeNull())
        {
            this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        }
        if (projection != null)
        {
            this.logger.LogDebug("{LogKey} active entity: projection {Projection}", Constants.LogKey, projection);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeProjectAllPagedAsync<TProjection>(Expression<Func<TEntity, TProjection>> projection, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogProjectAllPaged(this.logger, Constants.LogKey, this.type);
        if (projection != null)
        {
            this.logger.LogDebug("{LogKey} active entity: projection {Projection}", Constants.LogKey, projection);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeExistsAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.logger, Constants.LogKey, this.type);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeExistsAsync(object id, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExistsId(this.logger, Constants.LogKey, this.type, id);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeExistsAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeExistsAsync(IEnumerable<ISpecification<TEntity>> specifications, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.logger, Constants.LogKey, this.type);
        foreach (var specification in specifications.SafeNull())
        {
            this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        }

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeExistsAsync(FilterModel filter, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogExists(this.logger, Constants.LogKey, this.type);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeCountAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCount(this.logger, Constants.LogKey, this.type);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeCountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogCount(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIds(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIds(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIds(this.logger, Constants.LogKey, this.type);
        foreach (var specification in specifications.SafeNull())
        {
            this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIds(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsPagedAsync(IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIdsPaged(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsPagedAsync(ISpecification<TEntity> specification, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIdsPaged(this.logger, Constants.LogKey, this.type);
        this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsPagedAsync(IEnumerable<ISpecification<TEntity>> specifications, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIdsPaged(this.logger, Constants.LogKey, this.type);
        foreach (var specification in specifications.SafeNull())
        {
            this.logger.LogDebug("{LogKey} active entity specification: {Specification}", Constants.LogKey, specification);
        }
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeFindAllIdsPagedAsync(FilterModel filter, IFindOptions<TEntity> options, CancellationToken cancellationToken = default)
    {
        TypedLogger.LogFindAllIdsPaged(this.logger, Constants.LogKey, this.type);
        this.LogOptions(options);

        return Task.FromResult(Result.Success());
    }

    public override Task<Result> BeforeTransactionAsync(CancellationToken cancellationToken = default)
    {
        TypedLogger.LogTransaction(this.logger, Constants.LogKey, this.type);

        return Task.FromResult(Result.Success());
    }

    private void LogOptions(IFindOptions<TEntity> options)
    {
        if (options?.Distinct?.Expression != null)
        {
            this.logger.LogDebug("{LogKey} active entity: distinct {DistinctExpression}", Constants.LogKey, options.Distinct.Expression);
        }

        foreach (var order in (options?.Orders.EmptyToNull() ?? new List<OrderOption<TEntity>>()).Insert(options?.Order))
        {
            this.logger.LogDebug("{LogKey} active entity: order {OrderExpression}", Constants.LogKey, order.Expression);
        }

        foreach (var include in (options?.Includes.EmptyToNull() ?? new List<IncludeOption<TEntity>>()).Insert(options?.Include))
        {
            if (include.Expression != null)
            {
                this.logger.LogDebug("{LogKey} active entity: include {IncludeExpression}", Constants.LogKey, include.Expression);
            }

            if (include.Path != null)
            {
                this.logger.LogDebug("{LogKey} active entity: include {IncludePath}", Constants.LogKey, include.Path);
            }
        }

        if (options?.Skip.HasValue == true)
        {
            this.logger.LogDebug("{LogKey} active entity: skip {Skip}", Constants.LogKey, options.Skip.Value);
        }

        if (options?.Take.HasValue == true)
        {
            this.logger.LogDebug("{LogKey} active entity: take {Take}", Constants.LogKey, options.Take.Value);
        }
    }

    public static partial class TypedLogger
    {
        [LoggerMessage(0, LogLevel.Information, "{LogKey} active entity: count (type={EntityType})")]
        public static partial void LogCount(ILogger logger, string logKey, string entityType);

        [LoggerMessage(1, LogLevel.Information, "{LogKey} active entity: exists (type={EntityType})")]
        public static partial void LogExists(ILogger logger, string logKey, string entityType);

        [LoggerMessage(2, LogLevel.Information, "{LogKey} active entity: exists (type={EntityType}, id={EntityId})")]
        public static partial void LogExistsId(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(3, LogLevel.Information, "{LogKey} active entity: findall (type={EntityType})")]
        public static partial void LogFindAll(ILogger logger, string logKey, string entityType);

        [LoggerMessage(4, LogLevel.Information, "{LogKey} active entity: findall paged (type={EntityType})")]
        public static partial void LogFindAllPaged(ILogger logger, string logKey, string entityType);

        [LoggerMessage(5, LogLevel.Information, "{LogKey} active entity: projectall (type={EntityType})")]
        public static partial void LogProjectAll(ILogger logger, string logKey, string entityType);

        [LoggerMessage(6, LogLevel.Information, "{LogKey} active entity: projectall paged (type={EntityType})")]
        public static partial void LogProjectAllPaged(ILogger logger, string logKey, string entityType);

        [LoggerMessage(7, LogLevel.Information, "{LogKey} active entity: findone (type={EntityType}, id={EntityId})")]
        public static partial void LogFindOneId(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(8, LogLevel.Information, "{LogKey} active entity: findone (type={EntityType})")]
        public static partial void LogFindOne(ILogger logger, string logKey, string entityType);

        [LoggerMessage(9, LogLevel.Information, "{LogKey} active entity: insert (type={EntityType}, id={EntityId})")]
        public static partial void LogInsert(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(10, LogLevel.Information, "{LogKey} active entity: update (type={EntityType}, id={EntityId})")]
        public static partial void LogUpdate(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(11, LogLevel.Information, "{LogKey} active entity: upsert (type={EntityType}, id={EntityId})")]
        public static partial void LogUpsert(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(12, LogLevel.Information, "{LogKey} active entity: delete (type={EntityType}, id={EntityId})")]
        public static partial void LogDelete(ILogger logger, string logKey, string entityType, object entityId);

        [LoggerMessage(13, LogLevel.Information, "{LogKey} active entity: findall ids (type={EntityType})")]
        public static partial void LogFindAllIds(ILogger logger, string logKey, string entityType);

        [LoggerMessage(14, LogLevel.Information, "{LogKey} active entity: findall ids paged (type={EntityType})")]
        public static partial void LogFindAllIdsPaged(ILogger logger, string logKey, string entityType);

        [LoggerMessage(15, LogLevel.Information, "{LogKey} active entity: transaction (type={EntityType})")]
        public static partial void LogTransaction(ILogger logger, string logKey, string entityType);
    }
}
