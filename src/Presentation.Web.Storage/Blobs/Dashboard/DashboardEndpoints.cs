// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard;

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Maps the Blob Storage dashboard pages and dashboard-local blob actions.
/// </summary>
/// <example>
/// <code>
/// services.AddDashboard(options => options.WithPluginAssemblyContaining&lt;DashboardEndpoints&gt;());
/// </code>
/// </example>
public sealed class DashboardEndpoints(DashboardEndpointsOptions options) : EndpointsBase, IDashboardEndpoints
{
    private const string BlobsPath = "/storage/blobs";
    private const string BlobsContentPath = "/storage/blobs/content";
    private const string BlobsDownloadPath = "/storage/blobs/download";
    private const int MaximumPreviewBytes = 1024 * 1024;

    private const string ActionsPath = "/storage/blobs/actions";

    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        options ??= new DashboardEndpointsOptions();

        if (!options.Enabled || !IsBlobStorageEnabled(app.ServiceProvider))
        {
            return;
        }

        var group = this.MapGroup(app, options)
            .WithTags("_bdk.Dashboard");

        group.MapDashboardPage<Pages.Index>(
            BlobsPath,
            "_bdk.Dashboard.Storage.Blobs",
            "Dashboard Blobs",
            "Shows registered blob storage clients with blob listing, upload, download, and delete actions.");

        var metrics = new StorageDashboardMetrics("blobs");
        app.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>()
            .ApplicationStopping.Register(metrics.Dispose);
        group.MapDashboardPage<Pages.Diagnostics>(
            BlobsPath + "/diagnostics",
            "_bdk.Dashboard.Storage.Blobs.Diagnostics",
            "Dashboard Blobs Diagnostics",
            "Shows recent storage transfer throughput and item counts.");
        var load = StorageLoadEndpoints.Map(group, BlobsPath, "blobs", app.ServiceProvider);
        group.MapGet(BlobsPath + "/diagnostics/data", () => Results.Json(metrics.Snapshot(load.Snapshot())))
            .ExcludeFromDescription();

        group.MapDashboardPage<Pages.Content>(
            BlobsContentPath,
            "_bdk.Dashboard.Storage.BlobsContent",
            "Dashboard Blobs Content",
            "Shows the refreshable blob storage dashboard content fragment.");

        group.MapGet(BlobsDownloadPath, async (
            HttpContext context,
            [FromQuery] string store,
            [FromQuery] string container,
            [FromQuery] string name,
            CancellationToken cancellationToken) =>
            await DownloadBlobAsync(context, store, container, name, cancellationToken))
            .WithName("_bdk.Dashboard.Storage.Blobs.Download")
            .WithSummary("Download blob storage blob")
            .ExcludeFromDescription();

        group.MapGet(BlobStorageDashboardRoutes.BlobsPreviewPath, async (
            HttpContext context,
            [FromQuery] string store,
            [FromQuery] string container,
            [FromQuery] string name,
            CancellationToken cancellationToken) =>
            await PreviewBlobAsync(context, store, new BlobKey(container, name), cancellationToken))
            .WithName("_bdk.Dashboard.Storage.Blobs.Preview")
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/upload", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (client, form) =>
            {
                var file = form.Files.GetFile("file");
                if (file is null || file.Length == 0)
                {
                    return Result.Failure(new ValidationError("Upload file is required."));
                }

                var container = GetFormValue(form, "container");
                var name = NormalizeBlobPath(GetFormValue(form, "name"));
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = NormalizeBlobPath(file.FileName);
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    return Result.Failure(new ValidationError("Blob name or file name is required."));
                }

