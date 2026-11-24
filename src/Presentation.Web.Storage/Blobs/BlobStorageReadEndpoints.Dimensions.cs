// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using Application.Storage;
using Common;
using Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

public sealed partial class BlobStorageReadEndpoints
{
    private void MapDimensions(RouteGroupBuilder group)
    {
        group.MapPost("{storeName}/blobs/search", (string storeName, BlobQuery request, [FromServices] IBlobStoreClientFactory factory, CancellationToken cancellationToken) =>
            SearchDimensionsAsync(factory, storeName, request, cancellationToken))
            .Accepts<BlobQuery>("application/json")
            .Produces<BlobStorageBlobPageModel>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .WithName($"{RouteNamePrefix}.Search")
            .WithSummary("Search blob keys and Dimensions");
    }

    private static async Task<HttpResult> SearchDimensionsAsync(IBlobStoreClientFactory factory, string storeName, BlobQuery request, CancellationToken cancellationToken)
    {
        if (!TryCreateClient(factory, storeName, out var client, out var failure)) return failure;
        var result = await client.ListPageAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Ok(new BlobStorageBlobPageModel { Items = result.Value.Items.Select(MapInfo).ToArray(), ContinuationToken = result.Value.ContinuationToken })
            : MapFailure(result, "search");
    }

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

}
