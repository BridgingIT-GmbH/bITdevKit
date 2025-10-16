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
        ValidateIdentifier(schema ?? "public", "Schema name");

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
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var nextValue = await context.Database
                .SqlQueryRaw<long>(
                    $"SELECT nextval('\"{schemaName}\".\"{sequenceName}\"') AS \"Value\"")
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
            var schemaName = schema ?? "public";

            var exists = await context.Database
                .SqlQueryRaw<int>(
                    $@"SELECT COUNT(*) AS ""Value""
                       FROM information_schema.sequences
                       WHERE sequence_name = {{0}}
                       AND sequence_schema = {{1}}",
                    sequenceName,
                    schemaName)
                .FirstAsync(cancellationToken) > 0;

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
            var schemaName = schema ?? "public";
            var info = await context.Database
                .SqlQueryRaw<SequenceInfo>(
                    $@"SELECT 
                        sequencename AS ""Name"",
                        schemaname AS ""Schema"",
                        CAST(last_value AS BIGINT) AS ""CurrentValue"",
                        CAST(min_value AS BIGINT) AS ""MinValue"",
                        CAST(max_value AS BIGINT) AS ""MaxValue"",
                        CAST(increment_by AS INT) AS ""Increment"",
                        cycle AS ""IsCyclic""
                       FROM pg_sequences
                       WHERE sequencename = {{0}}
                       AND schemaname = {{1}}",
                    sequenceName,
                    schemaName)
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
            var schemaName = schema ?? "public";

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            var currentValue = await context.Database
                .SqlQueryRaw<long>(
                    $@"SELECT last_value AS ""Value""
                       FROM ""{schemaName}"".""{sequenceName}""")
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.

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
        ValidateIdentifier(sequenceName, "Sequence name");
        ValidateIdentifier(schema ?? "public", "Schema name");

        try
        {
            var schemaName = schema ?? "public";

#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER SEQUENCE \"{schemaName}\".\"{sequenceName}\" RESTART WITH {startValue}",
                cancellationToken);
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
