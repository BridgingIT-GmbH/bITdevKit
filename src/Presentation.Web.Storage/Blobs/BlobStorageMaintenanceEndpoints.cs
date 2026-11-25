// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Net;
using System.Text.Json;
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
/// Exposes content mutation and metadata REST endpoints for registered Blob Storage clients.
/// </summary>
/// <remarks>Enable this group with <c>AddMaintenanceEndpoints</c> or <c>AddBlobStorageMaintenanceEndpoints</c>. Assembly discovery alone leaves it disabled.</remarks>
/// <example>
/// <code>
/// services.AddBlobStorage()
///     .WithInMemoryClient("reports")
///     .AddMaintenanceEndpoints(options => options.RequireAuthorization());
/// </code>
/// </example>
public sealed partial class BlobStorageMaintenanceEndpoints(
    ILoggerFactory loggerFactory,
    BlobStorageMaintenanceEndpointsOptions options = null) : EndpointsBase
{
    private const string RouteNamePrefix = "_bdk.Storage.Blobs.Maintenance";
    private readonly ILogger<BlobStorageMaintenanceEndpoints> logger =
        loggerFactory?.CreateLogger<BlobStorageMaintenanceEndpoints>() ??
        NullLogger<BlobStorageMaintenanceEndpoints>.Instance;
    private readonly BlobStorageMaintenanceEndpointsOptions options = options ?? new BlobStorageMaintenanceEndpointsOptions { Enabled = false };

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

        MapFilter(group);

        group.MapGet("clients", ([FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            ListClientsAsync(factory, cancellationToken))
            .Produces<List<BlobStorageClientInfoModel>>()
            .WithName($"{RouteNamePrefix}.ListClients")
            .WithSummary("List registered blob storage clients")
            .WithDescription("Retrieves the registered Blob Storage clients and provider-neutral capabilities.");

        group.MapGet("{storeName}/provider", (string storeName, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            GetClientInfoAsync(factory, storeName, cancellationToken))
            .Produces<BlobStorageClientInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .WithName($"{RouteNamePrefix}.GetClientInfo")
            .WithSummary("Get blob storage client information")
            .WithDescription("Retrieves provider-neutral information about one configured Blob Storage client.");

        group.MapGet("{storeName}/blobs/exists", (string storeName, [FromQuery] string container, [FromQuery] string name, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.ExistsAsync(factory, storeName, container, name, cancellationToken))
            .Produces<BlobStorageExistsResponseModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Exists")
            .WithSummary("Check whether a blob exists")
            .WithDescription("Checks exact-key blob existence without downloading blob content.");

        group.MapGet("{storeName}/blobs/properties", (HttpContext context, string storeName, [FromQuery] string container, [FromQuery] string name, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.GetPropertiesAsync(factory, storeName, container, name, context, cancellationToken))
            .Produces<BlobStorageBlobInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.GetProperties")
            .WithSummary("Get blob properties")
            .WithDescription("Retrieves blob metadata without downloading blob content.");

        group.MapPost("{storeName}/blobs", (HttpContext context, string storeName, [FromQuery] string container, [FromQuery] string name, [FromQuery] DateTimeOffset? expiresAt, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromHeader(Name = "X-Storage-Properties")] string properties, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.UploadAsync(factory, storeName, container, name, expiresAt, ifMatchETag, properties, context, true, cancellationToken))
            .Accepts<Stream>("*/*")
            .Produces<BlobStorageBlobInfoModel>((int)HttpStatusCode.Created)
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.RequestEntityTooLarge)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Create")
            .WithSummary("Create a blob")
            .WithDescription("Streams the request body into a new blob. An existing key conflicts. If-Match is invalid for creation; X-Storage-Properties optionally supplies a JSON scalar-property object.");

        group.MapPut("{storeName}/blobs", (HttpContext context, string storeName, [FromQuery] string container, [FromQuery] string name, [FromQuery] DateTimeOffset? expiresAt, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromHeader(Name = "X-Storage-Properties")] string properties, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.UploadAsync(factory, storeName, container, name, expiresAt, ifMatchETag, properties, context, false, cancellationToken))
            .Accepts<Stream>("*/*")
            .Produces<BlobStorageBlobInfoModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.RequestEntityTooLarge)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Upsert")
            .WithSummary("Create or replace blob content")
            .WithDescription("Streams the request body into the blob. Optional If-Match requires the previously observed version; omission permits unconditional creation or replacement. X-Storage-Properties optionally supplies a JSON scalar-property object.");

        group.MapPatch("{storeName}/blobs/properties", (HttpContext context, string storeName, BlobStorageUpdatePropertiesRequestModel request, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.UpdatePropertiesAsync(factory, storeName, request, ifMatchETag, context, cancellationToken))
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

        group.MapGet("{storeName}/blobs", (string storeName, [FromQuery] string container, [FromQuery] string prefix, [FromQuery] int? take, [FromQuery] string continuationToken, [FromQuery] bool? allowFullScan, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.ListPageAsync(factory, storeName, container, prefix, take, continuationToken, allowFullScan, cancellationToken))
            .Produces<BlobStorageBlobPageModel>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.ListPage")
            .WithSummary("List blobs")
            .WithDescription("Lists one page of blob metadata without returning content streams.");

        group.MapDelete("{storeName}/blobs", (string storeName, [FromQuery] string container, [FromQuery] string name, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.DeleteAsync(factory, storeName, container, name, ifMatchETag, cancellationToken))
            .Produces<string>()
            .Produces<string>((int)HttpStatusCode.NotFound)
            .Produces<ProblemDetails>((int)HttpStatusCode.BadRequest)
            .Produces<ProblemDetails>((int)HttpStatusCode.Conflict)
            .Produces<ProblemDetails>((int)HttpStatusCode.Locked)
            .Produces<ProblemDetails>((int)HttpStatusCode.InternalServerError)
            .WithName($"{RouteNamePrefix}.Delete")
            .WithSummary("Delete a blob")
            .WithDescription("Deletes a blob by exact key. Deleting a missing blob follows the configured blob client contract.");

        this.MapDimensions(group);

        this.IsRegistered = true;
    }

    private static Task<List<BlobStorageClientInfoModel>> ListClientsAsync(IBlobStoreClientFactory factory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(factory.GetRegistrations()
            .Select(MapRegistration)
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static Task<HttpResult> GetClientInfoAsync(IBlobStoreClientFactory factory, string storeName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var registration = factory.GetRegistrations()
            .FirstOrDefault(item => string.Equals(item.Name, storeName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(registration is null
            ? Results.NotFound($"No blob storage client registered with name '{storeName}'.")
            : Results.Ok(MapRegistration(registration)));
    }

    private async Task<HttpResult> ExistsAsync(
        IBlobStoreClientFactory factory,
        string storeName,
        string container,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
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
        IBlobStoreClientFactory factory,
        string storeName,
        string container,
        string name,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
        {
            return failure;
        }

        var result = await client.GetPropertiesAsync(new BlobKey(container, name), cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return MapFailure(result, $"{container}/{name}");
        }

        HttpETag.Write(context.Response, result.Value.ETag);
        return Results.Ok(MapInfo(result.Value));
    }

    private async Task<HttpResult> UploadAsync(
        IBlobStoreClientFactory factory,
        string storeName,
        string container,
        string name,
        DateTimeOffset? expiresAt,
        string ifMatchETag,
        string propertiesJson,
        HttpContext context,
        bool createOnly,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
        {
            return failure;
        }

        if (createOnly && (ifMatchETag is not null || context.Request.Headers.ContainsKey("If-Match")))
        {
            return Results.Problem("If-Match cannot be supplied for create-only uploads.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var condition = HttpETag.ReadCondition(ifMatchETag);
        if (condition.IsFailure)
        {
            return MapFailure(condition, $"{container}/{name}");
        }

        BlobDimensions dimensions = null;
        if (context.Request.Headers.TryGetValue("X-Storage-Dimensions", out var rawDimensions))
        {
            if (DimensionsHeadersTooLarge(context))
                return Results.Problem("The request headers exceed the hosting limit. Reduce Dimensions metadata or configure a larger request-header limit.", statusCode: StatusCodes.Status431RequestHeaderFieldsTooLarge);
            var parsed = BlobStorageDimensionsJson.Parse(rawDimensions.ToString());
            if (parsed.IsFailure) return MapFailure(parsed, "dimensions");
            dimensions = parsed.Value;
        }

        var properties = HttpMetadata.ParseProperties(propertiesJson, preserveMissing: true);
        if (properties.IsFailure)
        {
            return MapFailure(properties, $"{container}/{name}");
        }

        var result = await client.UploadAsync(new BlobUpload
        {
            Key = new BlobKey(container, name),
            Content = context.Request.Body,
            ContentType = string.IsNullOrWhiteSpace(context.Request.ContentType)
                ? null
                : ContentTypeExtensions.FromMimeType(context.Request.ContentType.Split(';')[0].Trim(), ContentType.DEFAULT),
            Properties = properties.Value,
            Dimensions = dimensions,
            ExpiresAt = expiresAt,
            IfMatchETag = condition.Value,
            OverwriteMode = createOnly ? BlobOverwriteMode.FailIfExists : BlobOverwriteMode.Overwrite
        }, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return MapFailure(result, $"{container}/{name}");
        }

        HttpETag.Write(context.Response, result.Value.ETag);
        return createOnly
            ? Results.Created($"{context.Request.PathBase}{context.Request.Path}/properties?container={Uri.EscapeDataString(container)}&name={Uri.EscapeDataString(name)}", MapInfo(result.Value))
            : Results.Ok(MapInfo(result.Value));
    }

    private async Task<HttpResult> UpdatePropertiesAsync(
        IBlobStoreClientFactory factory,
        string storeName,
        BlobStorageUpdatePropertiesRequestModel request,
        string ifMatchETag,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
        {
            return failure;
        }

        if (request is null)
        {
            return Results.Problem("Blob property update request is required.", statusCode: (int)HttpStatusCode.BadRequest);
        }

        var condition = HttpETag.ReadCondition(ifMatchETag, request.IfMatchETag);
        if (condition.IsFailure)
        {
            return MapFailure(condition, $"{request.Container}/{request.Name}");
        }

        var properties = ScalarPropertyBagJson.ParseElement(JsonSerializer.SerializeToElement(request.Properties), preserveMissing: true);
        if (properties.IsFailure)
        {
            return MapFailure(properties, $"{request.Container}/{request.Name}");
        }

        var update = new BlobPropertiesUpdate
        {
            Key = new BlobKey(request.Container, request.Name),
            ContentType = string.IsNullOrWhiteSpace(request.ContentType)
                ? null
                : ContentTypeExtensions.FromMimeType(request.ContentType, ContentType.DEFAULT),
            ExpiresAt = request.ExpiresAt,
            IfMatchETag = condition.Value,
            Properties = properties.Value
        };

        var result = await client.UpdatePropertiesAsync(update, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return MapFailure(result, $"{request.Container}/{request.Name}");
        }

        HttpETag.Write(context.Response, result.Value.ETag);
        return Results.Ok(MapInfo(result.Value));
    }

    private async Task<HttpResult> ListPageAsync(
        IBlobStoreClientFactory factory,
        string storeName,
        string container,
        string prefix,
        int? take,
        string continuationToken,
        bool? allowFullScan,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
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
                Items = [.. result.Value.Items.Select(MapInfo)],
                ContinuationToken = result.Value.ContinuationToken,
                HasMore = result.Value.HasMore
            })
            : MapFailure(result, container);
    }

    private async Task<HttpResult> DeleteAsync(
        IBlobStoreClientFactory factory,
        string storeName,
        string container,
        string name,
        string ifMatchETag,
        CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure))
        {
            return failure;
        }

        var condition = HttpETag.ReadCondition(ifMatchETag);
        if (condition.IsFailure)
        {
            return MapFailure(condition, $"{container}/{name}");
        }

        this.logger.LogInformation("Deleting blob through maintenance endpoint (store={StoreName})", storeName);
        var result = await client.DeleteAsync(
                new BlobKey(container, name),
                new BlobDeleteOptions { IfMatchETag = condition.Value },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok($"Blob '{container}/{name}' was deleted successfully using store '{storeName}'.")
            : MapFailure(result, $"{container}/{name}");
    }

    private static bool TryCreateClient(IBlobStoreClientFactory factory, string storeName, out IBlobStoreClient client, out HttpResult failure)
    {
        try
        {
            client = factory.CreateClient(storeName);
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
            Dimensions = info.Dimensions,
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
            ValidationError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
            BlobStoreValidationError or BlobDimensionsNotSupportedError => Results.Problem(message, statusCode: (int)HttpStatusCode.BadRequest),
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
