// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using Application.Storage;
using Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class DocumentStorageReadEndpoints
{
    private void MapDimensions(RouteGroupBuilder group)
    {
        group.MapPost("{clientId}/documents/search", (string clientId, DocumentQuery request, [FromServices] IDocumentStoreClientFactory factory, CancellationToken token) =>
            SearchDimensionsAsync(factory, clientId, request, token))
            .Accepts<DocumentQuery>("application/json").Produces<DocumentJsonPage>()
            .WithName("_bdk.Storage.Documents.Read.Search").WithSummary("Search document keys and Dimensions");
        group.MapPost("{clientId}/documents/keys", (string clientId, DocumentQuery request, [FromServices] IDocumentStoreClientFactory factory, CancellationToken token) =>
            DimensionKeysAsync(factory, clientId, request, token))
            .Accepts<DocumentQuery>("application/json").Produces<DocumentKeyPage>()
            .WithName("_bdk.Storage.Documents.Read.Keys").WithSummary("Search document keys without reading payloads");
        group.MapPost("{clientId}/documents/count", (string clientId, DocumentCountQuery request, [FromServices] IDocumentStoreClientFactory factory, CancellationToken token) =>
            DimensionCountAsync(factory, clientId, request, token))
            .Accepts<DocumentCountQuery>("application/json").Produces<long>()
            .WithName("_bdk.Storage.Documents.Read.Count").WithSummary("Count documents matching keys and Dimensions");
    }

    private static async Task<HttpResult> SearchDimensionsAsync(IDocumentStoreClientFactory factory, string clientId, DocumentQuery request, CancellationToken token)
    {
        var client = factory.Create(clientId);
        if (client is null) return Results.NotFound();
        var result = await client.FindJsonPageAsync(request, token).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : MapDimensionFailure(result);
    }

    private static async Task<HttpResult> DimensionKeysAsync(IDocumentStoreClientFactory factory, string clientId, DocumentQuery request, CancellationToken token)
    {
        var client = factory.Create(clientId);
        if (client is null) return Results.NotFound();
        var result = await client.ListPageAsync(request, token).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : MapDimensionFailure(result);
    }

    private static async Task<HttpResult> DimensionCountAsync(IDocumentStoreClientFactory factory, string clientId, DocumentCountQuery request, CancellationToken token)
    {
        var client = factory.Create(clientId);
        if (client is null) return Results.NotFound();
        var result = await client.CountAsync(request, token).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(result.Value) : MapDimensionFailure(result);
    }

    private static HttpResult MapDimensionFailure(Common.IResult result)
    {
        var status = result.Errors.FirstOrDefault() switch
        {
            DocumentStoreNotFoundError => StatusCodes.Status404NotFound,
            DocumentStoreTimeoutError => StatusCodes.Status408RequestTimeout,
            ValidationError or DocumentDimensionsNotSupportedError or DocumentStoreInvalidQueryError or DocumentStorePageSizeExceededError or
                DocumentStoreFullScanNotAllowedError or DocumentStoreQueryNotSupportedError or DocumentStoreClientSideFilteringRejectedError or
                DocumentStoreInvalidContinuationTokenError or DocumentStoreContinuationTokenQueryMismatchError => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        return Results.Problem(string.Join("; ", result.Errors.Select(error => error.Message)), statusCode: status);
    }
}
