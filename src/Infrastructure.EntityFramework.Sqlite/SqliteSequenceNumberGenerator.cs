// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework;

using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite implementation using sqlite_sequence table with thread-safe operations.
/// Note: SQLite does not support native sequences like SQL Server/PostgreSQL.
/// Instead, it uses sqlite_sequence for AUTOINCREMENT tracking. This implementation
/// emulates sequence behavior using the sqlite_sequence table. Schema is not supported
/// in SQLite, so schema parameters are ignored but kept for interface consistency.
/// </summary>
/// <typeparam name="TContext">The DbContext type.</typeparam>
public class SqliteSequenceNumberGenerator<TContext>(
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
                    .WithError(new SequenceNotFoundError(
                        sequenceName,
                        schema ?? "default"));
            }

            await context.Database.ExecuteSqlAsync(
                $@"UPDATE sqlite_sequence SET seq = seq + 1 WHERE name = {sequenceName}", cancellationToken);

            var nextValue = await context.Database
                .SqlQuery<long>(
                    $"SELECT seq FROM sqlite_sequence WHERE name = {sequenceName}")
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
            var exists = await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) FROM sqlite_sequence WHERE name = {sequenceName}")
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
            var currentValue = await context.Database
                .SqlQuery<long>($"SELECT seq FROM sqlite_sequence WHERE name = {sequenceName}")
                .FirstOrDefaultAsync(cancellationToken);

            if (currentValue == 0)
            {
                return Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(
                        sequenceName,
                        schema ?? "default"));
            }

            // SQLite doesn't store min/max/increment in sqlite_sequence
            // Return default values for interface consistency
            return Result<SequenceInfo>.Success(new SequenceInfo
            {
                Name = sequenceName,
                Schema = schema ?? "default",
                CurrentValue = currentValue,
                MinValue = 1,
                MaxValue = long.MaxValue,
                Increment = 1,
                IsCyclic = false
            });
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
            var currentValue = await context.Database
                .SqlQuery<long>($"SELECT seq FROM sqlite_sequence WHERE name = {sequenceName}")
                .FirstOrDefaultAsync(cancellationToken);

            if (currentValue == 0)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default"));
            }

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
            await context.Database.ExecuteSqlAsync(
                $"UPDATE sqlite_sequence SET seq = {startValue} WHERE name = {sequenceName}", cancellationToken);

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