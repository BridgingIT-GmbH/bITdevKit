// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

/// <summary>
/// PostgreSQL implementation of sequence number generator with thread-safe operations.
/// This implementation uses native PostgreSQL sequences.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public class PostgresSequenceNumberGenerator<TContext>(
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
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "public"));
            }

            var schemaName = schema ?? "public";
            var nextValue = await context.Database
                .SqlQuery<long>(
                    $"SELECT nextval('\"{schemaName}\".\"{sequenceName}\"')")
                .FirstAsync(cancellationToken);

            return Result<long>.Success(nextValue);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(
                ex,
                "Failed to get next value from sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);
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
            var schemaName = schema ?? "public";
            var exists = await context.Database
                .SqlQuery<int>(
                    $@"SELECT COUNT(*)
                       FROM information_schema.sequences
                       WHERE sequence_name = {sequenceName}
                       AND sequence_schema = {schemaName}")
                .FirstAsync(cancellationToken) > 0;

            return Result<bool>.Success(exists);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(
                ex,
                "Failed to check existence of sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);
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
            var schemaName = schema ?? "public";
            var info = await context.Database
                .SqlQuery<SequenceInfo>(
                    $@"SELECT 
                        sequence_name AS Name,
                        sequence_schema AS Schema,
                        CAST(last_value AS BIGINT) AS CurrentValue,
                        CAST(minimum_value AS BIGINT) AS MinValue,
                        CAST(maximum_value AS BIGINT) AS MaxValue,
                        CAST(increment_by AS INT) AS Increment,
                        (CASE WHEN cycle_option = 'YES' THEN 1 ELSE 0 END) AS IsCyclic
                       FROM information_schema.sequences
                       WHERE sequence_name = {sequenceName}
                       AND sequence_schema = {schemaName}")
                .FirstOrDefaultAsync(cancellationToken);

            if (info == null)
            {
                return Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schemaName));
            }

            return Result<SequenceInfo>.Success(info);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(
                ex,
                "Failed to get sequence info for {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);
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
            var schemaName = schema ?? "public";
            var currentValue = await context.Database
                .SqlQuery<long>(
                    $@"SELECT last_value FROM ""{schemaName}"".""{sequenceName}""")
                .FirstOrDefaultAsync(cancellationToken);

            return Result<long>.Success(currentValue);
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(
                ex,
                "Failed to get current value from sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);
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
            var schemaName = schema ?? "public";
            await context.Database.ExecuteSqlAsync(
                $@"ALTER SEQUENCE ""{schemaName}"".""{sequenceName}"" RESTART WITH {startValue}", cancellationToken);

            return Result.Success();
        }
        catch (Exception ex) when (!ex.IsTransientException())
        {
            this.logger.LogError(
                ex,
                "Failed to reset sequence {Sequence} " +
                "(context={Context}): {ErrorMessage}",
                sequenceName,
                this.contextTypeName,
                ex.Message);
            return Result.Failure(ex.GetFullMessage(), new ExceptionError(ex));
        }
    }
}
