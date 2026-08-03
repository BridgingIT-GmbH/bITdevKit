// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Net;
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Resolves stable permalink identifiers and streams current Blob, Document, or File Storage content.
/// </summary>
/// <example>
/// <code>
/// services.AddStoragePermalinks().UseInMemory().AddDownloadEndpoints();
/// </code>
/// </example>
public sealed class StoragePermalinkEndpoints(
    IStoragePermalinkRegistry registry,
    StoragePermalinkMetrics metrics,
    StoragePermalinkEndpointsOptions options = null) : EndpointsBase
{
    private readonly StoragePermalinkEndpointsOptions options = options ?? new();

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled) return;
        var group = this.MapGroup(app, this.options).DisableAntiforgery();
        group.MapGet("{id}", (HttpContext context, string id, CancellationToken cancellationToken) => this.DownloadAsync(context, id, cancellationToken))
            .Produces((int)HttpStatusCode.OK)
            .Produces((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName("_bdk.Storage.Permalinks.Download")
            .WithSummary("Download storage content by permalink");
        this.IsRegistered = true;
    }

    private async Task<HttpResult> DownloadAsync(HttpContext context, string rawId, CancellationToken cancellationToken)
    {
        var started = metrics.Start();
        if (!StoragePermalinkId.TryParse(rawId, out var id))
        {
            metrics.RecordDownload(started, "invalid");
            return Results.Problem("The permalink identifier is invalid.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var resolved = await registry.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            var notFound = resolved.Errors.Any(x => x is StoragePermalinkNotFoundError);
            metrics.RecordDownload(started, notFound ? "not_found" : "failure");
            return notFound ? Results.NotFound() : Results.Problem("The permalink could not be resolved.", statusCode: (int)HttpStatusCode.InternalServerError);
        }

        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        try
        {
            return resolved.Value.Location.Kind switch
            {
                StorageResourceKind.Blob => await this.DownloadBlobAsync(context, resolved.Value, started, cancellationToken).ConfigureAwait(false),
                StorageResourceKind.Document => await this.DownloadDocumentAsync(context, resolved.Value, started, cancellationToken).ConfigureAwait(false),
                StorageResourceKind.File => await this.DownloadFileAsync(context, resolved.Value, started, cancellationToken).ConfigureAwait(false),
                _ => this.Unsupported(resolved.Value, started)
            };
        }
        catch (OperationCanceledException)
        {
            metrics.RecordDownload(started, "cancelled", resolved.Value.Location.Kind);
            throw;
        }
        catch (Exception)
        {
            metrics.RecordDownload(started, "failure", resolved.Value.Location.Kind);
            return Results.Problem("The permalink target could not be downloaded.", statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    private async Task<HttpResult> DownloadBlobAsync(HttpContext context, StoragePermalinkEntry entry, long started, CancellationToken cancellationToken)
    {
        var blobFactory = context.RequestServices.GetService<IBlobStoreClientFactory>();
        if (blobFactory is null) return this.ProviderUnavailable(entry, started);
        IBlobStoreClient client;
        try
        {
            client = blobFactory.CreateClient(entry.Location.RegistrationName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return this.ProviderUnavailable(entry, started);
        }

        var result = await client.DownloadAsync(new(entry.Location.Scope, entry.Location.Path), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return this.StorageFailure(entry, started, result);
        var download = result.Value;
        var contentType = ResolveBlobMimeType(download.Info, entry.Location.Path);
        return Results.Stream(async output =>
        {
            try
            {
                await using (download.ConfigureAwait(false)) await download.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                metrics.RecordDownload(started, "success", entry.Location.Kind);
            }
            catch (OperationCanceledException)
            {
                metrics.RecordDownload(started, "cancelled", entry.Location.Kind);
                throw;
            }
            catch
            {
                metrics.RecordDownload(started, "failure", entry.Location.Kind);
                throw;
            }
        }, contentType: contentType, fileDownloadName: Path.GetFileName(entry.Location.Path));
    }

    private async Task<HttpResult> DownloadDocumentAsync(HttpContext context, StoragePermalinkEntry entry, long started, CancellationToken cancellationToken)
    {
        var documentFactory = context.RequestServices.GetService<IDocumentStoreClientFactory>();
        var accessor = documentFactory?.Create(entry.Location.RegistrationName);
        if (accessor is null) return this.ProviderUnavailable(entry, started);
        var result = await accessor.GetJsonAsync(new(entry.Location.Scope, entry.Location.Path), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return this.StorageFailure(entry, started, result);
        metrics.RecordDownload(started, "success", entry.Location.Kind);
        return Results.File(Encoding.UTF8.GetBytes(result.Value), "application/json; charset=utf-8", CreateDocumentDownloadFileName(entry.Location.Path));
    }

    private async Task<HttpResult> DownloadFileAsync(HttpContext context, StoragePermalinkEntry entry, long started, CancellationToken cancellationToken)
    {
        var fileFactory = context.RequestServices.GetService<IFileStorageProviderFactory>();
        if (fileFactory is null) return this.ProviderUnavailable(entry, started);
        IFileStorageProvider provider;
        try
        {
            provider = fileFactory.CreateProvider(entry.Location.RegistrationName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return this.ProviderUnavailable(entry, started);
        }

        var result = await provider.ReadFileAsync(entry.Location.Path, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return this.StorageFailure(entry, started, result);
        var stream = result.Value;
        var contentType = ContentTypeExtensions.FromFileName(entry.Location.Path, ContentType.DEFAULT).MimeType();
        return Results.Stream(async output =>
        {
            try
            {
                await using (stream.ConfigureAwait(false)) await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                metrics.RecordDownload(started, "success", entry.Location.Kind);
            }
            catch (OperationCanceledException)
            {
                metrics.RecordDownload(started, "cancelled", entry.Location.Kind);
                throw;
            }
            catch
            {
                metrics.RecordDownload(started, "failure", entry.Location.Kind);
                throw;
            }
        }, contentType: contentType, fileDownloadName: Path.GetFileName(entry.Location.Path));
    }

    private HttpResult StorageFailure(StoragePermalinkEntry entry, long started, Result result)
    {
        var notFound = result.Errors.Any(x => x is BlobStoreNotFoundError or DocumentStoreNotFoundError or NotFoundError);
        metrics.RecordDownload(started, notFound ? "target_not_found" : "failure", entry.Location.Kind);
        return notFound ? Results.NotFound() : Results.Problem("The permalink target could not be downloaded.", statusCode: (int)HttpStatusCode.InternalServerError);
    }

    private HttpResult ProviderUnavailable(StoragePermalinkEntry entry, long started)
    {
        metrics.RecordDownload(started, "provider_unavailable", entry.Location.Kind);
        return Results.NotFound();
    }

    private HttpResult Unsupported(StoragePermalinkEntry entry, long started)
    {
        metrics.RecordDownload(started, "unsupported", entry.Location.Kind);
        return Results.Problem("The permalink storage kind is unsupported.", statusCode: (int)HttpStatusCode.InternalServerError);
    }

    private static string SafeFileName(string value) => string.Concat(Path.GetFileName(value).Select(x => Path.GetInvalidFileNameChars().Contains(x) ? '_' : x));

    private static string CreateDocumentDownloadFileName(string value)
    {
        var fileName = SafeFileName(value);
        return fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : $"{fileName}.json";
    }

    private static string ResolveBlobMimeType(BlobInfo info, string blobName)
    {
        var storedMimeType = info?.ContentType?.MimeType();
        return !string.IsNullOrWhiteSpace(storedMimeType)
            ? storedMimeType
            : ContentTypeExtensions.FromFileName(blobName, ContentType.DEFAULT).MimeType();
    }
}
