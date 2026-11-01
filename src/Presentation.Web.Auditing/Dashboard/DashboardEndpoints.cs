// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing.Dashboard;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using IResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Automatically discovered audit review pages under the host's existing global dashboard access.</summary>
/// <example><code>services.AddAuditing(); services.AddDashboard(options => options.AllowAnonymous()); app.MapEndpoints();</code></example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options = null) : EndpointsBase, IDashboardEndpoints
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (options is not { Enabled: true } || app.ServiceProvider.GetService<AuditingBuilder>() is null || options.DisabledPageKeys.Contains("auditing")) return;
        var path = DashboardPath.Combine(options.GroupPath, "auditing");
        var runtime = AuditWebRuntime.Get(app.ServiceProvider);
        var group = this.MapGroup(app, options).MapGroup("/auditing").WithMetadata(new AuditSuppressionMetadata());
        group.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.Headers.CacheControl = "no-store";
            using var suppressed = invocation.HttpContext.RequestServices.GetRequiredService<IAuditContext>().Suppress();
            try
            {
                var result = await next(invocation).ConfigureAwait(false);
                return result is IResult response ? new AuditSuppressedResult(response) : result;
            }
            catch (AuditExportException error) { return AuditHttpHandlers.Error(error.Code); }
            catch (System.Text.Json.JsonException) { return AuditHttpHandlers.Error("AuditFilterInvalid"); }
            catch (Exception) { return AuditHttpHandlers.Error("AuditReadUnavailable"); }
        });
        group.MapGet("", (Delegate)(async (HttpContext http) => Results.RazorSlice<Pages.Index, AuditingViewModel>(await AuditDashboardModelBuilder.BuildAsync(http, path).ConfigureAwait(false)))).WithName("_bdk.Dashboard.Auditing");
        group.MapGet("/data", (Delegate)(async (HttpContext http) => Results.RazorSlice<Pages.Data, AuditingViewModel>(await AuditDashboardModelBuilder.BuildAsync(http, path).ConfigureAwait(false))));
        group.MapGet("/{id:guid}", async (HttpContext http, Guid id) => Results.RazorSlice<Pages.Index, AuditingViewModel>(await AuditDashboardModelBuilder.BuildAsync(http, path, id).ConfigureAwait(false)));
        group.MapGet("/{id:guid}/details", async (HttpContext http, Guid id) => Results.RazorSlice<Pages.Detail, AuditingViewModel>(await AuditDashboardModelBuilder.BuildAsync(http, path, id).ConfigureAwait(false)));
        group.MapGet("/diagnostics", (Delegate)(async (HttpContext http) => Results.RazorSlice<Pages.Index, AuditingViewModel>(await AuditDashboardModelBuilder.BuildAsync(http, path, diagnostics: true).ConfigureAwait(false))));
        group.MapGet("/download/ticket", (Delegate)(async (HttpContext http) => Results.Json(await AuditDownloadHandlers.IssueAsync(http).ConfigureAwait(false))));
        group.MapPost("/download", (Delegate)((HttpContext http) => AuditDownloadHandlers.DownloadAsync(http, runtime))).WithNdjson<AuditExportEntry>(stream =>
        { stream.Mode = NdjsonMode.Raw; stream.EnableCompression = false; stream.DisableBatching = true; stream.MaxRecordBytes = 8_388_607; });
        group.MapGet("/exports/{id:guid}", (HttpContext http, Guid id) => AuditHttpHandlers.StatusAsync(http, id, runtime));
    }
}
