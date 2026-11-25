// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Http;

/// <summary>Evaluates request conditions and a single byte range against an already selected representation.</summary>
/// <remarks>The caller supplies authoritative existence, a strong entity tag and the last-modified timestamp.
/// This helper does not acquire content, build validators, authorize access or apply mutation conditions atomically.</remarks>
/// <seealso cref="HttpRepresentationHeaders"/>
/// <example><code>var outcome = HttpRepresentationConditions.Evaluate(request, true, entityTag, publishedAt, read: true);
/// if (outcome == HttpPreconditionOutcome.NotModified) { return Results.StatusCode(StatusCodes.Status304NotModified); }</code></example>
public static class HttpRepresentationConditions
{
    /// <summary>Evaluates If-Match, If-Unmodified-Since, If-None-Match and If-Modified-Since in precedence order.</summary>
    /// <param name="request">The request containing conditional headers.</param>
    /// <param name="exists">Whether the selected representation exists.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag, or null when it does not exist.</param>
    /// <param name="lastModified">Its last modification timestamp. HTTP dates compare at whole-second precision.</param>
    /// <param name="read">Whether this is a read, where a matching validator can produce a not-modified response.</param>
    /// <returns>The decision before delivering content or attempting an atomic mutation.</returns>
    /// <example><code>var outcome = HttpRepresentationConditions.Evaluate(request, exists, entityTag, modifiedAt, read: false);</code></example>
    public static HttpPreconditionOutcome Evaluate(HttpRequest request, bool exists, string entityTag, DateTimeOffset lastModified, bool read)
    {
        // The Common and Presentation outcomes share the original numeric values.
        return (HttpPreconditionOutcome)HttpRepresentationHeaders.Evaluate(
            request, exists, entityTag, lastModified, read, HttpRepresentationHeaderMode.LegacyPresentation);
    }

    /// <summary>Compares comma-separated entity tags with a current strong validator, optionally accepting weak candidate tags.</summary>
    /// <param name="values">The comma-separated candidate validators.</param>
    /// <param name="current">The current strong, quoted entity tag.</param>
    /// <param name="weak">Whether a candidate's case-sensitive W/ prefix can be ignored.</param>
    /// <returns>Whether an ordinal candidate matches. Wildcards are handled separately by precondition evaluation.</returns>
    /// <example><code>var matches = HttpRepresentationConditions.MatchesEntityTag(request.Headers.IfNoneMatch, entityTag, weak: true);</code></example>
    public static bool MatchesEntityTag(string values, string current, bool weak = false) =>
        HttpRepresentationHeaders.MatchesEntityTag(values, current, weak, HttpRepresentationHeaderMode.LegacyPresentation);

    /// <summary>Selects one contiguous byte range, or the full representation when Range is absent or If-Range does not match.</summary>
    /// <param name="request">The request containing Range and optional If-Range headers.</param>
    /// <param name="length">The nonnegative length of the selected representation in bytes.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag.</param>
    /// <param name="lastModified">Its last modification timestamp, compared at whole-second precision.</param>
    /// <returns>A full or partial selection, or an unsupported or unsatisfiable range classification.</returns>
    /// <remarks>A mismatching If-Range ignores Range before parsing it. Multiple ranges and units other than bytes are unsupported.</remarks>
    /// <example><code>var range = HttpRepresentationConditions.SelectRange(request, contentLength, entityTag, modifiedAt);
    /// if (range.Outcome == HttpRangeSelectionKind.Partial) { await CopyRangeAsync(range.Offset, range.Length.Value); }</code></example>
    public static HttpRangeSelection SelectRange(HttpRequest request, long length, string entityTag, DateTimeOffset lastModified)
    {
        var selection = HttpRepresentationHeaders.SelectRange(
            request, length, entityTag, lastModified, HttpRepresentationHeaderMode.LegacyPresentation);
        return new((HttpRangeSelectionKind)selection.Outcome, selection.Offset, selection.Length);
    }
}

/// <summary>Describes the conditional-request decision for a selected representation.</summary>
/// <example><code>if (outcome == HttpPreconditionOutcome.Failed) { return Results.StatusCode(StatusCodes.Status412PreconditionFailed); }</code></example>
public enum HttpPreconditionOutcome
{
    /// <summary>Continue with content delivery or atomically verify the mutation condition.</summary>
    Proceed,

    /// <summary>The existing representation has not changed for this conditional read.</summary>
    NotModified,

    /// <summary>A required condition does not match the selected representation.</summary>
    Failed
}

/// <summary>Describes whether a single byte range can select content from a representation.</summary>
/// <example><code>if (range.Outcome == HttpRangeSelectionKind.Unsatisfiable) { return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable); }</code></example>
public enum HttpRangeSelectionKind
{
    /// <summary>Deliver the full representation because no range applies.</summary>
    Full,

    /// <summary>Deliver the selected offset and length.</summary>
    Partial,

    /// <summary>The header is malformed, uses an unsupported unit or requests multiple ranges.</summary>
    Unsupported,

    /// <summary>A valid byte range selects no bytes from the current representation.</summary>
    Unsatisfiable
}

/// <summary>Contains the result of selecting a single range without reading representation content.</summary>
/// <param name="Outcome">The selection classification.</param>
/// <param name="Offset">The first byte of a partial selection; zero otherwise.</param>
/// <param name="Length">The number of selected bytes for a partial selection; null otherwise.</param>
/// <example><code>var selectedLength = range.Outcome == HttpRangeSelectionKind.Partial ? range.Length.Value : contentLength;</code></example>
public readonly record struct HttpRangeSelection(HttpRangeSelectionKind Outcome, long Offset = 0, long? Length = null);
