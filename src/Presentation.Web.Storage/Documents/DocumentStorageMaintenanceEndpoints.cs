// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Exposes registered typed document clients through protected maintenance REST endpoints.</summary>
/// <remarks>Enable this group with <c>AddMaintenanceEndpoints</c> or <c>AddDocumentStorageMaintenanceEndpoints</c>. Assembly discovery alone leaves it disabled.</remarks>
/// <example><code>services.AddDocumentStorage().AddMaintenanceEndpoints(options => options.RequireAuthorization());</code></example>
public sealed partial class DocumentStorageMaintenanceEndpoints(DocumentStorageMaintenanceEndpointsOptions options = null) : EndpointsBase
{
    private const string RouteNamePrefix = "_bdk.Storage.Documents.Maintenance";
    private readonly DocumentStorageMaintenanceEndpointsOptions options = options ?? new() { Enabled = false };

    /// <summary>Maps document metadata reads and conditional content and property mutations.</summary>
    /// <example><code>app.MapEndpoints();</code></example>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled) return;

        var group = this.MapGroup(app, this.options).DisableAntiforgery();

        group.MapGet("clients", ([FromServices] IDocumentStoreClientFactory factory) =>
            Results.Ok(factory.GetDescriptors().Select(descriptor => new DocumentStorageClientInfoModel
            {
                ClientId = descriptor.ClientId,
                Name = descriptor.Name,
                DocumentTypeName = descriptor.DocumentTypeName,
                ProviderName = descriptor.ProviderName,
                Capabilities = descriptor.Capabilities
            }).OrderBy(item => item.ClientId, StringComparer.Ordinal).ToArray()))
            .Produces<DocumentStorageClientInfoModel[]>()
            .WithName($"{RouteNamePrefix}.ListClients")
            .WithSummary("List registered document storage clients");

