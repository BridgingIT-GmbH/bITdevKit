// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage.Permalinks.Dashboard;

using System.Globalization;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps compact Storage Permalink Registry dashboard and maintenance actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new();
        if (!options.Enabled || app.ServiceProvider.GetService<IStoragePermalinkRegistryProvider>() is null) return;
        var group = this.MapGroup(app, options).WithTags("_bdk.Dashboard");
        group.MapDashboardPage<Pages.Index>("/storage/permalinks", "_bdk.Dashboard.Storage.Permalinks", "Storage Permalinks", "Lists and maintains stable storage download links.");
        group.MapPost("/storage/permalinks/actions/expiration", UpdateExpirationAsync).DisableAntiforgery().ExcludeFromDescription();
        group.MapPost("/storage/permalinks/actions/delete", DeleteAsync).DisableAntiforgery().ExcludeFromDescription();
        group.MapPost("/storage/permalinks/actions/download", CreateAndDownloadAsync).DisableAntiforgery().ExcludeFromDescription();
        group.MapPost("/storage/permalinks/actions/link", CreateLinkAsync).DisableAntiforgery().ExcludeFromDescription();
        this.IsRegistered = true;
    }

    private static async Task<HttpResult> UpdateExpirationAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        if (!StoragePermalinkId.TryParse(form["id"], out var id)) return Results.BadRequest();
        DateTimeOffset? expiresAt = null;
        if (!string.IsNullOrWhiteSpace(form["expiresAt"]))
        {
            if (!DateTimeOffset.TryParse(form["expiresAt"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) return Results.BadRequest();
            expiresAt = parsed;
        }

        var result = await context.RequestServices.GetRequiredService<IStoragePermalinkMaintenanceService>().UpdateExpirationAsync(id, new() { ExpiresAt = expiresAt, IfMatchETag = form["etag"] }, cancellationToken);
        return result.IsSuccess ? RedirectToDashboard(context) : Results.Conflict();
    }

    private static async Task<HttpResult> DeleteAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        if (!StoragePermalinkId.TryParse(form["id"], out var id)) return Results.BadRequest();
        var result = await context.RequestServices.GetRequiredService<IStoragePermalinkMaintenanceService>().DeleteAsync(id, new() { IfMatchETag = form["etag"] }, cancellationToken);
        return result.IsSuccess ? RedirectToDashboard(context) : Results.Conflict();
    }

    private static async Task<HttpResult> CreateAndDownloadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var result = await ResolveAsync(context, form, cancellationToken);
        return result.IsSuccess
            ? Results.Redirect(StoragePermalinkRoutes.Download(result.Value.Id))
            : CreateFailureResult(result);
    }

    private static async Task<HttpResult> CreateLinkAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var result = await ResolveAsync(context, form, cancellationToken);
        return result.IsSuccess
            ? Results.Json(new { url = StoragePermalinkRoutes.Download(result.Value.Id) })
            : CreateFailureResult(result);
    }

    private static async Task<Result<StoragePermalinkEntry>> ResolveAsync(HttpContext context, IFormCollection form, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<StorageResourceKind>(form["kind"], true, out var kind))
            return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError("The storage kind is invalid."));
        try
        {
            var registration = form["registration"].ToString();
            var scope = form["scope"].ToString();
            var path = form["path"].ToString();
            return kind switch
            {
                StorageResourceKind.Blob => await ResolveBlobAsync(context, registration, scope, path, cancellationToken),
                StorageResourceKind.Document => await ResolveDocumentAsync(context, registration, scope, path, cancellationToken),
                StorageResourceKind.File => await ResolveFileAsync(context, registration, path, cancellationToken),
                _ => Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError("The storage kind is unsupported."))
            };
        }
        catch (ArgumentException ex) { return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError(ex.Message)); }
        catch (InvalidOperationException ex) { return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError(ex.Message)); }
        catch (KeyNotFoundException ex) { return Result<StoragePermalinkEntry>.Failure(new StoragePermalinkValidationError(ex.Message)); }
    }

    private static Task<Result<StoragePermalinkEntry>> ResolveBlobAsync(HttpContext context, string registration, string scope, string path, CancellationToken cancellationToken)
    {
        var factory = context.RequestServices.GetService<IBlobStoreClientFactory>();
        return factory is null
            ? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError(registration)))
            : factory.CreateClient(registration).GetPermalinkAsync(new(scope, path), cancellationToken: cancellationToken);
    }

    private static Task<Result<StoragePermalinkEntry>> ResolveDocumentAsync(HttpContext context, string registration, string scope, string path, CancellationToken cancellationToken)
    {
        var accessor = context.RequestServices.GetService<IDocumentStoreClientFactory>()?.Create(registration);
        return accessor?.GetPermalinkAsync(new(scope, path), cancellationToken: cancellationToken)
            ?? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError(registration)));
    }

    private static Task<Result<StoragePermalinkEntry>> ResolveFileAsync(HttpContext context, string registration, string path, CancellationToken cancellationToken)
    {
        var factory = context.RequestServices.GetService<IFileStorageProviderFactory>();
        return factory is null
            ? Task.FromResult(Result<StoragePermalinkEntry>.Failure(new StoragePermalinkNotEnabledError(registration)))
            : factory.CreateProvider(registration).GetPermalinkAsync(path, cancellationToken: cancellationToken);
    }

    private static HttpResult CreateFailureResult(Result<StoragePermalinkEntry> result)
    {
        var message = result.Errors.FirstOrDefault()?.Message ?? "The permalink could not be created.";
        if (result.Errors.Any(x => x is StoragePermalinkValidationError or StoragePermalinkNotEnabledError))
        {
            return Results.Problem(message, statusCode: StatusCodes.Status400BadRequest);
        }

        if (result.Errors.Any(x => x is BlobStoreNotFoundError or DocumentStoreNotFoundError or NotFoundError))
        {
            return Results.NotFound();
        }

        return Results.Problem("The permalink could not be created.", statusCode: StatusCodes.Status500InternalServerError);
    }

    private static HttpResult RedirectToDashboard(HttpContext context)
    {
        var fallback = StoragePermalinkDashboardRoutes.Index(context.RequestServices.GetRequiredService<DashboardEndpointsOptions>());
        if (!Uri.TryCreate(context.Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) ||
            !string.Equals(referer.Host, context.Request.Host.Host, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(referer.Scheme, context.Request.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Redirect(fallback);
        }

        var localPath = referer.PathAndQuery;
        return localPath.StartsWith(fallback, StringComparison.OrdinalIgnoreCase)
            ? Results.Redirect(localPath)
            : Results.Redirect(fallback);
    }
}
