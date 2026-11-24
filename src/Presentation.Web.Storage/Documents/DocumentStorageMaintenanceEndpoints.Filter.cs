// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class DocumentStorageMaintenanceEndpoints
{
    private static void MapFilter(RouteGroupBuilder group) =>
        Describe(group.MapPost("{clientId}/search", (string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKeyPrefix,
                [FromQuery] bool? allowFullScan, HttpRequest request, [FromServices] IDocumentStoreClientFactory factory, CancellationToken token) =>
                ListFilteredAsync(factory, clientId, new(partitionKey, rowKeyPrefix, allowFullScan ?? false), request, token)),
                "FilterSearch", "Filter document metadata by key and Dimensions")
            .Accepts<StorageFilterRequest>("application/json")
            .Produces<DocumentStorageDocumentInfoPageModel>()
            .WithStorageFilterSchema("PartitionKey, RowKey");

    private static async Task<HttpResult> ListFilteredAsync(IDocumentStoreClientFactory factory, string clientId,
        DocumentFilterScope scope, HttpRequest request, CancellationToken token)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        var model = await StorageFilterRequest.ReadAsync(request, token).ConfigureAwait(false);
        if (model is null) return Results.Problem("A valid filter model body is required.", statusCode: StatusCodes.Status400BadRequest);
        var result = await client.ListPageAsync(scope, model, model.ContinuationToken, token).ConfigureAwait(false);
        return result.IsFailure ? MapFailure(result) : Results.Ok(new DocumentStorageDocumentInfoPageModel
        {
            Items = [.. result.Value.Items.Select(MapInfo)],
            ContinuationToken = result.Value.ContinuationToken
        });
    }
}
