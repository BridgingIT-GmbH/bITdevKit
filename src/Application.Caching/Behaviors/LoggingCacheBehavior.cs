// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Caching;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>Logs bounded collection/provider aliases and outcomes. Keys, dimensions, payloads and raw errors are omitted.</summary>
/// <example><code>collection.WithLogging();</code></example>
public sealed class LoggingCacheBehavior(ILogger<LoggingCacheBehavior> logger = null) : ICacheCollectionBehavior, ICacheLoadBehavior, ICacheTransferObserver
{
    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheOperationContext context, CacheOperationContinuation<T> next, CancellationToken cancellationToken = default)
    {
        var enabled = this.Enabled();
        var started = enabled ? Stopwatch.GetTimestamp() : 0;
        var result = await next(context, cancellationToken).ConfigureAwait(false);
        if (enabled)
        {
            this.Record(context.Collection, context.Provider, context.Operation.ToString(), result.IsSuccess, started);
        }

        return result;
    }

    /// <inheritdoc />
    public async ValueTask<Result<T>> ExecuteAsync<T>(CacheLoadBehaviorContext context, CacheLoadContinuation<T> next, CancellationToken cancellationToken = default)
    {
        var enabled = this.Enabled();
        var started = enabled ? Stopwatch.GetTimestamp() : 0;
        var result = await next(context, cancellationToken).ConfigureAwait(false);
        if (enabled)
        {
            this.Record(context.Collection, context.Provider, "OwnedSource", result.IsSuccess, started);
        }

        return result;
    }

    /// <inheritdoc />
    public ICacheTransferObservationScope BeginTransfer(CacheTransferContext context) => this.Enabled() ? new TransferScope(logger) : null;

    private sealed class TransferScope(ILogger<LoggingCacheBehavior> logger) : ICacheTransferObservationScope
    {
        public void Complete(CacheTransferObservation observation)
        {
            logger.LogDebug("[{LogKey}] cache source transfer (collection={Collection}, provider={Provider}, success={Success}, bytes={Bytes}, elapsedMs={ElapsedMs})",
                "ApplicationCaching", observation.Context.Collection, observation.Context.Provider,
                observation.Completion.IsSuccess && observation.Completion.Value != CacheReadCompletion.Incomplete,
                observation.BytesDelivered, observation.Elapsed.TotalMilliseconds);
        }
    }

    private bool Enabled()
    {
        try { return logger?.IsEnabled(LogLevel.Debug) == true; }
        catch { return false; }
    }

    private void Record(string collection, string provider, string operation, bool success, long started)
    {
        try
        {
            logger.LogDebug("[{LogKey}] cache operation (collection={Collection}, provider={Provider}, operation={Operation}, success={Success}, elapsedMs={ElapsedMs})",
                "ApplicationCaching", collection, provider, operation, success, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch { /* Instrumentation cannot change an engine outcome. */ }
    }
}