                var prefix = NormalizeBlobPath(GetFormValue(form, "prefix"));
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    name = $"{prefix}/{name}";
                }

                var key = new BlobKey(container, name);
                var isEdit = string.Equals(GetFormValue(form, "mode"), "edit", StringComparison.OrdinalIgnoreCase);
                var forceOverwrite = GetBoolean(form, "forceOverwrite");
                var properties = new PropertyBag();
                BlobDimensions dimensions = null;
                DateTimeOffset? expiration = null;
                if (isEdit)
                {
                    var current = await client.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
                    if (current.IsSuccess)
                    {
                        properties = current.Value.Properties.Clone();
                        expiration = current.Value.ExpiresAt;
                    }
                    else if (!current.HasError<BlobStoreNotFoundError>())
                    {
                        return Result.Failure(current.Messages, current.Errors);
                    }
                    else if (!forceOverwrite)
                    {
                        return Result.Failure(new BlobStoreConflictError("The blob was deleted before replacement."));
                    }
                }
                else
                {
                    var metadata = HttpMetadata.ParseProperties(GetFormValue(form, "properties"));
                    if (metadata.IsFailure)
                    {
                        return Result.Failure(metadata.Messages, metadata.Errors);
                    }

                    var dimensionInput = GetFormValue(form, "dimensions");
                    var parsedDimensions = BlobStorageDimensionsJson.Parse(string.IsNullOrWhiteSpace(dimensionInput) ? null : dimensionInput);
                    if (parsedDimensions.IsFailure)
                    {
                        return Result.Failure(parsedDimensions.Messages, parsedDimensions.Errors);
                    }

                    properties = metadata.Value;
                    dimensions = parsedDimensions.Value;
                    var expiresAt = GetFormValue(form, "expiresAt");
                    if (!string.IsNullOrWhiteSpace(expiresAt))
                    {
                        if (!DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                        {
                            return Result.Failure(new ValidationError("Expiration must be an ISO-8601 timestamp."));
                        }

                        expiration = parsed;
                    }
                }

                await using var stream = file.OpenReadStream();
                var result = await client.UploadAsync(
                    new BlobUpload
                    {
                        Key = key,
                        Content = stream,
                        IfMatchETag = forceOverwrite ? null : GetFormValue(form, "etag"),
                        ContentType = ResolveContentType(file.ContentType, name),
                        ExpiresAt = expiration,
                        Properties = properties,
                        Dimensions = dimensions,
                        OverwriteMode = isEdit ? BlobOverwriteMode.Overwrite : BlobOverwriteMode.FailIfExists
                    },
                    cancellationToken);

                return result.IsSuccess
                    ? Result.Success().WithMessage($"Blob '{container}/{name}' uploaded.")
                    : Result.Failure(result.Messages, result.Errors);
            }))
            .WithName("_bdk.Dashboard.Storage.Blobs.Upload")
            .WithSummary("Upload blob storage blob")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/delete", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (client, form) =>
            {
                var result = await client.DeleteAsync(
                    new BlobKey(GetFormValue(form, "container"), GetFormValue(form, "name")),
                    new BlobDeleteOptions { IfMatchETag = GetFormValue(form, "etag") },
                    cancellationToken: cancellationToken);

                return result.IsSuccess
                    ? Result.Success().WithMessage("Blob deleted.")
                    : result;
            }))
            .WithName("_bdk.Dashboard.Storage.Blobs.Delete")
            .WithSummary("Delete blob storage blob")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/expiration", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, (client, form) => UpdateMetadataAsync(client, form, clearOmittedExpiration: true, cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Blobs.UpdateExpiration")
            .WithSummary("Update blob storage expiration")
            .DisableAntiforgery()
            .ExcludeFromDescription();

        group.MapPost($"{ActionsPath}/metadata", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, (client, form) => UpdateMetadataAsync(client, form, clearOmittedExpiration: false, cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Blobs.UpdateMetadata")
            .WithSummary("Update blob storage metadata")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }

    private static bool IsBlobStorageEnabled(IServiceProvider services) =>
        services.GetServices<BlobStoreClientRegistration>().Any();

    private static async Task<Result> UpdateMetadataAsync(
        IBlobStoreClient client,
        IFormCollection form,
        bool clearOmittedExpiration,
        CancellationToken cancellationToken)
    {
        var propertiesText = GetFormValue(form, "properties");
        var properties = form.ContainsKey("properties")
            ? HttpMetadata.ParseProperties(propertiesText)
            : Result<PropertyBag>.Success(null);
        if (properties.IsFailure)
        {
            return Result.Failure(properties.Messages, properties.Errors);
        }

        var dimensionInput = GetFormValue(form, "dimensions");
        var dimensions = BlobStorageDimensionsJson.Parse(string.IsNullOrWhiteSpace(dimensionInput) ? null : dimensionInput);
        if (dimensions.IsFailure)
        {
            return Result.Failure(dimensions.Messages, dimensions.Errors);
        }

        var expiresAtText = GetFormValue(form, "expiresAt");
        DateTimeOffset? expiresAt = null;
        if (!string.IsNullOrWhiteSpace(expiresAtText))
        {
            if (!DateTimeOffset.TryParse(expiresAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedExpiresAt))
            {
                return Result.Failure(new ValidationError("Expiration must be an ISO-8601 timestamp."));
            }

            expiresAt = parsedExpiresAt;
        }

        var key = new BlobKey(GetFormValue(form, "container"), GetFormValue(form, "name"));
        var current = await client.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        if (current.IsFailure)
        {
            return Result.Failure(current.Messages, current.Errors);
        }

        if (properties.Value?.Count > 0 && current.Value.Properties?.Count > 0)
        {
            using var edited = JsonDocument.Parse(propertiesText);
            // JSON scalars may have originated as more specific .NET property types.
            foreach (var entry in edited.RootElement.EnumerateObject().Reverse().DistinctBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (current.Value.Properties.TryGetValue(entry.Name, out var stored) &&
                    JsonElement.DeepEquals(entry.Value, JsonSerializer.SerializeToElement(stored)))
                {
                    properties.Value.Set(entry.Name, stored);
                }
            }
        }

        var updateResult = await client.UpdatePropertiesAsync(
            new BlobPropertiesUpdate
            {
                Key = key,
                ContentType = current.Value.ContentType,
                Properties = properties.Value,
                Dimensions = dimensions.Value,
                ExpiresAt = clearOmittedExpiration || form.ContainsKey("expiresAt") ? expiresAt : current.Value.ExpiresAt,
                IfMatchETag = GetBoolean(form, "forceOverwrite") ? null : GetFormValue(form, "etag")
            },
            cancellationToken).ConfigureAwait(false);

        return updateResult.IsSuccess
            ? Result.Success().WithMessage(clearOmittedExpiration ? "Blob expiration updated." : "Blob metadata saved.")
            : Result.Failure(updateResult.Messages, updateResult.Errors);
    }

    private static async Task<HttpResult> DownloadBlobAsync(
        HttpContext context,
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(context, storeName, out var client, out var failure))
        {
            return failure;
        }

        var result = await client.DownloadAsync(new BlobKey(container, name), cancellationToken);
        if (result.IsFailure)
        {
            return MapFailure(result);
        }

        var download = result.Value;
        var contentType = download.Info?.ContentType?.MimeType()
            ?? ContentTypeExtensions.FromFileName(name, ContentType.DEFAULT).MimeType();

        return Results.Stream(
            async output =>
            {
                await using (download.ConfigureAwait(false))
                {
                    await download.Content.CopyToAsync(output, cancellationToken);
                }
            },
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            CreateDownloadFileName(name));
    }

    private static async Task<HttpResult> PreviewBlobAsync(
        HttpContext context,
        string storeName,
        BlobKey key,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(context, storeName, out var client, out var failure))
        {
            return failure;
        }

        var properties = await client.GetPropertiesAsync(key, cancellationToken);
        if (properties.IsFailure)
        {
            return MapFailure(properties);
        }

        var contentType = PreviewContentType(properties.Value, key.Name);
        if (GetPreviewError(properties.Value.Length, contentType) is { } preflightError)
        {
            return preflightError;
        }

        var result = await client.DownloadAsync(key, cancellationToken);
        if (result.IsFailure)
        {
            return MapFailure(result);
        }

        await using var download = result.Value;
        var info = download.Info ?? properties.Value;
        contentType = PreviewContentType(info, key.Name);
        if (GetPreviewError(info.Length, contentType) is { } downloadError)
        {
            return downloadError;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(MaximumPreviewBytes + 1);
        try
        {
            var length = await download.Content.ReadAtLeastAsync(
                buffer.AsMemory(0, MaximumPreviewBytes + 1),
                MaximumPreviewBytes + 1,
                throwOnEndOfStream: false,
                cancellationToken);
            if (GetPreviewError(length, contentType) is { } sizeError)
            {
                return sizeError;
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Bytes(buffer.AsSpan(0, length).ToArray(), contentType);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static HttpResult GetPreviewError(long length, string contentType)
    {
        if (length > MaximumPreviewBytes)
        {
            return Results.Problem("Previews are available for blobs up to 1 MiB.", statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        return contentType is null
            ? Results.Problem("This file type cannot be previewed.", statusCode: StatusCodes.Status415UnsupportedMediaType)
            : null;
    }

    private static string PreviewContentType(BlobInfo info, string name)
    {
        var mediaType = info.ContentType?.MimeType() ?? ContentTypeExtensions.FromFileName(name, ContentType.DEFAULT).MimeType();
        mediaType = mediaType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return mediaType switch
        {
            "image/png" or "image/jpeg" or "image/gif" or "image/webp" => mediaType,
            "application/json" => "text/plain; charset=utf-8",
            _ when mediaType.StartsWith("text/", StringComparison.Ordinal) || mediaType.EndsWith("+json", StringComparison.Ordinal) =>
                "text/plain; charset=utf-8",
            _ => null
        };
    }

    private static async Task<HttpResult> ExecuteFormActionAsync(
        HttpContext context,
        Func<IBlobStoreClient, IFormCollection, Task<Result>> action)
    {
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var storeName = GetFormValue(form, "store");
        if (!TryCreateClient(context, storeName, out var client, out var failure))
        {
            return failure;
        }

        try
        {
            var isDelete = context.Request.Path.Value?.EndsWith("/delete", StringComparison.OrdinalIgnoreCase) == true;
            var isUpload = context.Request.Path.Value?.EndsWith("/upload", StringComparison.OrdinalIgnoreCase) == true;
            var isNew = isUpload && !string.Equals(GetFormValue(form, "mode"), "edit", StringComparison.OrdinalIgnoreCase);
            var force = GetBoolean(form, "forceOverwrite");
            if ((isNew && force) || (!isNew && (!force || isDelete) && string.IsNullOrWhiteSpace(GetFormValue(form, "etag"))))
                return Results.Problem("An original ETag is required for an edit; use Force overwrite explicitly to omit it.", statusCode: 400);
            var result = await action(client, form);
            return result.IsSuccess
                ? Results.Ok(new { message = result.Messages?.LastOrDefault() ?? "Blob action completed." })
                : MapFailure(result);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    private static bool TryCreateClient(
        HttpContext context,
        string storeName,
        out IBlobStoreClient client,
        out HttpResult failure)
    {
        client = null;
        failure = null;

        if (string.IsNullOrWhiteSpace(storeName))
        {
            failure = Results.Problem("Blob store is required.", statusCode: (int)HttpStatusCode.BadRequest);
            return false;
        }

        var factory = context.RequestServices.GetService<IBlobStoreClientFactory>();
        if (factory is null)
        {
            failure = Results.Problem("AddBlobStorage() is not registered.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
            return false;
        }

        try
        {
            client = factory.CreateClient(storeName);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            failure = Results.NotFound($"No blob storage client registered with name '{storeName}'.");
            return false;
        }
    }

    private static HttpResult MapFailure(Common.IResult result)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? "The blob storage request failed.";

        return error switch
        {
            ValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobDimensionsNotSupportedError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreQueryTooBroadError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStorePageSizeExceededError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreInvalidContinuationTokenError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreNotFoundError => Results.NotFound(message),
            BlobStoreConflictError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            BlobStoreLeaseError => Results.Problem(message, statusCode: (int)HttpStatusCode.Locked),
            BlobStoreIntegrityError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            BlobStoreSizeLimitExceededError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestEntityTooLarge),
            BlobStoreTimeoutError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestTimeout),
            _ => Results.Problem(message, statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    private static ContentType? ResolveContentType(string mimeType, string name)
    {
        if (!string.IsNullOrWhiteSpace(mimeType))
        {
            return ContentTypeExtensions.FromMimeType(mimeType, ContentType.DEFAULT);
        }

        return ContentTypeExtensions.FromFileName(name, ContentType.DEFAULT);
    }

    private static string GetFormValue(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) ? value.ToString().Trim() : string.Empty;

    private static bool GetBoolean(IFormCollection form, string key) =>
        form.TryGetValue(key, out var value) &&
        string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBlobPath(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\\', '/').Trim('/');

    private static string CreateDownloadFileName(string name)
    {
        var normalized = NormalizeBlobPath(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "download";
        }

        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(fileName.Select(character => invalidChars.Contains(character) ? '_' : character));
    }
}
