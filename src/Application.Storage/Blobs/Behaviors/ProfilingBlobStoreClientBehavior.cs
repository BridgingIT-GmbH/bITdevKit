// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;

/// <summary>Profiles blob-client calls using named store, container and operation dimensions.</summary>
/// <param name="inner">The decorated blob client; content-stream ownership remains unchanged.</param>
/// <param name="profiling">The optional shared operation profiler.</param>
/// <param name="storeName">The configured client name.</param>
/// <example><code>services.AddBlobStorage().WithProfilingBehavior().WithInMemoryClient("reports");</code></example>
public sealed class ProfilingBlobStoreClientBehavior(IBlobStoreClient inner, IOperationProfiler profiling = null, string storeName = null)
    : BlobStoreClientBehaviorBase(inner, storeName)
{
    /// <inheritdoc />
    protected override Task<Result<T>> ExecuteAsync<T>(string operation, BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result<T>>> next, CancellationToken cancellationToken) =>
        this.ProfileAsync(operation, context, () => next(cancellationToken), cancellationToken);

    /// <inheritdoc />
    protected override Task<Result> ExecuteAsync(string operation, BlobStoreOperationContext context,
        Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken) =>
        this.ProfileAsync(operation, context, () => next(cancellationToken), cancellationToken);

    private Task<TResult> ProfileAsync<TResult>(string operation, BlobStoreOperationContext context, Func<Task<TResult>> next, CancellationToken cancellationToken)
        where TResult : IResult
    {
        if (profiling is null) { return next(); }

        return profiling.JoinOrStartAsync(new OperationProfilingStartRequest
        {
            Key = "blobstorage:" + operation, Kind = "BlobStorage", CorrelationId = CorrelationId.Current,
        }, (scope, _) =>
        {
            try
            {
                if (scope.IsRecording)
                {
                    scope.SetDimension("blobstorage.store", this.StoreName);
                    scope.SetDimension("blobstorage.operation", operation);
                    if ((context.Key?.Container ?? context.Query?.Container) is { } container) { scope.SetDimension("blobstorage.container", container); }
                }
            }
            catch (Exception) { /* No blob names, query prefixes or stream contents are retained. */ }

            return next();
        }, cancellationToken, result => OperationProfilingHelpers.ClassifyResult(result));
    }
}
