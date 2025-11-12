// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A pipeline behavior that manages database transactions for handlers and executes
/// the whole unit of work via EF Core's execution strategy (no-op if retries are not enabled).
/// Supports resolving the DbContext by either a configured context name or a concrete type,
/// depending on which attribute you use.
/// </summary>
public class DatabaseTransactionPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IDbContextResolver contextResolver)
    : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IDbContextResolver dbContextResolver =
        contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        // We support either attribute style:
        // - HandlerDatabaseTransactionAttribute (name-based, infra-agnostic)
        // - HandlerDatabaseTransactionAttribute<DbContext> (type-based, legacy)
        return handlerType?.GetCustomAttribute<HandlerDatabaseTransactionAttribute>() != null
            || handlerType?.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>() != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        // Prefer the name-based attribute (infra-friendly)
        var nameAttr = handlerType.GetCustomAttribute<HandlerDatabaseTransactionAttribute>();
        var typeAttr = handlerType.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>();

        if (nameAttr is null && typeAttr is null)
        {
            return await next();
        }

        // Resolve DbContext using the resolver:
        // - If name-based attribute specified: resolve by contextName
        // - Else fall back to type-based attribute: resolve by DbContext type
        DbContext dbContext;
        DatabaseTransactionIsolationLevel attrIsolationLevel;
        bool rollbackOnFailure;

        if (nameAttr is not null)
        {
            if (string.IsNullOrWhiteSpace(nameAttr.ContextName))
            {
                this.Logger.LogError("{LogKey} behavior: contextName missing on HandlerDatabaseTransactionAttribute (handler={Handler})", LogKey, handlerType.FullName);
                throw new InvalidOperationException("HandlerDatabaseTransactionAttribute.ContextName must be provided.");
            }

            dbContext = this.dbContextResolver.Resolve(nameAttr.ContextName);
            attrIsolationLevel = nameAttr.IsolationLevel;
            rollbackOnFailure = nameAttr.RollbackOnFailure;
        }
        else
        {
            // typeAttr is not null here
            var dbContextType = typeAttr!.DbContextType;
            if (dbContextType is null || !typeof(DbContext).IsAssignableFrom(dbContextType))
            {
                this.Logger.LogError("{LogKey} behavior: DbContextType invalid on HandlerDatabaseTransactionAttribute<T> (handler={Handler}, type={DbContextType})", LogKey, handlerType.FullName, dbContextType?.FullName ?? "<null>");
                throw new InvalidOperationException("DbContextType must be a concrete DbContext.");
            }

            dbContext = this.dbContextResolver.Resolve(dbContextType);
            attrIsolationLevel = typeAttr.IsolationLevel;
            rollbackOnFailure = typeAttr.RollbackOnFailure;
        }

        var requestId = request is IRequest req ? req.RequestId.ToString() : string.Empty;
        var isolationLevel = this.MapIsolationLevel(attrIsolationLevel);

        // Obtain the provider’s execution strategy (no-op if retries are not enabled).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        // Execute the entire transactional unit within the strategy.
        return await strategy.ExecuteAsync(async () =>
        {
            this.Logger.LogInformation("{LogKey} behavior: database transaction starting (context={DbContextType}/{DbContextId}, isolationLevel={IsolationLevel}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, isolationLevel, requestId, this.GetType().Name);

            // If a transaction already exists, avoid nesting
            if (dbContext.Database.CurrentTransaction is not null)
            {
                var resultExisting = await next();

                this.Logger.LogInformation(
                    "{LogKey} behavior: database transaction reused existing (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})",
                    LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, dbContext.Database.CurrentTransaction?.TransactionId);

                return resultExisting;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken); // https://learn.microsoft.com/en-us/ef/core/saving/transactions
            var trxId = dbContext.Database.CurrentTransaction?.TransactionId;

            try
            {
                var result = await next();

                if (result.IsSuccess)
                {
                    await transaction.CommitAsync(cancellationToken);
                    this.Logger.LogInformation("{LogKey} behavior: database transaction commit completed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId);
                }
                else
                {
                    if (rollbackOnFailure)
                    {
                        try
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            this.Logger.LogWarning(
                                "{LogKey} behavior: database transaction rolled back due to failure (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})",
                                LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId);
                        }
                        catch (Exception rbEx)
                        {
                            this.Logger.LogError(
                                rbEx,
                                "{LogKey} behavior: database transaction rollback failed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId}) {ExceptionMessage}",
                                LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId, rbEx.Message);
                        }
                    }
                    else
                    {
                        this.Logger.LogWarning(
                            "{LogKey} behavior: database transaction rollback not requested (RollbackOnFailure=false) despite exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(
                            "{LogKey} behavior: database transaction commit failure (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId}): {ExceptionMessage}",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId, ex.Message);

                if (rollbackOnFailure)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        this.Logger.LogWarning(
                            "{LogKey} behavior: database transaction rolled back due to failure (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId);
                    }
                    catch (Exception rbEx)
                    {
                        this.Logger.LogError(
                            rbEx,
                            "{LogKey} behavior: database transaction rollback failed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId}) {ExceptionMessage}",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId, rbEx.Message);
                    }
                }
                else
                {
                    this.Logger.LogWarning(
                        "{LogKey} behavior: database transaction rollback not requested (RollbackOnFailure=false) despite exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}, trxId={TransactionId})",
                        LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, trxId);
                }

                throw; // Let Requester decide based on HandleExceptionsAsResultError
            }
        });
    }

    /// <summary>
    /// Maps custom isolation enum values to System.Data.IsolationLevel for EF Core transactions.
    /// </summary>
    private IsolationLevel MapIsolationLevel(DatabaseTransactionIsolationLevel level)
    {
        return level switch
        {
            DatabaseTransactionIsolationLevel.Unspecified => IsolationLevel.Unspecified,
            DatabaseTransactionIsolationLevel.Chaos => IsolationLevel.Chaos,
            DatabaseTransactionIsolationLevel.ReadUncommitted => IsolationLevel.ReadUncommitted,
            DatabaseTransactionIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
            DatabaseTransactionIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
            DatabaseTransactionIsolationLevel.Serializable => IsolationLevel.Serializable,
            DatabaseTransactionIsolationLevel.Snapshot => IsolationLevel.Snapshot,
            _ => IsolationLevel.Unspecified
        };
    }
}