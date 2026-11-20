// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using Application.Storage;
using Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class BlobStorageMaintenanceEndpoints
{
    private void MapDimensions(RouteGroupBuilder group)
    {
        group.MapPatch("{storeName}/blobs/dimensions", (HttpContext context, string storeName, BlobStorageUpdateDimensionsRequestModel request,
            [FromHeader(Name = "If-Match")] string ifMatchETag, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            this.UpdateDimensionsAsync(factory, storeName, request, ifMatchETag, context, cancellationToken))
            .Accepts<BlobStorageUpdateDimensionsRequestModel>("application/json")
            .Produces<BlobStorageBlobInfoModel>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName($"{RouteNamePrefix}.UpdateDimensions")
            .WithSummary("Replace blob Dimensions without uploading content");
    }

    private async Task<HttpResult> UpdateDimensionsAsync(IBlobStoreClientFactory factory, string storeName, BlobStorageUpdateDimensionsRequestModel request,
        string ifMatchETag, HttpContext context, CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure)) return failure;
        if (request?.Dimensions is null) return Results.Problem("Replacement Dimensions are required.", statusCode: StatusCodes.Status400BadRequest);
        var condition = HttpETag.ReadCondition(ifMatchETag, request.IfMatchETag);
        if (condition.IsFailure) return MapFailure(condition, "dimensions");
        var result = await client.UpdateDimensionsAsync(new() { Key = new(request.Container, request.Name), Dimensions = request.Dimensions,
            IfMatchETag = condition.Value }, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return MapFailure(result, "dimensions");
        HttpETag.Write(context.Response, result.Value.ETag);
        return Results.Ok(MapInfo(result.Value));
    }

    private static bool DimensionsHeadersTooLarge(HttpContext context)
    {
        var maximum = context.RequestServices?.GetService<IOptions<KestrelServerOptions>>()?.Value.Limits.MaxRequestHeadersTotalSize ?? 32768;
        var bytes = context.Request.Headers.Sum(header => Encoding.UTF8.GetByteCount(header.Key) + 4
            + header.Value.Sum(value => Encoding.UTF8.GetByteCount(value ?? string.Empty)));
        return bytes > maximum;
    }
}
