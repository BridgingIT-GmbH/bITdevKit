// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A pipeline behavior that manages database transactions for handlers
/// and executes the whole unit of work via EF Core's execution strategy
/// (no-op if retries are not enabled).
/// </summary>
public class DatabaseTransactionPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IServiceProvider serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType?.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>() != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        var attribute = handlerType.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>();
        if (attribute == null || attribute.DbContextType == null)
        {
            return await next();
        }

        var dbContextType = attribute.DbContextType;
        if (!typeof(DbContext).IsAssignableFrom(dbContextType))
        {
            this.Logger.LogError("{LogKey} transaction pipeline behavior failed: Type {DbContextType} is not a DbContext (type={BehaviorType})", LogKey, dbContextType.Name, this.GetType().Name);
            throw new InvalidOperationException($"Type {dbContextType.FullName} is not a DbContext.");
        }

        if (this.serviceProvider.GetService(dbContextType) is not DbContext dbContext)
        {
            this.Logger.LogError("{LogKey} transaction pipeline behavior failed: DbContext type {DbContextType} is not registered (type={BehaviorType})", LogKey, dbContextType.Name, this.GetType().Name);
            throw new InvalidOperationException($"DbContext type {dbContextType.FullName} is not registered in the service provider.");
        }

        var requestId = request is IRequest req ? req.RequestId.ToString() : string.Empty;
        var isolationLevel = this.MapIsolationLevel(attribute.IsolationLevel);

        // Obtain the provider’s execution strategy (no-op if retries are not enabled).
        var strategy = dbContext.Database.CreateExecutionStrategy();

        // Execute the entire transactional unit within the strategy.
        return await strategy.ExecuteAsync(async () =>
        {
            this.Logger.LogInformation("{LogKey} transaction pipeline behavior starting (context={DbContextType}/{DbContextId}, isolationLevel={IsolationLevel}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, isolationLevel, requestId, this.GetType().Name);

            // If a transaction already exists, avoid nesting unless you explicitly want savepoints.
            if (dbContext.Database.CurrentTransaction is not null)
            {
                // Reuse existing transaction scope.
                var resultNoNewTx = await next(); // execute next handler in pipeline

                this.Logger.LogInformation("{LogKey} transaction pipeline behavior reused existing transaction (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name);
                return resultNoNewTx;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
            try
            {
                var result = await next(); // execute next handler in pipeline

                await transaction.CommitAsync(cancellationToken);
                this.Logger.LogInformation("{LogKey} transaction pipeline behavior committed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name);

                return result;
            }
            catch (Exception ex)
            {
                if (attribute.RollbackOnFailure)
                {
                    try
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        this.Logger.LogWarning(
                            "{LogKey} transaction pipeline behavior rolled back due to exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, ex.Message);
                    }
                    catch (Exception rbEx)
                    {
                        this.Logger.LogError(
                            rbEx,
                            "{LogKey} rollback failed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType})",
                            LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name);
                    }
                }
                else
                {
                    this.Logger.LogWarning(
                        "{LogKey} transaction pipeline behavior did not rollback (RollbackOnFailure=false) despite exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}",
                        LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, ex.Message);
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