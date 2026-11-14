// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Infrastructure.EntityFramework.Caching;

using System.Data.Common;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>Owns a native cache transaction and preserves uncertainty when an attempted commit cannot be confirmed.</summary>
/// <param name="transaction">The transaction holding the bounded cache-store authority.</param>
/// <param name="logger">The optional cleanup logger.</param>
/// <param name="provider">The registered cache provider name.</param>
/// <example><code>await using var transaction = new CacheTransaction(nativeTransaction, logger, "database"); await transaction.CommitAsync(token);</code></example>
public sealed class CacheTransaction(IDbContextTransaction transaction, ILogger logger, string provider) : IAsyncDisposable
{
    private bool commitStarted;

    /// <summary>Commits once, reporting an indeterminate result if native acknowledgement is lost.</summary>
    /// <param name="cancellationToken">The cancellation token for the commit attempt.</param>
    /// <exception cref="CacheRelationalException">The commit may have succeeded and requires operation-identity reconciliation.</exception>
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.commitStarted = true;
        try { await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException)
        {
            throw new CacheRelationalException(CacheErrorCode.Indeterminate, "The native transaction may have committed; reconcile its stable operation identity.");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try { await transaction.DisposeAsync().ConfigureAwait(false); }
        catch when (this.commitStarted)
        {
            CacheRelationalLog.Deferred(logger, provider, "transaction-cleanup");
        }
    }
}