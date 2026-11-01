// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace Microsoft.AspNetCore.Builder;

using BridgingIT.DevKit.Presentation.Web.Auditing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Maps explicitly selected audit routes using the same endpoint instance as general endpoint discovery.</summary>
/// <example><code>app.MapAuditingEndpoints();</code></example>
public static class AuditingApplicationBuilderExtensions
{
    /// <summary>Maps once; returns without mapping when WithEndpoints was not selected.</summary>
    /// <example><code>app.MapAuditingEndpoints();</code></example>
    public static WebApplication MapAuditingEndpoints(this WebApplication app)
    {
        var endpoint = app.Services.GetServices<BridgingIT.DevKit.Presentation.Web.IEndpoints>().OfType<AuditingEndpoints>().SingleOrDefault();
        if (endpoint is not null && !endpoint.IsRegistered) { endpoint.Map(app); endpoint.IsRegistered = true; }

        return app;
    }
}
