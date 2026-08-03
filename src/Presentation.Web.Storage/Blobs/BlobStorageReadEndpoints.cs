// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Exposes read-only REST endpoints for downloading Blob Storage content.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithInMemoryClient("reports")
///     .AddReadEndpoints(options => options.AllowAnonymous());
/// </code>
/// </example>
public sealed class BlobStorageReadEndpoints(
    IBlobStoreClientFactory factory,
    BlobStorageReadEndpointsOptions options = null) : EndpointsBase
{
    private const string RouteNamePrefix = "_bdk.Storage.Blobs.Read";
    private readonly IBlobStoreClientFactory factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly BlobStorageReadEndpointsOptions options = options ?? new BlobStorageReadEndpointsOptions();

    /// <summary>
    /// Maps Blob Storage read endpoints into the current endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <example>
    /// <code>
    /// app.MapEndpoints();
    /// </code>
    /// </example>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled)
        {
            return;
        }

        var group = this.MapGroup(app, this.options)
            .DisableAntiforgery();

        group.MapGet("{storeName}/content", (string storeName, [FromQuery] string container, [FromQuery] string name, CancellationToken cancellationToken) =>
                this.DownloadAsync(storeName, container, name, cancellationToken))
            .Produces((int)HttpStatusCode.OK)
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Download")
            .WithSummary("Download blob content")
            .WithDescription("Streams one blob by exact key through the configured Blob Storage client.");

        this.IsRegistered = true;
    }

    private async Task<HttpResult> DownloadAsync(
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        var result = await client.DownloadAsync(new BlobKey(container, name), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return MapFailure(result, $"{container}/{name}");
        }

        var download = result.Value;
        var contentType = download.Info?.ContentType?.MimeType();
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = ContentTypeExtensions.FromFileName(name, ContentType.DEFAULT).MimeType();
        }

        return Results.Stream(
            async output =>
            {
                await using (download.ConfigureAwait(false))
                {
                    await download.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }
            },
            contentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            fileDownloadName: Path.GetFileName(name));
    }

    private bool TryCreateClient(string storeName, out IBlobStoreClient client, out HttpResult failure)
    {
        try
        {
            client = this.factory.CreateClient(storeName);
            failure = null;

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            client = null;
            failure = Results.NotFound($"No blob storage client registered with name '{storeName}'.");

            return false;
        }
    }

    private static HttpResult MapFailure(Result result, string subject)
    {
        var error = result.Errors?.FirstOrDefault();
        var message = result.Messages?.LastOrDefault()
            ?? error?.Message
            ?? $"The request for '{subject}' failed.";

        if (IsNotFound(result))
        {
            return Results.NotFound(message);
        }

        return error switch
        {
            BlobStoreValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreTimeoutError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestTimeout),
            _ => Results.Problem(message, statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    private static bool IsNotFound(Result result) =>
        result.Errors?.Any(error => error is BlobStoreNotFoundError) == true;
}
