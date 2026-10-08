// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Profiling;

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal sealed class EntityFrameworkProfilingUnitOfWork<TContext>(IServiceScopeFactory scopes,
    EntityFrameworkProfilingCoordination<TContext> coordination)
    where TContext : DbContext, IProfilingDbContext
{
    internal async Task<IResult<T>> WriteAsync<T>(Func<TContext, EntityFrameworkProfilingCoordination<TContext>.Frame, ProfilingRuntimeGateEntity, DateTimeOffset, CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken, bool runtimeGate = false)
    {
        // One fresh context per attempt. Only a known pre-commit rollback may retry once;
        // a failed commit acknowledgement is returned as uncertain and is never replayed here.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();
            var commitStarted = false;
            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite" ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
                var gate = runtimeGate ? await EntityFrameworkProfilingRuntimeGate.AcquireAsync(context, cancellationToken).ConfigureAwait(false) : null;
                var frame = await coordination.AcquireAsync(context, cancellationToken).ConfigureAwait(false);
                var utc = await ProviderUtcAsync(context, cancellationToken).ConfigureAwait(false);
                var result = await action(context, frame, gate, utc, cancellationToken).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    await EntityFrameworkProfilingCoordination<TContext>.SaveAsync(context, frame, cancellationToken).ConfigureAwait(false);
                    commitStarted = true;
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
            catch (Exception exception) when (!commitStarted && attempt == 0 && DatabaseFailure(exception))
            {
                // Disposal has rolled back before the next fresh attempt starts.
            }
            catch (Exception exception) when (DatabaseFailure(exception))
            {
                return Result<T>.Failure(new ProfilingPersistenceError(commitStarted, true));
            }
            catch (OperationCanceledException) when (commitStarted)
            {
                // Cancellation during commit does not establish whether the database committed.
                return Result<T>.Failure(new ProfilingPersistenceError(true, true));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or OverflowException)
            {
                return Result<T>.Failure(new ProfilingPersistenceError(false, false));
            }
        }

        return Result<T>.Failure(new ProfilingPersistenceError(false, true));
    }

    internal async Task<IResult<T>> ReadAsync<T>(Func<TContext, CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        try
        {
            return await action(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (DatabaseFailure(exception))
        {
            return Result<T>.Failure(new ProfilingPersistenceError(false, true));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or OverflowException)
        {
            return Result<T>.Failure(new ProfilingPersistenceError(false, false));
        }
    }

    private static bool DatabaseFailure(Exception exception) => exception is DbException or DbUpdateException
        || exception is InvalidOperationException && exception.InnerException is not null && DatabaseFailure(exception.InnerException);

    internal static async Task<DateTimeOffset> ProviderUtcAsync(TContext context, CancellationToken cancellationToken)
    {
        var provider = context.Database.ProviderName;
        if (provider == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var text = await context.Database.SqlQueryRaw<string>("SELECT strftime('%Y-%m-%dT%H:%M:%fZ', 'now') AS \"Value\"")
                .SingleAsync(cancellationToken).ConfigureAwait(false);
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        var statement = provider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "SELECT SYSUTCDATETIME() AS [Value]",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => "SELECT clock_timestamp() AS \"Value\"",
            _ => throw new ArgumentException("The configured profiling database engine does not support provider-authoritative UTC."),
        };
        var utc = await context.Database.SqlQueryRaw<DateTime>(statement).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }
}
