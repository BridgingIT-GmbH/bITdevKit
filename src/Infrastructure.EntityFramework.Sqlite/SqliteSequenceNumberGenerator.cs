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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    private static readonly Regex IdentifierPattern = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    private static void ValidateIdentifier(string name, string type)
    {
        if (string.IsNullOrWhiteSpace(name) || !IdentifierPattern.IsMatch(name))
        {
            throw new ArgumentException($"{type} '{name}' contains invalid characters. Only alphanumeric and underscores are allowed.");
        }
    }

    protected override async Task<Result<long>> GetNextInternalAsync(
        TContext context,
        string sequenceName,
        string schema,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(sequenceName, "Sequence name");
        // Schema is ignored in SQLite, but validate if provided for consistency
        if (!string.IsNullOrWhiteSpace(schema))
        {
            ValidateIdentifier(schema, "Schema name");
        }

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
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default"));
            }

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync(
                $"UPDATE sqlite_sequence SET seq = seq + 1 WHERE name = {sequenceName}", cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var nextValue = await context.Database
                .SqlQueryRaw<long>($"SELECT seq FROM sqlite_sequence WHERE name = {sequenceName}")
                .FirstAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            return Result<long>.Success(nextValue);
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
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var exists = await context.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS Value FROM sqlite_sequence WHERE name = '{sequenceName}'")
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
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var currentValue = await context.Database
                .SqlQueryRaw<long>($"SELECT seq FROM sqlite_sequence WHERE name = '{sequenceName}'")
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            if (currentValue == 0)
            {
                return Result<SequenceInfo>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default"));
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
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var currentValue = await context.Database
                .SqlQueryRaw<long>($"SELECT seq FROM sqlite_sequence WHERE name = '{sequenceName}")
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

            if (currentValue == 0)
            {
                return Result<long>.Failure()
                    .WithError(new SequenceNotFoundError(sequenceName, schema ?? "default"));
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
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync(
                $"UPDATE sqlite_sequence SET seq = {startValue} WHERE name = '{sequenceName}'", cancellationToken);
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