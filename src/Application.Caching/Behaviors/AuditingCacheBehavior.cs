// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

/// <summary>Connects explicit mutations to optional Common auditing without retaining keys, dimensions or values.</summary>
/// <example><code>collection.WithAuditing();</code></example>
public sealed class AuditingCacheBehavior(IAuditContext audit = null) : ICacheCollectionBehavior
{
    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next, CancellationToken cancellationToken = default)
    {
        if (context.Operation is not (CacheOperationKind.Set or CacheOperationKind.SetEncoded or CacheOperationKind.Remove or CacheOperationKind.Touch or
            CacheOperationKind.Invalidate or CacheOperationKind.Clear or CacheOperationKind.Refresh))
        {
            return await next(context, cancellationToken).ConfigureAwait(false);
        }

        IAuditScope scope = null;
        try
        {
            if (audit?.IsEnabled == true)
            {
                scope = audit.BeginScope(AuditScopeOptionsBuilder.Create("cache:" + context.Collection + ":" + context.Operation).Kind("Cache").Build());
            }
        }
        catch { /* Audit admission failure cannot prevent a cache mutation. */ }

        try
        {
            var result = await next(context, cancellationToken).ConfigureAwait(false);
            try { scope?.Complete(result.IsSuccess ? AuditOutcome.Succeeded : AuditOutcome.Rejected); }
            catch { /* Preserve the actual mutation outcome. */ }

            return result;
        }
        catch (Exception exception)
        {
            try { scope?.Fail(exception); }
            catch { /* Preserve the actual mutation exception. */ }

            throw;
        }
        finally
        {
            try { if (scope is not null) { await scope.DisposeAsync().ConfigureAwait(false); } }
            catch { /* Preserve the actual mutation outcome. */ }
        }
    }
}