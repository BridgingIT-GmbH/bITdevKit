// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;

/// <summary>
/// SQL Server implementation of sequence number generator with thread-safe operations.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public class SqlServerSequenceNumberGenerator<TContext>(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    SequenceNumberGeneratorOptions options = null)
    : SequenceNumberGeneratorBase<TContext>(loggerFactory, serviceProvider, options)
    where TContext : DbContext
{
    protected override async Task<Result<long>> GetNextInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            var existsResult = await this.ExistsInternalAsync(context, sequenceName, schema, cancellationToken);
            if (existsResult.IsFailure)
            {
                return Result<long>.Failure()
                    .WithErrors(existsResult.Errors);
            }

            if (!existsResult.Value)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "dbo"));
            }

            var qualifiedName = $"[{schema ?? "dbo"}].[{sequenceName}]";
            var outputParam = new SqlParameter
            {
                ParameterName = "@NextValue",
                SqlDbType = SqlDbType.BigInt,
                Direction = ParameterDirection.Output
            };

            var schemaName = schema ?? "dbo";
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync(
                $"SET @NextValue = NEXT VALUE FOR [{schemaName}].[{sequenceName}]", outputParam);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            return Result<long>.Success((long)outputParam.Value);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "Failed to get next value from sequence {Sequence} (context={Context}): {ErrorMessage}", sequenceName, this.contextTypeName, ex.Message);

            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    protected override async Task<Result<bool>> ExistsInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            var schemaName = schema ?? "dbo";

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var exists = await context.Database
                .SqlQueryRaw<int>(
                    $@"SELECT COUNT(*) AS Value
                       FROM sys.sequences s
                       INNER JOIN sys.schemas sc ON s.schema_id = sc.schema_id
                       WHERE s.name = '{sequenceName}'
                       AND sc.name = '{schemaName}'")
                .FirstAsync(cancellationToken) > 0;
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "Failed to check existence of sequence {Sequence} (context={Context}): {ErrorMessage}", sequenceName, this.contextTypeName, ex.Message);

            return Result<bool>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    protected override async Task<Result<SequenceInfo>> GetSequenceInfoInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            var schemaName = schema ?? "dbo";
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var info = await context.Database
                .SqlQueryRaw<SequenceInfo>(
                    $@"SELECT 
                        s.name AS Name,
                        sc.name AS [Schema],
                        CAST(s.current_value AS BIGINT) AS CurrentValue,
                        CAST(s.minimum_value AS BIGINT) AS MinValue,
                        CAST(s.maximum_value AS BIGINT) AS MaxValue,
                        CAST(s.increment AS INT) AS Increment,
                        s.is_cycling AS IsCyclic
                       FROM sys.sequences s
                       INNER JOIN sys.schemas sc ON s.schema_id = sc.schema_id
                       WHERE s.name = '{sequenceName}'
                       AND sc.name = '{schemaName}'")
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            if (info == null)
            {
                return Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schemaName));
            }

            return Result<SequenceInfo>.Success(info);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "Failed to get sequence info for {Sequence} (context={Context}): {ErrorMessage}", sequenceName, this.contextTypeName, ex.Message);

            return Result<SequenceInfo>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    protected override async Task<Result<long>> GetCurrentValueInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            var schemaName = schema ?? "dbo";
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var currentValue = await context.Database.SqlQueryRaw<long>(
                    $@"SELECT CAST(current_value AS BIGINT) AS Value
                       FROM sys.sequences s
                       INNER JOIN sys.schemas sc ON s.schema_id = sc.schema_id
                       WHERE s.name = '{sequenceName}'
                       AND sc.name = '{schemaName}'")
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            if (currentValue == 0)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schemaName));
            }

            return Result<long>.Success(currentValue);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "Failed to get current value from sequence {Sequence} (context={Context}): {ErrorMessage}", sequenceName, this.contextTypeName, ex.Message);

            return Result<long>.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }

    protected override async Task<Result> ResetSequenceInternalAsync(
        TContext context,
        string sequenceName,
        long startValue,
        string schema,
        CancellationToken cancellationToken)
    {
        try
        {
            var schemaName = schema ?? "dbo";
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync($"ALTER SEQUENCE [{schemaName}].[{sequenceName}] RESTART WITH {startValue}", cancellationToken: cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            return Result.Success();
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(ex, "Failed to reset sequence {Sequence} (context={Context}): {ErrorMessage}", sequenceName, this.contextTypeName, ex.Message);

            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }
}