// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A pipeline behavior that manages database transactions for handlers
/// </summary>
public class DatabaseTransactionPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

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

        // Resolve the DbContext dynamically
        var dbContextType = attribute.DbContextType;
        if (this.serviceProvider.GetService(dbContextType) is not DbContext dbContext)
        {
            this.Logger.LogError("{LogKey} transaction pipeline behavior failed: DbContext type {DbContextType} is not registered in the service provider (type={BehaviorType})", LogKey, dbContextType.Name, this.GetType().Name);
            throw new InvalidOperationException($"DbContext type {dbContextType.FullName} is not registered in the service provider.");
        }

        var requestId = request is IRequest req ? req.RequestId.ToString() : string.Empty;
        var isolationLevel = this.MapIsolationLevel(attribute.IsolationLevel);

        this.Logger.LogInformation("{LogKey} transaction pipeline behavior starting (context={DbContextType}/{DbContextId}, isolationLevel={IsolationLevel}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, isolationLevel, requestId, this.GetType().Name);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            var result = await next(); // Proceed to the next behavior/handler

            await transaction.CommitAsync(cancellationToken);
            this.Logger.LogInformation("{LogKey} transaction pipeline behavior committed (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType})", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name);

            return result;
        }
        catch (Exception ex)
        {
            if (attribute.RollbackOnFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                this.Logger.LogWarning("{LogKey} transaction pipeline behavior rolled back due to exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, ex.Message);
            }
            else
            {
                this.Logger.LogWarning("{LogKey} transaction pipeline behavior did not rollback (RollbackOnFailure=false) despite exception (context={DbContextType}/{DbContextId}, requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}", LogKey, dbContext.GetType().Name, dbContext.ContextId, requestId, this.GetType().Name, ex.Message);
            }

            throw; // Rethrow to let the Requester handle the exception based on HandleExceptionsAsResultError
        }
    }

    /// <summary>
    /// Maps custom isolation enum values to System.Data.IsolationLevel for EF Core transactions.
    /// </summary>
    private System.Data.IsolationLevel MapIsolationLevel(DatabaseTransactionIsolationLevel level)
    {
        return level switch
        {
            DatabaseTransactionIsolationLevel.Unspecified => System.Data.IsolationLevel.Unspecified,
            DatabaseTransactionIsolationLevel.Chaos => System.Data.IsolationLevel.Chaos,
            DatabaseTransactionIsolationLevel.ReadUncommitted => System.Data.IsolationLevel.ReadUncommitted,
            DatabaseTransactionIsolationLevel.ReadCommitted => System.Data.IsolationLevel.ReadCommitted,
            DatabaseTransactionIsolationLevel.RepeatableRead => System.Data.IsolationLevel.RepeatableRead,
            DatabaseTransactionIsolationLevel.Serializable => System.Data.IsolationLevel.Serializable,
            DatabaseTransactionIsolationLevel.Snapshot => System.Data.IsolationLevel.Snapshot,
            _ => System.Data.IsolationLevel.Unspecified
        };
    }
}
