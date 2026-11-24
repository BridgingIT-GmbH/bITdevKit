// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DoFiesta.Application.Modules.Core;

using BridgingIT.DevKit.Common;

/// <summary>Requests the cached JSON representation of the application's shared enumeration catalog.</summary>
/// <example><code>var json = await requester.SendAsync(new EnumerationCatalogQuery(), cancellationToken);</code></example>
public sealed class EnumerationCatalogQuery : RequestBase<string>
{
}

/// <summary>Routes reference-catalog reads through the same application service used by every transport.</summary>
/// <param name="catalog">The cache-backed public-reference catalog.</param>
/// <example><code>await requester.SendAsync(new EnumerationCatalogQuery(), cancellationToken);</code></example>
public sealed class EnumerationCatalogQueryHandler(EnumerationCatalog catalog) : RequestHandlerBase<EnumerationCatalogQuery, string>
{
    /// <inheritdoc />
    protected override async Task<Result<string>> HandleAsync(EnumerationCatalogQuery request, SendOptions options, CancellationToken cancellationToken) =>
        await catalog.GetJsonAsync(cancellationToken);
}