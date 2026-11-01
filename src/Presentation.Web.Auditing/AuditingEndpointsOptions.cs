// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Presentation.Web;

/// <summary>Optional read-only public audit routes. The host owns authentication, authorization and trusted data scope.</summary>
/// <example><code>auditing.WithEndpoints(options => options.RequirePolicy = "AuditReview");</code></example>
public sealed class AuditingEndpointsOptions : EndpointsOptionsBase
{
    /// <summary>Creates the conventional audit API prefix with host authorization required.</summary>
    /// <example><code>var options = new AuditingEndpointsOptions { GroupPath = "/api/audit", RequirePolicy = "AuditReview" };</code></example>
    public AuditingEndpointsOptions() { this.GroupPath = "/_bdk/api/auditing"; this.GroupTag = "Auditing"; this.RequireAuthorization = true; }
}
