// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using Application.Storage;
using Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class DocumentStorageMaintenanceEndpoints
{
    private void MapDimensions(RouteGroupBuilder group)
    {
        group.MapPatch("{clientId}/documents/dimensions", (HttpContext context, string clientId, DocumentStorageUpdateDimensionsRequestModel request,
            [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            UpdateDimensionsAsync(factory, clientId, request, ifMatchETag, context, cancellationToken))
            .Accepts<DocumentStorageUpdateDimensionsRequestModel>("application/json")
            .Produces<DocumentStorageDocumentInfoModel>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("_bdk.Storage.Documents.Maintenance.UpdateDimensions")
            .WithSummary("Replace document Dimensions without replacing content");
    }

    private static async Task<HttpResult> UpdateDimensionsAsync(IDocumentStoreClientFactory factory, string clientId, DocumentStorageUpdateDimensionsRequestModel request,
        string ifMatchETag, HttpContext context, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return UnknownClient();
        if (request?.Dimensions is null) return BadRequest("Replacement Dimensions are required.");
        var condition = HttpETag.ReadCondition(ifMatchETag, request.IfMatchETag);
        if (condition.IsFailure) return MapFailure(condition);
        var result = await client.UpdateDimensionsAsync(new() { Key = new(request.PartitionKey, request.RowKey), Dimensions = request.Dimensions,
            IfMatchETag = condition.Value }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return MapFailure(result);
        HttpETag.Write(context.Response, result.Value.ETag);
        return Results.Ok(MapInfo(result.Value));
    }
}
