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

public sealed partial class BlobStorageMaintenanceEndpoints
{
    private static void MapFilter(RouteGroupBuilder group) =>
        group.MapPost("{storeName}/search", (string storeName, [FromQuery] string container, [FromQuery] string prefix,
                [FromQuery] bool? allowFullScan, HttpRequest request, [FromServices] IBlobStoreClientFactory factory, CancellationToken token) =>
                ListFilteredAsync(factory, storeName, new(container, prefix, allowFullScan ?? false), request, token))
            .Accepts<StorageFilterRequest>("application/json")
            .Produces<BlobStorageBlobPageModel>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName($"{RouteNamePrefix}.FilterSearch")
            .WithSummary("Filter blob metadata by name and Dimensions")
            .WithStorageFilterSchema("Container, Name");

    private static async Task<HttpResult> ListFilteredAsync(IBlobStoreClientFactory factory, string storeName,
        BlobFilterScope scope, HttpRequest request, CancellationToken token)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure)) return failure;
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        var model = await StorageFilterRequest.ReadAsync(request, token).ConfigureAwait(false);
        if (model is null) return Results.Problem("A valid filter model body is required.", statusCode: StatusCodes.Status400BadRequest);
        var result = await client.ListPageAsync(scope, model, model.ContinuationToken, token).ConfigureAwait(false);
        return result.IsFailure ? MapFailure(result, "blob filter query") : Results.Ok(new BlobStorageBlobPageModel
        {
            Items = [.. result.Value.Items.Select(MapInfo)],
            ContinuationToken = result.Value.ContinuationToken,
            HasMore = result.Value.HasMore
        });
    }
}
