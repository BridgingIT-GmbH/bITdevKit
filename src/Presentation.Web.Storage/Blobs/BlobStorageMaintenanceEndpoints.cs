// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>
/// Exposes metadata-only REST endpoints for registered Blob Storage clients.
/// </summary>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithInMemoryClient("reports")
///     .AddMaintenanceEndpoints(options => options.RequireAuthorization());
/// </code>
/// </example>
public sealed class BlobStorageMaintenanceEndpoints(
    ILoggerFactory loggerFactory,
    IBlobStoreClientFactory factory,
    BlobStorageMaintenanceEndpointsOptions options = null) : EndpointsBase
{
    private const string RouteNamePrefix = "_bdk.Storage.Blobs.Maintenance";
    private readonly ILogger<BlobStorageMaintenanceEndpoints> logger =
        loggerFactory?.CreateLogger<BlobStorageMaintenanceEndpoints>() ??
        NullLogger<BlobStorageMaintenanceEndpoints>.Instance;
    private readonly IBlobStoreClientFactory factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly BlobStorageMaintenanceEndpointsOptions options = options ?? new BlobStorageMaintenanceEndpointsOptions();

    /// <summary>
    /// Maps Blob Storage maintenance endpoints into the current endpoint route builder.
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

        group.MapGet("clients", (CancellationToken cancellationToken) =>
                this.ListClientsAsync(cancellationToken))
            .Produces<List<BlobStorageClientInfoModel>>()
            .WithName($"{RouteNamePrefix}.ListClients")
            .WithSummary("List registered blob storage clients")
            .WithDescription("Retrieves the registered Blob Storage clients and provider-neutral capabilities.");

        group.MapGet("{storeName}/provider", (string storeName, CancellationToken cancellationToken) =>
                this.GetClientInfoAsync(storeName, cancellationToken))
            .Produces<BlobStorageClientInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .WithName($"{RouteNamePrefix}.GetClientInfo")
            .WithSummary("Get blob storage client information")
            .WithDescription("Retrieves provider-neutral information about one configured Blob Storage client.");

        group.MapGet("{storeName}/blobs/exists", (string storeName, [FromQuery] string container, [FromQuery] string name, CancellationToken cancellationToken) =>
                this.ExistsAsync(storeName, container, name, cancellationToken))
            .Produces<BlobStorageExistsResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Exists")
            .WithSummary("Check whether a blob exists")
            .WithDescription("Checks exact-key blob existence without downloading blob content.");

        group.MapGet("{storeName}/blobs/properties", (string storeName, [FromQuery] string container, [FromQuery] string name, CancellationToken cancellationToken) =>
                this.GetPropertiesAsync(storeName, container, name, cancellationToken))
            .Produces<BlobStorageBlobInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.GetProperties")
            .WithSummary("Get blob properties")
            .WithDescription("Retrieves blob metadata without downloading blob content.");

        group.MapPatch("{storeName}/blobs/properties", (string storeName, BlobStorageUpdatePropertiesRequestModel request, CancellationToken cancellationToken) =>
                this.UpdatePropertiesAsync(storeName, request, cancellationToken))
            .Accepts<BlobStorageUpdatePropertiesRequestModel>("application/json")
            .Produces<BlobStorageBlobInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.Locked)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.UpdateProperties")
            .WithSummary("Update blob properties")
            .WithDescription("Updates blob metadata without downloading or rewriting blob content.");

        group.MapGet("{storeName}/blobs", (string storeName, [FromQuery] string container, [FromQuery] string prefix, [FromQuery] int? take, [FromQuery] string continuationToken, [FromQuery] bool? allowFullScan, CancellationToken cancellationToken) =>
                this.ListPageAsync(storeName, container, prefix, take, continuationToken, allowFullScan, cancellationToken))
            .Produces<BlobStorageBlobPageModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ListPage")
            .WithSummary("List blobs")
            .WithDescription("Lists one page of blob metadata without returning content streams.");

        group.MapDelete("{storeName}/blobs", (string storeName, [FromQuery] string container, [FromQuery] string name, CancellationToken cancellationToken) =>
                this.DeleteAsync(storeName, container, name, cancellationToken))
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.Locked)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Delete")
            .WithSummary("Delete a blob")
            .WithDescription("Deletes a blob by exact key. Deleting a missing blob follows the configured blob client contract.");

        this.IsRegistered = true;
    }

    private Task<List<BlobStorageClientInfoModel>> ListClientsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(this.factory.GetRegistrations()
            .Select(MapRegistration)
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private Task<HttpResult> GetClientInfoAsync(string storeName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var registration = this.factory.GetRegistrations()
            .FirstOrDefault(item => string.Equals(item.Name, storeName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(registration is null
            ? Results.NotFound($"No blob storage client registered with name '{storeName}'.")
            : Results.Ok(MapRegistration(registration)));
    }

    private async Task<HttpResult> ExistsAsync(
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        var key = new BlobKey(container, name);
        var result = await client.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return Results.Ok(new BlobStorageExistsResponseModel
            {
                Container = container,
                Name = name,
                Exists = result.Value
            });
        }

        return IsNotFound(result)
            ? Results.Ok(new BlobStorageExistsResponseModel { Container = container, Name = name, Exists = false })
            : MapFailure(result, $"{container}/{name}");
    }

    private async Task<HttpResult> GetPropertiesAsync(
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        var result = await client.GetPropertiesAsync(new BlobKey(container, name), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(MapInfo(result.Value))
            : MapFailure(result, $"{container}/{name}");
    }

    private async Task<HttpResult> UpdatePropertiesAsync(
        string storeName,
        BlobStorageUpdatePropertiesRequestModel request,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        if (request is null)
        {
            return Results.Problem("Blob property update request is required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var update = new BlobPropertiesUpdate
        {
            Key = new BlobKey(request.Container, request.Name),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? null
                : ContentTypeExtensions.FromMimeType(request.ContentType, ContentType.DEFAULT),
            ExpiresAt = request.ExpiresAt,
            IfMatchETag = request.IfMatchETag,
            Properties = new PropertyBag(request.Properties ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        };

        var result = await client.UpdatePropertiesAsync(update, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(MapInfo(result.Value))
            : MapFailure(result, $"{request.Container}/{request.Name}");
    }

    private async Task<HttpResult> ListPageAsync(
        string storeName,
        string container,
        string prefix,
        int? take,
        string continuationToken,
        bool? allowFullScan,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        var result = await client.ListPageAsync(
            new BlobQuery
            {
                Container = container,
                Prefix = prefix,
                Take = take,
                ContinuationToken = continuationToken,
                AllowFullScan = allowFullScan.GetValueOrDefault()
            },
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(new BlobStorageBlobPageModel
            {
                Items = result.Value.Items.Select(MapInfo).ToArray(),
                ContinuationToken = result.Value.ContinuationToken,
                HasMore = result.Value.HasMore
            })
            : MapFailure(result, container);
    }

    private async Task<HttpResult> DeleteAsync(
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!this.TryCreateClient(storeName, out var client, out var failure))
        {
            return failure;
        }

        this.logger.LogInformation("Deleting blob through maintenance endpoint (store={StoreName})", storeName);
        var result = await client.DeleteAsync(
                new BlobKey(container, name),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok($"Blob '{container}/{name}' was deleted successfully using store '{storeName}'.")
            : MapFailure(result, $"{container}/{name}");
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

    private static BlobStorageClientInfoModel MapRegistration(BlobStoreClientRegistration registration) =>
        new()
        {
            Name = registration.Name,
            ProviderName = registration.ProviderName,
            Capabilities = registration.Capabilities
        };

    private static BlobStorageBlobInfoModel MapInfo(BlobInfo info) =>
        new()
        {
            Container = info.Key?.Container,
            Name = info.Key?.Name,
            Length = info.Length,
            ContentType = info.ContentType?.MimeType(),
            ContentHash = info.ContentHash,
            ETag = info.ETag,
            CreatedAt = info.CreatedAt,
            LastModifiedAt = info.LastModifiedAt,
            ExpiresAt = info.ExpiresAt,
            Properties = info.Properties?.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };

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
            BlobStoreQueryTooBroadError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStorePageSizeExceededError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreInvalidContinuationTokenError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreQueryNotSupportedError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreConflictError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            BlobStoreLeaseError => Results.Problem(message, statusCode: (int)HttpStatusCode.Locked),
            BlobStoreIntegrityError => Results.Problem(message, statusCode: (int)HttpStatusCode.Conflict),
            BlobStoreSizeLimitExceededError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestEntityTooLarge),
            BlobStoreTimeoutError => Results.Problem(message, statusCode: (int)HttpStatusCode.RequestTimeout),
            _ => Results.Problem(message, statusCode: (int)HttpStatusCode.InternalServerError)
        };
    }

    private static bool IsNotFound(Result result) =>
        result.Errors?.Any(error => error is BlobStoreNotFoundError) == true;
}
