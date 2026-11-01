// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.Storage;

using BridgingIT.DevKit.Common;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Owns one selected client call, preserving returned streams and lazy collections without reading or enumerating them.</summary>
/// <example><code>return StorageAuditBehaviorExecutor.RunAsync(scopes, "BlobStorage", "upload", () => "exports", false, options, next);</code></example>
public static class StorageAuditBehaviorExecutor
{
    /// <summary>Captures approved client/operation facts and known result counts, with no business persistence inference.</summary>
    /// <example><code>await StorageAuditBehaviorExecutor.RunAsync(scopes, "FileStorage", "write", () => "exports", false, options, next);</code></example>
    public static Task<T> RunAsync<T>(IServiceScopeFactory scopes, string feature, string operation, Func<string> client, bool read, StorageAuditOptions options, Func<Task<T>> next,
        Action<IAuditScope, T> completed = null) where T : IResult
        => AuditSourceExecutor.RunAsync(scopes, builder => builder.Key("storage." + feature.ToLowerInvariant() + "." + operation).Kind(feature).DisplayName(AuditNamingConvention.Humanize(operation)),
            next, new() { Feature = feature, ExecutionStatus = "Invoked" }, starting: (services, scope) =>
            {
                var alias = client();
                scope.SetDetail("storage.client", alias?.Length <= 128 ? AuditValue.From(alias) : AuditValue.Policy("Oversized"));
                scope.SetDetail("storage.operation", AuditValue.From(operation));
                if (options?.Resource?.Invoke(services, alias, operation) is { } resource) scope.AddResource(resource);
            }, completed: (scope, result) =>
            {
                if (result.IsFailure) scope.Complete(AuditOutcome.Failed);
                if (result is IResult<long> { IsSuccess: true } count) scope.SetDetail("storage.observedCount", AuditValue.From(count.Value));
                if (result.IsSuccess && operation is "read" or "open-write" or "download") (scope as IAuditSourceScope)?.SetSource(new() { ExecutionStatus = "Acquired" });
                completed?.Invoke(scope, result);
            }, select: read ? () => options?.SelectRead?.Invoke(operation) == true : null);
}
