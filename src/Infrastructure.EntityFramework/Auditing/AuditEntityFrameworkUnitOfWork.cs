// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Auditing;

using System.Data.Common;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Creates independent suppressed EF scopes and preserves uncertain commit outcomes explicitly.</summary>
/// <example><code>var result = await work.ExecuteAsync(operation, write: true, cancellationToken);</code></example>
public sealed class AuditEntityFrameworkUnitOfWork<TContext>(IServiceScopeFactory scopes, TimeSpan? operationTimeout = null) where TContext : DbContext, IAuditingDbContext
{
    /// <summary>Executes one finite operation under the durable publication protocol without touching a business context.</summary>
    /// <remarks>Only the audit writer decides retries of immutable append identities. This layer never hides a failed commit.</remarks>
    /// <example><code>var result = await work.ExecuteAsync((db, frame, ct) => ReadAsync(db, frame, ct), false, ct);</code></example>
    public async Task<Result<T>> ExecuteAsync<T>(Func<TContext, AuditStoreSession, CancellationToken, Task<Result<T>>> operation, bool write, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(operationTimeout ?? TimeSpan.FromSeconds(10));
        var token = timeout.Token;
        await using var scope = scopes.CreateAsyncScope();
        using var suppression = scope.ServiceProvider.GetRequiredService<IAuditContext>().Suppress();
        try
        {
            if (scope.ServiceProvider.GetService<IDatabaseReadyService>() is { } databaseReady)
                await databaseReady.WaitForReadyAsync(typeof(TContext).Name, cancellationToken: token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<T>.Failure(new AuditError("AuditStoreTimeout")); }
        catch (TimeoutException) { return Result<T>.Failure(new AuditError("AuditStoreTimeout")); }
        catch (InvalidOperationException) { return Result<T>.Failure(new AuditError("AuditStoreUnavailable")); }

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        context.Database.SetCommandTimeout((int)Math.Ceiling((operationTimeout ?? TimeSpan.FromSeconds(10)).TotalSeconds));
        // Enter the configured execution-strategy context to support host retry-enabled options.
        // All expected database errors become explicit Results inside it, so it cannot replay an uncertain commit.
        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var commitStarted = false;
            try
            {
                await using var session = await AuditStoreSession.BeginAsync(context, token).ConfigureAwait(false);
                if (session.State is null || session.State.StoreId == Guid.Empty || session.State.StoreEpoch == Guid.Empty) return Result<T>.Failure(new AuditError("AuditStoreUninitialized"));
                var result = await operation(context, session, token).ConfigureAwait(false);
                if (result.IsFailure) return result;
                if (write) await context.SaveChangesAsync(token).ConfigureAwait(false);
                commitStarted = true;
                await session.CommitAsync(token).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException) when (commitStarted) { return Result<T>.Failure(new AuditError("AuditCommitUnconfirmed")); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Result<T>.Failure(new AuditError("AuditStoreTimeout")); }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) when (error is DbException or DbUpdateException) { return Result<T>.Failure(new AuditError(commitStarted ? "AuditCommitUnconfirmed" : "AuditStoreUnavailable")); }
            catch (Exception error) when (error is ArgumentException or JsonException or OverflowException) { return Result<T>.Failure(new AuditError("AuditQueryInvalid")); }
            catch (InvalidOperationException) { return Result<T>.Failure(new AuditError("AuditStoreUnavailable")); }
        }).ConfigureAwait(false);
    }
}
