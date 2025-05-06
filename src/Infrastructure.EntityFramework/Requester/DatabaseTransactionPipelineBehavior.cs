// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Application.Requester;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class DatabaseTransactionPipelineBehavior<TRequest, TResponse>(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : PipelineBehaviorBase<TRequest, TResponse>(loggerFactory)
    where TRequest : class
    where TResponse : IResult
{
    private readonly IServiceProvider serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    protected override bool CanProcess(TRequest request, Type handlerType)
    {
        return handlerType != null && handlerType.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>() != null;
    }

    protected override async Task<TResponse> Process(
        TRequest request,
        Type handlerType,
        Func<Task<TResponse>> next,
        CancellationToken cancellationToken)
    {
        var attribute = handlerType.GetCustomAttribute<HandlerDatabaseTransactionAttribute<DbContext>>();
        if (attribute == null)
        {
            return await next();
        }

        // Resolve the DbContext dynamically
        var dbContextType = attribute.DbContextType;
        if (this.serviceProvider.GetService(dbContextType) is not DbContext dbContext)
        {
            this.Logger.LogError("{LogKey} transaction behavior failed: DbContext type {DbContextType} is not registered in the service provider (type={BehaviorType})",
                LogKey,
                dbContextType.FullName,
                this.GetType().Name);
            throw new InvalidOperationException($"DbContext type {dbContextType.FullName} is not registered in the service provider.");
        }

        var requestId = request is IRequest req ? req.RequestId.ToString() : string.Empty;
        this.Logger.LogDebug("{LogKey} transaction behavior starting (isolationLevel={IsolationLevel}, requestId={RequestId}, type={BehaviorType})",
            LogKey,
            attribute.IsolationLevel,
            requestId,
            this.GetType().Name);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(attribute.IsolationLevel, cancellationToken);

        try
        {
            var result = await next();

            await transaction.CommitAsync(cancellationToken);
            this.Logger.LogDebug("{LogKey} transaction behavior committed (requestId={RequestId}, type={BehaviorType})",
                LogKey,
                requestId,
                this.GetType().Name);

            return result;
        }
        catch (Exception ex)
        {
            if (attribute.RollbackOnFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                this.Logger.LogWarning("{LogKey} transaction behavior rolled back due to exception (requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}",
                    LogKey,
                    requestId,
                    this.GetType().Name,
                    ex.Message);
            }
            else
            {
                this.Logger.LogWarning("{LogKey} transaction behavior did not rollback (RollbackOnFailure=false) despite exception (requestId={RequestId}, type={BehaviorType}): {ExceptionMessage}",
                    LogKey,
                    requestId,
                    this.GetType().Name,
                    ex.Message);
            }

            throw; // Rethrow to let the Requester handle the exception based on HandleExceptionsAsResultError
        }
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HandlerDatabaseTransactionAttribute<TDBcontext>(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool rollbackOnFailure = true) : Attribute
    where TDBcontext : DbContext
{
    public IsolationLevel IsolationLevel { get; } = isolationLevel;

    public bool RollbackOnFailure { get; } = rollbackOnFailure;

    public Type DbContextType { get; } = typeof(TDBcontext);
}