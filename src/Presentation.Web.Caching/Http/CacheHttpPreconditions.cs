// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

/// <summary>Evaluates HTTP conditions against one authoritative compatible snapshot before content delivery or conditional publication.</summary>
/// <example><code>var condition = CacheHttpPreconditions.Evaluate(request, lookup, representationId, read: true);</code></example>
public static class CacheHttpPreconditions
{
    /// <summary>Tests whether a GET requires metadata-only validity before deciding 304/preconditions/range.</summary>
    public static bool RequiresMetadata(HttpRequest request) => request.Headers.ContainsKey(HeaderNames.IfMatch) ||
        request.Headers.ContainsKey(HeaderNames.IfNoneMatch) || request.Headers.ContainsKey(HeaderNames.IfModifiedSince) ||
        request.Headers.ContainsKey(HeaderNames.IfUnmodifiedSince) || request.Headers.ContainsKey(HeaderNames.Range);

    /// <summary>Tests whether a mutation requires an authoritative snapshot; create-only PUT can go directly to atomic publication.</summary>
    public static bool MutationRequiresMetadata(HttpRequest request) => request.Headers.ContainsKey(HeaderNames.IfMatch) ||
        request.Headers.ContainsKey(HeaderNames.IfUnmodifiedSince) ||
        (request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var none) &&
            (none.ToString().Trim() != "*" || !HttpMethods.IsPut(request.Method)));

    /// <summary>Applies standard If-Match/date/If-None-Match/date ordering and returns an atomic write condition for the selected snapshot.</summary>
    public static Result<CacheHttpPreconditionOutcome> Evaluate(HttpRequest request, CacheLookup<CacheEntryMetadata> lookup,
        string representationId, bool read)
    {
        var exists = lookup.Found;
        var metadata = exists ? lookup.Value : null;
        var current = exists ? CacheEntityTag.Create(metadata.Version, representationId) : null;
        var decision = HttpRepresentationConditions.Evaluate(request, exists, current, metadata?.PublishedAt ?? default, read);
        if (decision == HttpPreconditionOutcome.Failed) { return Failed(); }

        if (decision == HttpPreconditionOutcome.NotModified)
        {
            return Result<CacheHttpPreconditionOutcome>.Success(new(true, default));
        }

        var ifMatch = request.Headers[HeaderNames.IfMatch].ToString();
        var ifNoneMatch = request.Headers[HeaderNames.IfNoneMatch].ToString();
        var conditionalMutation = !read && (!string.IsNullOrWhiteSpace(ifMatch) || !string.IsNullOrWhiteSpace(ifNoneMatch) ||
            request.Headers.ContainsKey(HeaderNames.IfUnmodifiedSince));
        return Result<CacheHttpPreconditionOutcome>.Success(new(false, conditionalMutation ?
            exists ? new(metadata.Version) : new(CreateOnly: true) : default));
    }

    /// <summary>Resolves a single contiguous logical byte range without buffering bytes or broadening provider access capabilities.</summary>
    public static Result<CacheReadOptions> ReadOptions(HttpRequest request, CacheEntryMetadata metadata, string representationId)
    {
        var raw = request.Headers[HeaderNames.Range].ToString();
        if (string.IsNullOrEmpty(raw)) { return Result<CacheReadOptions>.Success(new(ExpectedVersion: metadata?.Version)); }

        var current = string.IsNullOrWhiteSpace(request.Headers[HeaderNames.IfRange].ToString()) ? null :
            CacheEntityTag.Create(metadata.Version, representationId);
        var selected = HttpRepresentationConditions.SelectRange(request, metadata.LogicalSizeBytes, current, metadata.PublishedAt);
        if (selected.Outcome == HttpRangeSelectionKind.Full)
        {
            return Result<CacheReadOptions>.Success(new(ExpectedVersion: metadata.Version));
        }

        if (selected.Outcome == HttpRangeSelectionKind.Unsupported)
        {
            return Result<CacheReadOptions>.Failure(new CacheError(CacheErrorCode.Unsupported, "Only one contiguous bytes range is supported."));
        }

        if (selected.Outcome == HttpRangeSelectionKind.Unsatisfiable)
        {
            return Result<CacheReadOptions>.Failure(new CacheHttpRangeError());
        }

        return Result<CacheReadOptions>.Success(new(Offset: selected.Offset, Length: selected.Length, ExpectedVersion: metadata.Version));
    }

    private static Result<CacheHttpPreconditionOutcome> Failed() => Result<CacheHttpPreconditionOutcome>.Failure(
        new CacheError(CacheErrorCode.Conflict, "The HTTP cache precondition did not match the current positive representation."));
}

/// <summary>Describes a payload-free 304 decision and the selected atomic mutation condition.</summary>
/// <param name="NotModified">Whether a valid conditional GET should return 304.</param>
/// <param name="Condition">A version/create-only condition checked again at publication.</param>
/// <example><code>var version = decision.Condition.ExpectedVersion;</code></example>
public sealed record CacheHttpPreconditionOutcome(bool NotModified, CacheWriteCondition Condition);