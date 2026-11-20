// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.Web.Storage;

using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Exposes read-only REST endpoints for retrieving typed Document Storage content.</summary>
/// <remarks>Enable this group with <c>AddReadEndpoints</c> or <c>AddDocumentStorageReadEndpoints</c>. Assembly discovery alone leaves it disabled.</remarks>
/// <example><code>services.AddDocumentStorage().AddReadEndpoints(options => options.RequireAuthorization());</code></example>
public sealed partial class DocumentStorageReadEndpoints(DocumentStorageReadEndpointsOptions options = null) : EndpointsBase
{
    private readonly DocumentStorageReadEndpointsOptions options = options ?? new() { Enabled = false };

    /// <summary>Maps an exact document-content GET route with the ETag of the returned snapshot.</summary>
    /// <example><code>app.MapEndpoints();</code></example>
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !this.options.Enabled) return;

        var group = this.MapGroup(app, this.options).DisableAntiforgery();
        group.MapGet("{clientId}/documents", (HttpContext context, string clientId, [FromQuery] string partitionKey, [FromQuery] string rowKey, [FromServices] IDocumentStoreClientFactory factory, CancellationToken cancellationToken) =>
            ReadAsync(factory, clientId, new(partitionKey, rowKey), context, cancellationToken))
            .Produces<JsonElement>(contentType: "application/json")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status408RequestTimeout)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .WithName("_bdk.Storage.Documents.Read.Get")
            .WithSummary("Read document content")
            .WithDescription("Returns one typed document as JSON with its whole-item ETag, through the registered client pipeline.");

        this.MapDimensions(group);

        this.IsRegistered = true;
    }

    private static async Task<HttpResult> ReadAsync(IDocumentStoreClientFactory factory, string clientId, DocumentKey key, HttpContext context, CancellationToken cancellationToken)
    {
        var client = factory.Create(clientId);
        if (client is null) return Results.Problem("The document storage client is not registered.", statusCode: StatusCodes.Status404NotFound);

        var result = await client.GetEntryJsonAsync(key, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            var status = result.Errors.FirstOrDefault() switch
            {
                DocumentStoreNotFoundError => StatusCodes.Status404NotFound,
                ValidationError or DocumentStoreInvalidQueryError => StatusCodes.Status400BadRequest,
                DocumentStoreTimeoutError => StatusCodes.Status408RequestTimeout,
                _ => StatusCodes.Status500InternalServerError
            };
            return Results.Problem(string.Join("; ", result.Errors.Select(error => error.Message)), statusCode: status);
        }

        HttpETag.Write(context.Response, result.Value.Info.ETag);
        return Results.Content(result.Value.Content, "application/json");
    }
}
