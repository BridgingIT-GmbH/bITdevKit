// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using System.Security.Claims;
using BridgingIT.DevKit.Application.Caching;
using Microsoft.AspNetCore.Http;

/// <summary>Creates trusted application management scope from current host identity after action authorization.</summary>
/// <example><code>var access = factory.Create(context, CacheAccessActions.Read);</code></example>
public interface ICacheHttpAccessContextFactory
{
    /// <summary>Creates current scope without trusting namespace, tenant, authority, or principal data from the request body.</summary>
    CacheAccessContext Create(HttpContext context, CacheAccessActions action);
}

/// <summary>Uses the configured namespace and authenticated host identity for the external cache API.</summary>
/// <example><code>services.AddSingleton&lt;ICacheHttpAccessContextFactory, CacheHttpAccessContextFactory&gt;();</code></example>
public sealed class CacheHttpAccessContextFactory(CacheCatalog catalog, CacheWebConfiguration configuration) : ICacheHttpAccessContextFactory
{
    /// <inheritdoc />
    public CacheAccessContext Create(HttpContext context, CacheAccessActions action) => new(
        catalog.Options.Namespace, context.User.Identity?.IsAuthenticated == true ?
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity.Name ?? "authenticated" : "",
        "http", action, RevealSensitiveMetadata: configuration.Options.RevealSensitiveMetadata);
}