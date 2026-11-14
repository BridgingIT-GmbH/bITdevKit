// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Maps cache-resource HTTP protocol behavior through the common requester and shared Result infrastructure.</summary>
/// <example><code>return await mapper.ReadAsync(context, "cards", key, requester, cancellationToken);</code></example>
public sealed class CacheHttpResultMapper(CacheCatalog catalog, CacheWebConfiguration configuration, ICacheHttpAccessContextFactory accessFactory,
    CacheRuntimeResources resources, CacheHttpRequestReader reader)
{
    /// <summary>Executes a normal direct read or metadata-only conditional decision without opening payloads for 304.</summary>
    public async Task<HttpResult> ReadAsync(HttpContext context, string collection, string key, IRequester requester, CancellationToken cancellationToken)
    {
        var binding = catalog.Collections[collection];
        var access = accessFactory.Create(context, CacheAccessActions.Read);
        CacheReadOptions options = null;
        if (CacheHttpPreconditions.RequiresMetadata(context.Request))
        {
            var metadata = await requester.SendAsync(new CacheEntryMetadataQuery(access, collection, key), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (metadata.IsFailure) { return Failure(metadata); }

            var decision = CacheHttpPreconditions.Evaluate(context.Request, metadata.Value, CacheStreamingResult.Representation(binding), true);
            if (decision.IsFailure) { return Failure(decision); }

            if (!metadata.Value.Found) { return Results.NotFound(); }

            if (decision.Value.NotModified)
            {
                context.Response.Headers.ETag = CacheEntityTag.Create(metadata.Value.Version.Value, CacheStreamingResult.Representation(binding));
                context.Response.Headers.LastModified = metadata.Value.Value.PublishedAt.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                context.Response.Headers.CacheControl = configuration.Options.CachePolicy == CacheHttpCachePolicy.Revalidate ? "private, no-cache" : "no-store";
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            var resolved = CacheHttpPreconditions.ReadOptions(context.Request, metadata.Value.Value, CacheStreamingResult.Representation(binding));
            if (resolved.IsFailure)
            {
                if (resolved.Errors.Any(error => error is CacheHttpRangeError))
                {
                    context.Response.Headers.ContentRange = "bytes */" + metadata.Value.Value.LogicalSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                return Failure(resolved);
            }

            options = resolved.Value;
        }

        var acquired = await requester.SendAsync(new CacheEntryReadQuery(access, collection, key, options), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (acquired.IsFailure) { return Failure(acquired); }

        return !acquired.Value.Found ? Results.NotFound() : new CacheStreamingResult(acquired.Value.Value, binding, resources,
            configuration.Options.CachePolicy, binding.Contract.IsBinary ? "application/octet-stream" :
                acquired.Value.Value.Metadata.CodecId.Equals("json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "application/octet-stream");
    }

    /// <summary>Resolves conditions, then reads the approved request format and delegates publication through the common handler.</summary>
    public async Task<HttpResult> WriteAsync(HttpContext context, string collection, string key, IRequester requester, CancellationToken cancellationToken)
    {
        var access = accessFactory.Create(context, CacheAccessActions.Write);
        var binding = catalog.Collections[collection];
        var condition = await this.ConditionAsync(context, collection, key, requester, binding, CacheAccessActions.Write, cancellationToken).ConfigureAwait(false);
        if (condition.IsFailure) { return Failure(condition); }

        var descriptor = reader.ReadDescriptor(context.Request);
        if (descriptor.IsFailure) { return Failure(descriptor); }

        var prepared = await reader.ReadWriteAsync(context.Request, binding, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure) { return Failure(prepared); }

        var owned = prepared.Value;
        using var lease = CacheResourceLeaseContext.Begin(owned.Reservation);
        var result = await requester.SendAsync(new CacheEntryWriteCommand(access, collection, new(key), owned.Input,
            owned.Value, owned.Typed, descriptor.Value, condition.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) { return Failure(result); }

        if (result.Value.Version.HasValue)
        {
            context.Response.Headers.ETag = CacheEntityTag.Create(result.Value.Version.Value, CacheStreamingResult.Representation(binding));
        }

        return Results.Ok(new CacheHttpWriteResponse(result.Value.StorageOutcome, result.Value.Version?.Value));
    }

    /// <summary>Maps mutation preconditions to an atomic version condition without exposing a source loader.</summary>
    public async Task<HttpResult> RemoveAsync(HttpContext context, string collection, string key, IRequester requester, CancellationToken cancellationToken)
    {
        var condition = await this.ConditionAsync(context, collection, key, requester, catalog.Collections[collection], CacheAccessActions.Invalidate, cancellationToken).ConfigureAwait(false);
        if (condition.IsFailure) { return Failure(condition); }

        var removed = await requester.SendAsync(new CacheEntryRemoveCommand(accessFactory.Create(context, CacheAccessActions.Invalidate),
            collection, key, condition.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        return removed.IsSuccess ? Results.NoContent() : Failure(removed);
    }

    /// <summary>Maps any cache request failure through the shared registered Problem Details handler.</summary>
    public static HttpResult Failure(BridgingIT.DevKit.Common.IResult result) => Result.Failure().WithErrors(result.Errors).MapHttpNoContent();

    /// <summary>Maps accepted background submission separately from confirmed synchronous completion and partial child failures.</summary>
    public static HttpResult Submission(Result<IReadOnlyList<Result<CacheOperationSubmission>>> result, string statusBasePath)
        => Submission(result, handle => statusBasePath + "/" + Uri.EscapeDataString(handle.Id));

    /// <summary>Maps bounded child submissions using each collection's actual configured status route.</summary>
    public static HttpResult Submission(Result<IReadOnlyList<Result<CacheOperationSubmission>>> result, Func<CacheOperationHandle, string> statusLocation,
        IReadOnlyList<string> targets = null)
    {
        if (result.IsFailure) { return Failure(result); }

        var children = result.Value.Select((child, index) => child.IsSuccess ? new CacheHttpOperationResponse(child.Value.Handle.Collection,
            child.Value.Accepted, CacheHttpOperationStatus.From(child.Value.Status), statusLocation(child.Value.Handle)) :
            new CacheHttpOperationResponse(targets?.ElementAtOrDefault(index), false, null, null, child.Errors.FirstOrDefault()?.Message)).ToArray();
        return children.Length > 0 && children.All(child => child.Accepted) ? Results.Accepted(children[0].Location, children) : Results.Ok(children);
    }

    private async Task<Result<CacheWriteCondition>> ConditionAsync(HttpContext context, string collection, string key,
        IRequester requester, CacheCollectionBinding binding, CacheAccessActions action, CancellationToken cancellationToken)
    {
        if (!CacheHttpPreconditions.MutationRequiresMetadata(context.Request))
        {
            return Result<CacheWriteCondition>.Success(new(CreateOnly: context.Request.Headers[HeaderNames.IfNoneMatch].ToString().Trim() == "*"));
        }

        var metadata = await requester.SendAsync(new CacheEntryMetadataQuery(accessFactory.Create(context, action),
            collection, key, action), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (metadata.IsFailure) { return Result<CacheWriteCondition>.Failure(metadata); }

        var evaluated = CacheHttpPreconditions.Evaluate(context.Request, metadata.Value, CacheStreamingResult.Representation(binding), false);
        return evaluated.IsFailure ? Result<CacheWriteCondition>.Failure(evaluated) : Result<CacheWriteCondition>.Success(evaluated.Value.Condition);
    }
}