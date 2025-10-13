// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using global::BridgingIT.DevKit.Common;
using global::BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
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
            var existsResult = await this.ExistsInternalAsync(
                context,
                sequenceName,
                schema,
                cancellationToken);

            if (existsResult.IsFailure)
            {
                return Result<long>.Failure()
                    .WithErrors(existsResult.Errors);
            }

            if (!existsResult.Value)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(
                        sequenceName,
                        schema ?? "dbo"));
            }

            var qualifiedName = string.IsNullOrWhiteSpace(schema)
                ? $"[{sequenceName}]"
                : $"[{schema}].[{sequenceName}]";

            var nextValue = await context.Database
                .SqlQuery<long>($"SELECT NEXT VALUE FOR {qualifiedName}")
                .FirstAsync(cancellationToken);

            return Result<long>.Success(nextValue);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to get next value from sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);

            return Result<long>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to get next sequence value: {ex.Message}");
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

            var exists = await context.Database
                .SqlQuery<int>(
                    $@"SELECT COUNT(*)
                       FROM sys.sequences s
                       INNER JOIN sys.schemas sc ON s.schema_id = sc.schema_id
                       WHERE s.name = {sequenceName}
                       AND sc.name = {schemaName}")
                .FirstAsync(cancellationToken) > 0;

            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to check existence of sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);

            return Result<bool>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to check sequence existence: {ex.Message}");
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

            var info = await context.Database
                .SqlQuery<SequenceInfo>(
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
                       WHERE s.name = {sequenceName}
                       AND sc.name = {schemaName}")
                .FirstOrDefaultAsync(cancellationToken);

            if (info == null)
            {
                return Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schemaName));
            }

            return Result<SequenceInfo>.Success(info);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to get sequence info for {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);

            return Result<SequenceInfo>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to get sequence info: {ex.Message}");
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
            var currentValue = await context.Database
                .SqlQuery<long>(
                    $@"SELECT CAST(current_value AS BIGINT)
                       FROM sys.sequences s
                       INNER JOIN sys.schemas sc ON s.schema_id = sc.schema_id
                       WHERE s.name = {sequenceName}
                       AND sc.name = {schemaName}")
                .FirstOrDefaultAsync(cancellationToken);

            if (currentValue == 0)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schemaName));
            }

            return Result<long>.Success(currentValue);
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to get current value from sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);

            return Result<long>.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to get current sequence value: {ex.Message}");
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

            await context.Database.ExecuteSqlAsync(
                $@"ALTER SEQUENCE [{schemaName}].[{sequenceName}] RESTART WITH {startValue}", cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Failed to reset sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);

            return Result.Failure()
                .WithError(new ExceptionError(ex))
                .WithMessage($"Failed to reset sequence: {ex.Message}");
        }
    }
}