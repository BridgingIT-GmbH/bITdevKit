// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps the independently enabled external cache-resource API through bounded common Application requests.</summary>
/// <example><code>services.AddApplicationCaching().WithEndpoints(); app.MapEndpoints();</code></example>
public sealed class CacheEndpoints(CacheWebConfiguration configuration, CacheHttpRequestReader reader, CacheCatalog catalog) : EndpointsBase
{
    /// <inheritdoc />
    public override void Map(IEndpointRouteBuilder app)
    {
        if (!this.Enabled || !configuration.Options.Enabled) { return; }

        reader.ValidateProfiles(catalog);
        var group = this.MapGroup(app, configuration.Options).DisableAntiforgery().AddEndpointFilter<CacheExposureEndpointFilter>();
        this.Describe(group.MapGet("/collections", CollectionsAsync), "Collections", "List exposed declared cache collections", CacheAccessActions.Read);
        this.Describe(group.MapGet("/{collection}/entries", ReadAsync), "Get", "Stream one valid cached representation without loading", CacheAccessActions.Read)
            .Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status206PartialContent).Produces(StatusCodes.Status304NotModified)
            .Produces(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status412PreconditionFailed).ProducesProblem(StatusCodes.Status416RangeNotSatisfiable);
        this.Describe(group.MapPut("/{collection}/entries", WriteAsync), "Set", "Conditionally publish one declared cache representation", CacheAccessActions.Write)
            .Accepts<Stream>("application/octet-stream", "application/json").Produces(StatusCodes.Status200OK);
        this.Describe(group.MapDelete("/{collection}/entries", RemoveAsync), "Remove", "Conditionally remove an exact key and fence prior publication", CacheAccessActions.Invalidate)
            .Produces(StatusCodes.Status204NoContent);
        this.Describe(group.MapGet("/{collection}/metadata", MetadataAsync), "Metadata", "Read one authoritative compatible snapshot without payload bytes", CacheAccessActions.Read);
        this.Describe(group.MapGet("/{collection}/metadata/entries", EntriesAsync), "Entries", "Browse one bounded redacted metadata page", CacheAccessActions.Read);
        this.Describe(group.MapPost("/{collection}/invalidate/preview", PreviewAsync), "Preview", "Preview a bounded metadata invalidation selection", CacheAccessActions.Read);
        this.Describe(group.MapPost("/{collection}/invalidate", InvalidateAsync), "Invalidate", "Invalidate selected entries with explicit background acceptance", CacheAccessActions.Invalidate)
            .Produces(StatusCodes.Status202Accepted);
        this.Describe(group.MapPost("/{collection}/clear", ClearAsync), "Clear", "Explicitly clear the logical collection with bounded tracked progress", CacheAccessActions.Invalidate)
            .Produces(StatusCodes.Status202Accepted);
        this.Describe(group.MapPost("/invalidate/preview", MultiPreviewAsync), "Multi.Preview", "Preview explicit exposed collection targets before mutation", CacheAccessActions.Read)
            .Accepts<CacheHttpInvalidationRequest>("application/json").Produces<CacheHttpPreviewResponse[]>();
        this.Describe(group.MapPost("/invalidate", MultiInvalidateAsync), "Multi.Invalidate", "Invalidate explicit exposed collection targets with per-scope outcomes", CacheAccessActions.Invalidate)
            .Accepts<CacheHttpInvalidationRequest>("application/json").Produces<CacheHttpOperationResponse[]>().Produces<CacheHttpOperationResponse[]>(StatusCodes.Status202Accepted);
        this.Describe(group.MapPost("/clear", MultiClearAsync), "Multi.Clear", "Explicitly clear selected exposed collections with per-scope outcomes", CacheAccessActions.Invalidate)
            .Accepts<CacheHttpInvalidationRequest>("application/json").Produces<CacheHttpOperationResponse[]>().Produces<CacheHttpOperationResponse[]>(StatusCodes.Status202Accepted);
        this.Describe(group.MapGet("/{collection}/operations/{operationId}", StatusAsync), "Operation.Status", "Read retained invalidation progress", CacheAccessActions.Read);
        this.Describe(group.MapPost("/{collection}/operations/{operationId}/resume", ResumeAsync), "Operation.Resume", "Continue the recorded invalidation remainder", CacheAccessActions.Invalidate);
        this.Describe(group.MapPost("/{collection}/operations/{operationId}/cancel", CancelAsync), "Operation.Cancel", "Cancel remaining work at a safe boundary", CacheAccessActions.Invalidate);
        this.IsRegistered = true;
    }

    private RouteHandlerBuilder Describe(RouteHandlerBuilder route, string name, string summary, CacheAccessActions action)
    {
        var policy = action switch
        {
            CacheAccessActions.Write => configuration.Options.WritePolicy,
            CacheAccessActions.Invalidate => configuration.Options.InvalidationPolicy,
            _ => configuration.Options.ReadPolicy
        };
        if (string.IsNullOrWhiteSpace(policy)) { route.RequireAuthorization(); }
        else { route.RequireAuthorization(policy); }

        return route.WithName("Application.Caching." + name).WithSummary(summary).WithDescription(summary).WithMetadata(new CacheHttpActionMetadata(action))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable).ProducesProblem(StatusCodes.Status412PreconditionFailed);
    }

    private static Task<HttpResult> ReadAsync(HttpContext context, [FromRoute] string collection, [FromQuery] string key,
        [FromServices] CacheHttpResultMapper mapper, [FromServices] IRequester requester, CancellationToken ct) =>
        mapper.ReadAsync(context, collection, key, requester, ct);

    private static Task<HttpResult> WriteAsync(HttpContext context, [FromRoute] string collection, [FromQuery] string key,
        [FromServices] CacheHttpResultMapper mapper, [FromServices] IRequester requester, CancellationToken ct) =>
        mapper.WriteAsync(context, collection, key, requester, ct);

    private static Task<HttpResult> RemoveAsync(HttpContext context, [FromRoute] string collection, [FromQuery] string key,
        [FromServices] CacheHttpResultMapper mapper, [FromServices] IRequester requester, CancellationToken ct) =>
        mapper.RemoveAsync(context, collection, key, requester, ct);

    private static async Task<HttpResult> CollectionsAsync(HttpContext context, [FromServices] IRequester requester,
        [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheWebConfiguration configuration, CancellationToken ct)
    {
        var result = await requester.SendAsync(new CacheCollectionsQuery(access.Create(context, CacheAccessActions.Read)), cancellationToken: ct).ConfigureAwait(false);
        return result.IsFailure ? CacheHttpResultMapper.Failure(result) : Results.Ok(result.Value.Where(collection => configuration.IsExposed(collection.Name)).ToArray());
    }

    private static async Task<HttpResult> MetadataAsync(HttpContext context, [FromRoute] string collection, [FromQuery] string key,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, CancellationToken ct)
    {
        var result = await requester.SendAsync(new CacheEntryMetadataQuery(access.Create(context, CacheAccessActions.Read), collection, key), cancellationToken: ct).ConfigureAwait(false);
        return result.IsFailure ? CacheHttpResultMapper.Failure(result) : result.Value.Value is null ? Results.NotFound() : Results.Ok(CacheHttpEntryMetadata.From(result.Value.Value));
    }

    private static async Task<HttpResult> EntriesAsync(HttpContext context, [FromRoute] string collection, [FromQuery] string prefix,
        [FromQuery] int? take, [FromQuery] string continuation, [FromQuery] DateTimeOffset? cutoff, [FromServices] IRequester requester,
        [FromServices] ICacheHttpAccessContextFactory access, CancellationToken ct)
    {
        try
        {
            var result = await requester.SendAsync(new CacheEntriesQuery(access.Create(context, CacheAccessActions.Read), collection,
                string.IsNullOrEmpty(prefix) ? null : CacheSelection.Prefix(prefix), take ?? 100, continuation, cutoff), cancellationToken: ct).ConfigureAwait(false);
            return result.IsFailure ? CacheHttpResultMapper.Failure(result) : Result<CacheHttpEntriesResponse>.Success(new(
                result.Value.Entries.Select(CacheHttpEntryMetadata.From).ToArray(), result.Value.Count, result.Value.CountIsExact,
                result.Value.Completed, result.Value.Continuation, result.Value.Cutoff, result.Value.ObservedAt)).MapHttpOk();
        }
        catch (ArgumentException)
        {
            return CacheHttpResultMapper.Failure(Result.Failure(new CacheError(CacheErrorCode.Validation, "The metadata prefix is invalid.")));
        }
    }

    private static async Task<HttpResult> PreviewAsync(HttpContext context, [FromRoute] string collection,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader, CancellationToken ct)
    {
        var body = await reader.ReadInvalidationAsync(context.Request, ct).ConfigureAwait(false);
        if (body.IsFailure) { return CacheHttpResultMapper.Failure(body); }

        var result = await requester.SendAsync(new CacheInvalidationPreviewQuery(access.Create(context, CacheAccessActions.Read), collection,
            body.Value.Selection ?? CacheSelection.None), cancellationToken: ct).ConfigureAwait(false);
        return result.IsFailure ? CacheHttpResultMapper.Failure(result) : Result<CacheHttpPreviewResponse>.Success(new(collection,
            result.Value.Count, result.Value.CountIsExact, result.Value.Continuation is null, result.Value.Continuation,
            result.Value.Sample.Select(CacheHttpEntryMetadata.From).ToArray())).MapHttpOk();
    }

    private static Task<HttpResult> InvalidateAsync(HttpContext context, [FromRoute] string collection, [FromServices] IRequester requester,
        [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader, CancellationToken ct) =>
        SubmitAsync(context, collection, false, requester, access, reader, ct);

    private static Task<HttpResult> ClearAsync(HttpContext context, [FromRoute] string collection, [FromServices] IRequester requester,
        [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader, CancellationToken ct) =>
        SubmitAsync(context, collection, true, requester, access, reader, ct);

    private static async Task<HttpResult> SubmitAsync(HttpContext context, string collection, bool clear, IRequester requester,
        ICacheHttpAccessContextFactory access, CacheHttpRequestReader reader, CancellationToken ct)
    {
        var body = await reader.ReadInvalidationAsync(context.Request, ct).ConfigureAwait(false);
        if (body.IsFailure) { return CacheHttpResultMapper.Failure(body); }

        var result = await requester.SendAsync(new CacheInvalidateCommand(access.Create(context, CacheAccessActions.Invalidate),
            [collection], body.Value.Selection ?? CacheSelection.None, clear, body.Value.Background), cancellationToken: ct).ConfigureAwait(false);
        var root = context.Request.Path.Value![..context.Request.Path.Value!.LastIndexOf('/')];
        return CacheHttpResultMapper.Submission(result, handle => context.Request.PathBase.ToString() + root + "/operations/" + Uri.EscapeDataString(handle.Id), [collection]);
    }

    private static async Task<HttpResult> MultiPreviewAsync(HttpContext context, [FromQuery] string[] collections,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader, CancellationToken ct)
    {
        var body = await reader.ReadInvalidationAsync(context.Request, ct).ConfigureAwait(false);
        if (body.IsFailure) { return CacheHttpResultMapper.Failure(body); }

        var responses = new List<CacheHttpPreviewResponse>();
        foreach (var collection in collections)
        {
            var result = await requester.SendAsync(new CacheInvalidationPreviewQuery(access.Create(context, CacheAccessActions.Read), collection,
                body.Value.Selection ?? CacheSelection.None), cancellationToken: ct).ConfigureAwait(false);
            responses.Add(result.IsSuccess ? new(collection, result.Value.Count, result.Value.CountIsExact, result.Value.Continuation is null,
                result.Value.Continuation, []) : new(collection, null, false, false, null, [], result.Errors.FirstOrDefault()?.Message));
        }

        return Results.Ok(responses);
    }

    private static Task<HttpResult> MultiInvalidateAsync(HttpContext context, [FromQuery] string[] collections,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader,
        [FromServices] CacheWebConfiguration configuration, CancellationToken ct) => MultiSubmitAsync(context, collections, false, requester, access, reader, configuration, ct);

    private static Task<HttpResult> MultiClearAsync(HttpContext context, [FromQuery] string[] collections,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheHttpRequestReader reader,
        [FromServices] CacheWebConfiguration configuration, CancellationToken ct) => MultiSubmitAsync(context, collections, true, requester, access, reader, configuration, ct);

    private static async Task<HttpResult> MultiSubmitAsync(HttpContext context, IReadOnlyList<string> collections, bool clear, IRequester requester,
        ICacheHttpAccessContextFactory access, CacheHttpRequestReader reader, CacheWebConfiguration configuration, CancellationToken ct)
    {
        var body = await reader.ReadInvalidationAsync(context.Request, ct).ConfigureAwait(false);
        if (body.IsFailure) { return CacheHttpResultMapper.Failure(body); }

        var result = await requester.SendAsync(new CacheInvalidateCommand(access.Create(context, CacheAccessActions.Invalidate),
            collections, body.Value.Selection ?? CacheSelection.None, clear, body.Value.Background), cancellationToken: ct).ConfigureAwait(false);
        return CacheHttpResultMapper.Submission(result, handle => context.Request.PathBase.ToString() + configuration.Options.GroupPath.TrimEnd('/') + "/" +
            Uri.EscapeDataString(handle.Collection) + "/operations/" + Uri.EscapeDataString(handle.Id), collections);
    }

    private static Task<HttpResult> StatusAsync(HttpContext context, [FromRoute] string collection, [FromRoute] string operationId,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheCatalog catalog, CancellationToken ct) =>
        OperationAsync(context, collection, operationId, CacheOperationAction.Status, requester, access, catalog, ct);

    private static Task<HttpResult> ResumeAsync(HttpContext context, [FromRoute] string collection, [FromRoute] string operationId,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheCatalog catalog, CancellationToken ct) =>
        OperationAsync(context, collection, operationId, CacheOperationAction.Resume, requester, access, catalog, ct);

    private static Task<HttpResult> CancelAsync(HttpContext context, [FromRoute] string collection, [FromRoute] string operationId,
        [FromServices] IRequester requester, [FromServices] ICacheHttpAccessContextFactory access, [FromServices] CacheCatalog catalog, CancellationToken ct) =>
        OperationAsync(context, collection, operationId, CacheOperationAction.Cancel, requester, access, catalog, ct);

    private static async Task<HttpResult> OperationAsync(HttpContext context, string collection, string operationId, CacheOperationAction action,
        IRequester requester, ICacheHttpAccessContextFactory access, CacheCatalog catalog, CancellationToken ct)
    {
        var result = await requester.SendAsync(new CacheOperationCommand(access.Create(context,
            action == CacheOperationAction.Status ? CacheAccessActions.Read : CacheAccessActions.Invalidate),
            new(catalog.Collections[collection].Provider.Name, collection, operationId), action), cancellationToken: ct).ConfigureAwait(false);
        return result.IsFailure ? CacheHttpResultMapper.Failure(result) : Result<CacheHttpOperationStatus>.Success(CacheHttpOperationStatus.From(result.Value)).MapHttpOk();
    }
}