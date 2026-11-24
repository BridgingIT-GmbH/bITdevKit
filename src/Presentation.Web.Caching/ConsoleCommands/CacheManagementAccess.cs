// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;

/// <summary>Resolves current host identity and action authorization for Console or MCP without accepting identity from tool arguments.</summary>
/// <remarks>The default denies access. Hosts provide an implementation backed by their trusted process or authenticated caller context.</remarks>
/// <example><code>builder.WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
public interface ICacheManagementAccessContextFactory
{
    /// <summary>Authorizes the current host caller for the requested bounded operation.</summary>
    /// <param name="request">Requested action, transport, and declared targets.</param>
    /// <param name="cancellationToken">The transport cancellation token.</param>
    /// <returns>A trusted access context or an explicit authorization failure.</returns>
    ValueTask<Result<CacheAccessContext>> CreateAsync(CacheManagementAccessRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Describes requested management work without granting identity, tenant, namespace, or action authority.</summary>
/// <param name="Transport">The registered ConsoleCommands or Mcp transport.</param>
/// <param name="Operation">The fixed supported operation name.</param>
/// <param name="Collections">The explicit bounded declared target set; empty means a metadata-only collection listing.</param>
/// <param name="RequiredAction">The action the host must authorize.</param>
/// <example><code>var required = request.RequiredAction;</code></example>
public sealed record CacheManagementAccessRequest(string Transport, string Operation, IReadOnlyList<string> Collections,
    CacheAccessActions RequiredAction);

/// <summary>Denies management until the host explicitly supplies a trusted authorization context factory.</summary>
/// <example><code>services.AddApplicationCaching().WithManagementAccessContext&lt;OperationsAccessContextFactory&gt;();</code></example>
public sealed class CacheDeniedManagementAccessContextFactory : ICacheManagementAccessContextFactory
{
    /// <inheritdoc />
    public ValueTask<Result<CacheAccessContext>> CreateAsync(CacheManagementAccessRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<CacheAccessContext>.Failure(new UnauthorizedError("A trusted host management authorization context is required.")));
    }
}

/// <summary>Contains bounded non-payload arguments shared by the independent Console and MCP adapters.</summary>
/// <param name="Collection">One declared target for inspection or exact-key controls.</param>
/// <param name="Collections">At most 32 explicit targets for invalidation or clear.</param>
/// <param name="Key">An optional exact key selector.</param>
/// <param name="Prefix">An optional literal prefix selector.</param>
/// <param name="Pattern">An optional bounded portable glob selector.</param>
/// <param name="SelectionJson">Optional structured selection JSON, bounded to 64 KiB.</param>
/// <param name="Take">The bounded metadata page size, at most 1,000.</param>
/// <param name="Continuation">The opaque selection continuation.</param>
/// <param name="OperationId">A retained provider-owned operation handle.</param>
/// <param name="Background">Whether invalidation should return accepted status before execution.</param>
/// <param name="Cutoff">The original page cutoff returned with a continuation.</param>
/// <example><code>var arguments = new CacheManagementArguments(Collection: "cards", Prefix: "region:");</code></example>
public sealed record CacheManagementArguments(string Collection = null, IReadOnlyList<string> Collections = null,
    string Key = null, string Prefix = null, string Pattern = null, string SelectionJson = null, int Take = 100,
    string Continuation = null, string OperationId = null, bool Background = false, DateTimeOffset? Cutoff = null);