        Describe(group.MapGet("{clientId}/documents/properties", (HttpContext context, string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            GetPropertiesAsync(factory, clientId, new(partitionKey, rowKey), context, cancellationToken)), "GetProperties", "Read document properties")
            .Produces<DocumentStorageDocumentInfoModel>();

        Describe(group.MapGet("{clientId}/documents/exists", (string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            ExistsAsync(factory, clientId, new(partitionKey, rowKey), cancellationToken)), "Exists", "Check exact document existence")
            .Produces<bool>();

        Describe(group.MapGet("{clientId}/documents/keys", (string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKeyPrefix, [FromQuery] int? take, [FromQuery] string continuationToken, [FromQuery] bool? allowFullScan, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            ListKeysAsync(factory, clientId, partitionKey, rowKeyPrefix, take, continuationToken, allowFullScan, cancellationToken)), "ListKeys", "List a bounded page of document keys")
            .Produces<DocumentKeyPage>();

        Describe(group.MapPost("{clientId}/documents", (HttpContext context, string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, DocumentStorageWriteRequestModel request, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            WriteAsync(factory, clientId, new(partitionKey, rowKey), request, ifMatchETag, context, true, cancellationToken)), "Create", "Create a document")
            .Accepts<DocumentStorageWriteRequestModel>("application/json")
            .Produces<DocumentStorageDocumentInfoModel>(StatusCodes.Status201Created)
            .WithDescription("Creates only when no physical document exists. If-Match cannot be supplied for creation.");

        Describe(group.MapPut("{clientId}/documents", (HttpContext context, string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, DocumentStorageWriteRequestModel request, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            WriteAsync(factory, clientId, new(partitionKey, rowKey), request, ifMatchETag, context, false, cancellationToken)), "Upsert", "Create or replace document content")
            .Accepts<DocumentStorageWriteRequestModel>("application/json")
            .Produces<DocumentStorageDocumentInfoModel>()
            .WithDescription("Optional If-Match requires the caller's observed version. Omission permits unconditional creation or replacement. Omitted properties and expiration are preserved.");

        Describe(group.MapPatch("{clientId}/documents/properties", (HttpContext context, string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, DocumentStorageUpdatePropertiesRequestModel request, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            UpdatePropertiesAsync(factory, clientId, new(partitionKey, rowKey), request, ifMatchETag, context, cancellationToken)), "UpdateProperties", "Update document properties without replacing content")
            .Accepts<DocumentStorageUpdatePropertiesRequestModel>("application/json")
            .Produces<DocumentStorageDocumentInfoModel>();

        Describe(group.MapDelete("{clientId}/documents", (string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            DeleteAsync(factory, clientId, new(partitionKey, rowKey), ifMatchETag, cancellationToken)), "Delete", "Delete a document with an optional ETag condition")
            .Produces(StatusCodes.Status200OK);

        this.MapDimensions(group);

        this.IsRegistered = true;
    }

    private static RouteHandlerBuilder Describe(RouteHandlerBuilder route, string name, string summary) => route
        .WithName($"{RouteNamePrefix}.{name}")
        .WithSummary(summary)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
        .Produces<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)
        .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

    private static async Task<HttpResult> GetPropertiesAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, HttpContext context, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        var result = await client.GetEntryJsonAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return MapFailure(result);
        HttpETag.Write(context.Response, result.Value.Info.ETag);
        return Results.Ok(MapInfo(result.Value.Info));
    }

    private static async Task<HttpResult> ExistsAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        var result = await client.ExistsAsync(key, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : MapFailure(result);
    }

    private static async Task<HttpResult> ListKeysAsync(IDocumentStoreClientFactory factory, string clientId, string partitionKey, string prefix, int? take, string token, bool? allowFullScan, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        var result = await client.ListPageAsync(new DocumentQuery
        {
            DocumentKey = partitionKey is null && prefix is null ? null : new(partitionKey, prefix ?? string.Empty),
            Filter = DocumentKeyFilter.RowKeyPrefixMatch,
            Take = take,
            ContinuationToken = token,
            AllowFullScan = allowFullScan.GetValueOrDefault()
        }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : MapFailure(result);
    }

    private static async Task<HttpResult> WriteAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, DocumentStorageWriteRequestModel request, string ifMatchETag, HttpContext context, bool createOnly, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        if (createOnly && context.Request.Headers.ContainsKey("If-Match")) return BadRequest("If-Match cannot be supplied for create-only writes.");
        if (request is null || request.Content.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return BadRequest("Document content is required.");

        var condition = HttpETag.ReadCondition(ifMatchETag);
        if (condition.IsFailure) return MapFailure(condition);
        var properties = HttpMetadata.ParseProperties(JsonSerializer.Serialize(request.Properties), preserveMissing: true);
        if (properties.IsFailure) return MapFailure(properties);
        var expiration = HttpMetadata.ResolveExpiration(request.ExpiresAt, request.ClearExpiration);
        if (expiration.IsFailure) return MapFailure(expiration);

        var result = await client.UpsertEntryJsonAsync(key, request.Content.GetRawText(), new DocumentWriteOptions
        {
            CreateOnly = createOnly,
            IfMatchETag = condition.Value,
            Properties = properties.Value,
            Dimensions = request.Dimensions,
            Expiration = expiration.Value
        }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return MapFailure(result);
        HttpETag.Write(context.Response, result.Value.ETag);
        return createOnly ? Results.Created($"{context.Request.PathBase}{context.Request.Path}/properties{context.Request.QueryString}", MapInfo(result.Value)) : Results.Ok(MapInfo(result.Value));
    }

    private static async Task<HttpResult> UpdatePropertiesAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, DocumentStorageUpdatePropertiesRequestModel request, string ifMatchETag, HttpContext context, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        if (request is null) return BadRequest("A properties update is required.");
        var condition = HttpETag.ReadCondition(ifMatchETag);
        if (condition.IsFailure) return MapFailure(condition);
        var properties = HttpMetadata.ParseProperties(JsonSerializer.Serialize(request.Properties), preserveMissing: true);
        if (properties.IsFailure) return MapFailure(properties);
        var expiration = HttpMetadata.ResolveExpiration(request.ExpiresAt, request.ClearExpiration);
        if (expiration.IsFailure) return MapFailure(expiration);

        var result = await client.UpdatePropertiesAsync(new DocumentPropertiesUpdate(key)
        {
            IfMatchETag = condition.Value,
            Properties = properties.Value,
            Expiration = expiration.Value
        }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return MapFailure(result);
        HttpETag.Write(context.Response, result.Value.ETag);
        return Results.Ok(MapInfo(result.Value));
    }

    private static async Task<HttpResult> DeleteAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, string ifMatchETag, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        var condition = HttpETag.ReadCondition(ifMatchETag);
        if (condition.IsFailure) return MapFailure(condition);
        var result = await client.DeleteAsync(key, new DocumentDeleteOptions { IfMatchETag = condition.Value }, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok() : MapFailure(result);
    }

    private static DocumentStorageDocumentInfoModel MapInfo(DocumentInfo info) => new()
    {
        Key = info.Key,
        Dimensions = info.Dimensions,
        ETag = info.ETag,
        ContentHash = info.ContentHash,
        ContentLength = info.ContentLength,
        CreatedAt = info.CreatedAt,
        LastModifiedAt = info.LastModifiedAt,
        ExpiresAt = info.ExpiresAt,
        Properties = info.Properties?.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase) ?? new(StringComparer.OrdinalIgnoreCase)
    };

    private static HttpResult UnknownClient() => Results.Problem("The document storage client is not registered.", statusCode: StatusCodes.Status404NotFound);

    private static HttpResult BadRequest(string detail) => Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest);

    private static HttpResult MapFailure(BridgingIT.DevKit.Common.IResult result)
    {
        var status = result.Errors.FirstOrDefault() switch
        {
            DocumentStoreNotFoundError => StatusCodes.Status404NotFound,
            DocumentStoreConflictError => StatusCodes.Status409Conflict,
            DocumentStoreSizeLimitError => StatusCodes.Status413PayloadTooLarge,
            DocumentStoreTimeoutError => StatusCodes.Status408RequestTimeout,
            ValidationError or DocumentDimensionsNotSupportedError or DocumentStoreInvalidQueryError or DocumentStorePageSizeExceededError or
                DocumentStoreFullScanNotAllowedError or DocumentStoreQueryNotSupportedError or
                DocumentStoreClientSideFilteringRejectedError or DocumentStoreInvalidContinuationTokenError or
                DocumentStoreContinuationTokenQueryMismatchError => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        return Results.Problem(string.Join("; ", result.Errors.Select(error => error.Message)), statusCode: status);
    }
}
