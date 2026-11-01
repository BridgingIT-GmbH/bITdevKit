// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.Web.Auditing;

using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Explicit optional read-only audit API. Dashboard and embedded history do not depend on these routes.</summary>
/// <example><code>services.AddAuditing().WithEndpoints(); app.MapAuditingEndpoints();</code></example>
public sealed class AuditingEndpoints(AuditingEndpointsOptions options = null) : EndpointsBase
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (options is not { Enabled: true } || app.ServiceProvider.GetService<AuditingBuilder>() is null) return;
        var runtime = AuditWebRuntime.Get(app.ServiceProvider);
        var group = this.MapGroup(app, options).WithMetadata(new AuditSuppressionMetadata());
        group.AddEndpointFilter(async (invocation, next) =>
        {
            using var suppressed = invocation.HttpContext.RequestServices.GetRequiredService<IAuditContext>().Suppress();
            try
            {
                var result = await next(invocation).ConfigureAwait(false);
                return result is Microsoft.AspNetCore.Http.IResult response ? new AuditSuppressedResult(response) : result;
            }
            catch (AuditExportException error) { return AuditHttpHandlers.Error(error.Code); }
            catch (System.Text.Json.JsonException) { return AuditHttpHandlers.Error("AuditFilterInvalid"); }
            catch (Exception) { return AuditHttpHandlers.Error("AuditReadUnavailable"); }
        });
        group.MapGet("/records", (HttpContext http, IAuditQueryService queries) => AuditHttpHandlers.GuardAsync(async () =>
        {
            var result = await queries.SearchAsync(AuditHttpHandlers.QueryFilter(http), http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : result.MapHttpOkPaged();
        })).WithFilterSchema();
        group.MapPost("/records/search", (HttpContext http, IAuditQueryService queries) => AuditHttpHandlers.GuardAsync(async () =>
        {
            var result = await queries.SearchAsync(await AuditHttpHandlers.ReadFilterAsync(http).ConfigureAwait(false), http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : result.MapHttpOkPaged();
        })).WithFilterSchema(isRequestBody: true);
        group.MapPost("/records/export", (Delegate)((HttpContext http) => AuditHttpHandlers.GuardAsync(async () => await AuditHttpHandlers.ExportAsync(http, await AuditHttpHandlers.ReadFilterAsync(http).ConfigureAwait(false), runtime).ConfigureAwait(false))))
            .WithFilterSchema(isRequestBody: true).WithNdjson<AuditExportEntry>(stream =>
            { stream.Mode = NdjsonMode.Raw; stream.EnableCompression = false; stream.DisableBatching = true; stream.MaxRecordBytes = 8_388_607; }).Produces(200, contentType: "text/csv");
        group.MapGet("/exports/{id:guid}", (HttpContext http, Guid id) => AuditHttpHandlers.GuardAsync(() => AuditHttpHandlers.StatusAsync(http, id, runtime)));
        group.MapGet("/records/{id:guid}", async (HttpContext http, Guid id, IAuditQueryService queries) =>
        {
            var result = await queries.FindAsync(id, http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : result.MapHttpOk();
        });
        group.MapGet("/records/{id:guid}/changes", async (HttpContext http, Guid id, IAuditQueryService queries) =>
        {
            var result = await queries.FindAsync(id, http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : Results.Ok(result.Value.Record.EntityChanges);
        });
        group.MapGet("/records/{id:guid}/related", (HttpContext http, Guid id, IAuditQueryService queries) => AuditHttpHandlers.GuardAsync(async () =>
        {
            var result = await queries.FindRelatedAsync(id, AuditHttpHandlers.QueryFilter(http), http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : result.MapHttpOkPaged();
        })).WithFilterSchema();
        group.MapPost("/aggregates", (HttpContext http, IAuditQueryService queries) => AuditHttpHandlers.GuardAsync(async () =>
        {
            var result = await queries.AggregateAsync(await AuditHttpHandlers.ReadFilterAsync(http).ConfigureAwait(false), http.Request.Query["field"].ToString(), cancellationToken: http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : Results.Ok(result.Value);
        })).WithFilterSchema(isRequestBody: true);
        group.MapGet("/facets", (HttpContext http, IAuditQueryService queries) => AuditHttpHandlers.GuardAsync(async () =>
        {
            var result = await queries.FindFacetsAsync(AuditHttpHandlers.QueryFilter(http), http.Request.Query["field"].ToString(), http.Request.Query["prefix"].ToString(), cancellationToken: http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : Results.Ok(result.Value);
        })).WithFilterSchema();
        group.MapGet("/capabilities", (IAuditingStorageProvider provider, AuditFieldCatalog fields, AuditExportRegistry formats) => Results.Ok(new { provider = provider.Capabilities, fields = fields.Fields.Select(field => new { field.Domain, field.Name, field.Kind }), formats = formats.Formatters.Select(format => new { format.Format, format.Profile, format.Omissions }) }));
        group.MapGet("/diagnostics", async (AuditDiagnosticsService diagnostics, CancellationToken token) => Results.Ok(await diagnostics.GetSnapshotAsync(token).ConfigureAwait(false)));
        group.MapPost("/diagnostics/explain", async (HttpContext http, AuditDiagnosticsService diagnostics) =>
        {
            var result = await diagnostics.ExplainAsync(await AuditHttpHandlers.ReadProbeAsync(http).ConfigureAwait(false), http.RequestAborted).ConfigureAwait(false);
            return result.IsFailure ? AuditHttpHandlers.Failure(result.Errors) : result.MapHttpOk();
        });
    }
}
