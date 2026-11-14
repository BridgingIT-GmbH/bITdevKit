// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching.Dashboard;

using System.Security.Claims;
using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Http;

/// <summary>Creates a trusted current Dashboard management scope after host read or mutation authorization.</summary>
/// <example><code>var scope = factory.Create(context, CacheAccessActions.Read);</code></example>
public interface ICacheDashboardAccessContextFactory
{
    /// <summary>Creates host scope without accepting namespace, identity, or authority from a form or query string.</summary>
    CacheAccessContext Create(HttpContext context, CacheAccessActions action);
}

/// <summary>Uses the Dashboard host's explicit read access and authenticated mutation identity.</summary>
/// <example><code>services.AddSingleton&lt;ICacheDashboardAccessContextFactory, CacheDashboardAccessContextFactory&gt;();</code></example>
public sealed class CacheDashboardAccessContextFactory(CacheCatalog catalog, DashboardEndpointsOptions dashboard,
    CacheDashboardConfiguration configuration) : ICacheDashboardAccessContextFactory
{
    /// <inheritdoc />
    public CacheAccessContext Create(HttpContext context, CacheAccessActions action)
    {
        var subject = !dashboard.Enabled ? "" : context.User.Identity?.IsAuthenticated == true ?
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.Identity.Name ?? "authenticated" :
            action == CacheAccessActions.Read && (dashboard.AllowAnonymous || !dashboard.RequireAuthorization) ? "dashboard-host" : "";
        return new(catalog.Options.Namespace, subject, "dashboard", dashboard.Enabled ? action : CacheAccessActions.None,
            RevealSensitiveMetadata: dashboard.Enabled && configuration.RevealSensitiveMetadata);
    }
}