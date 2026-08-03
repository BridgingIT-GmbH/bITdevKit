// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard;

using System.Globalization;
using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
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

        group.MapPost($"{ActionsPath}/upload", async (HttpContext context, CancellationToken cancellationToken) =>
            await ExecuteFormActionAsync(context, async (client, form) =>
            {
                var file = form.Files.GetFile("file");
                if (file is null || file.Length == 0)
                {
                    return Result.Failure(new ValidationError("Upload file is required."));
                }

                var container = GetFormValue(form, "container");
                var name = CombineBlobName(GetFormValue(form, "prefix"), GetFormValue(form, "name"));
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = CombineBlobName(GetFormValue(form, "prefix"), file.FileName);
                }

                await using var stream = file.OpenReadStream();
                var expiresAt = GetFormValue(form, "expiresAt");
                DateTimeOffset? expiration = null;
                if (!string.IsNullOrWhiteSpace(expiresAt))
                {
                    if (!DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                    {
                        return Result.Failure(new ValidationError("Expiration must be an ISO-8601 timestamp."));
                    }

                    expiration = parsed;
                }

                var result = await client.UploadAsync(
                    new BlobUpload
                    {
                        Key = new BlobKey(container, name),
                        Content = stream,
                        ContentType = ResolveContentType(file.ContentType, name),
                        ExpiresAt = expiration,
                        OverwriteMode = GetBoolean(form, "overwrite")
                            ? BlobOverwriteMode.Overwrite
                            : BlobOverwriteMode.FailIfExists
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
            await ExecuteFormActionAsync(context, (client, form) => UpdateExpirationAsync(client, form, cancellationToken)))
            .WithName("_bdk.Dashboard.Storage.Blobs.UpdateExpiration")
            .WithSummary("Update blob storage expiration")
            .DisableAntiforgery()
            .ExcludeFromDescription();
    }

    private static bool IsBlobStorageEnabled(IServiceProvider services) =>
        services.GetServices<BlobStoreClientRegistration>().Any();

    private static async Task<Result> UpdateExpirationAsync(
        IBlobStoreClient client,
        IFormCollection form,
        CancellationToken cancellationToken)
    {
        var key = new BlobKey(GetFormValue(form, "container"), GetFormValue(form, "name"));
        var propertiesResult = await client.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        if (propertiesResult.IsFailure)
        {
            return Result.Failure(propertiesResult.Messages, propertiesResult.Errors);
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

        var updateResult = await client.UpdatePropertiesAsync(
            new BlobPropertiesUpdate
            {
                Key = key,
                ContentType = propertiesResult.Value.ContentType,
                Properties = propertiesResult.Value.Properties,
                ExpiresAt = expiresAt,
                IfMatchETag = GetFormValue(form, "etag")
            },
            cancellationToken).ConfigureAwait(false);

        return updateResult.IsSuccess
            ? Result.Success().WithMessage("Blob expiration updated.")
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

    private static HttpResult MapFailure(BridgingIT.DevKit.Common.IResult result)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? "The blob storage request failed.";

        return error switch
        {
            ValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
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

    private static string CombineBlobName(string prefix, string name)
    {
        var normalizedPrefix = NormalizeBlobPath(prefix);
        var normalizedName = NormalizeBlobPath(name);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return normalizedPrefix;
        }

        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? normalizedName
            : $"{normalizedPrefix}/{normalizedName}";
    }

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
