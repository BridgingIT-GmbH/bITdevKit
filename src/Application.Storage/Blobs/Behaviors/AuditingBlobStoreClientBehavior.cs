// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Audits selected blob-client operations without copying blob names, queries, signed URLs, properties, or stream contents.</summary>
/// <example><code>services.AddBlobStorage().WithAuditing().WithInMemoryClient("exports");</code></example>
public sealed class AuditingBlobStoreClientBehavior(IBlobStoreClient inner, IServiceScopeFactory scopes, string storeName = null, StorageAuditOptions options = null)
    : BlobStoreClientBehaviorBase(inner, storeName), IBlobStoreContainerCatalog
{
    /// <inheritdoc />
    public Task<Result<IReadOnlyList<string>>> ListContainersAsync(CancellationToken cancellationToken = default)
        => this.Inner is IBlobStoreContainerCatalog catalog ? catalog.ListContainersAsync(cancellationToken)
            : Task.FromResult(Result<IReadOnlyList<string>>.Failure(new BlobStoreQueryNotSupportedError("Provider does not support container discovery.")));

    /// <inheritdoc />
    protected override Task<Result<T>> ExecuteAsync<T>(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result<T>>> next, CancellationToken cancellationToken)
        => this.AuditAsync(operation, () => next(cancellationToken));
    /// <inheritdoc />
    protected override Task<Result> ExecuteAsync(string operation, BlobStoreOperationContext context, Func<CancellationToken, Task<Result>> next, CancellationToken cancellationToken)
        => this.AuditAsync(operation, () => next(cancellationToken));
    private Task<T> AuditAsync<T>(string operation, Func<Task<T>> next) where T : IResult
        => StorageAuditBehaviorExecutor.RunAsync(scopes, "BlobStorage", operation, () => this.StoreName,
            operation is "exists" or "download" or "get_properties" or "list_page", options, next);
}
