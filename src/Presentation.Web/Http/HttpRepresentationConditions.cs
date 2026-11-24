// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.Web;

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

/// <summary>Evaluates request conditions and a single byte range against an already selected representation.</summary>
/// <remarks>The caller supplies authoritative existence, a strong entity tag and the last-modified timestamp.
/// This helper does not acquire content, build validators, authorize access or apply mutation conditions atomically.</remarks>
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
        var ifMatch = request.Headers[HeaderNames.IfMatch].ToString();
        var ifNoneMatch = request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            if (!exists || (ifMatch.Trim() != "*" && !MatchesEntityTag(ifMatch, entityTag, false))) { return HttpPreconditionOutcome.Failed; }
        }
        else if (exists && TryDate(request.Headers[HeaderNames.IfUnmodifiedSince], out var unmodified) && WholeSeconds(lastModified) > unmodified)
        {
            return HttpPreconditionOutcome.Failed;
        }

        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            if (exists && (ifNoneMatch.Trim() == "*" || MatchesEntityTag(ifNoneMatch, entityTag, true)))
            {
                return read ? HttpPreconditionOutcome.NotModified : HttpPreconditionOutcome.Failed;
            }
        }
        else if (read && exists && TryDate(request.Headers[HeaderNames.IfModifiedSince], out var modified) && WholeSeconds(lastModified) <= modified)
        {
            return HttpPreconditionOutcome.NotModified;
        }

        return HttpPreconditionOutcome.Proceed;
    }

    /// <summary>Compares comma-separated entity tags with a current strong validator, optionally accepting weak candidate tags.</summary>
    /// <param name="values">The comma-separated candidate validators.</param>
    /// <param name="current">The current strong, quoted entity tag.</param>
    /// <param name="weak">Whether a candidate's case-sensitive W/ prefix can be ignored.</param>
    /// <returns>Whether an ordinal candidate matches. Wildcards are handled separately by precondition evaluation.</returns>
    /// <example><code>var matches = HttpRepresentationConditions.MatchesEntityTag(request.Headers.IfNoneMatch, entityTag, weak: true);</code></example>
    public static bool MatchesEntityTag(string values, string current, bool weak = false)
    {
        if (string.IsNullOrWhiteSpace(values) || current is null) { return false; }

        return values.Split(',').Any(value =>
        {
            var tag = value.Trim();
            return StringComparer.Ordinal.Equals(weak && tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag, current);
        });
    }

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
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var raw = request.Headers[HeaderNames.Range].ToString();
        if (string.IsNullOrEmpty(raw)) { return new(HttpRangeSelectionKind.Full); }

        var ifRange = request.Headers[HeaderNames.IfRange].ToString();
        if (!string.IsNullOrWhiteSpace(ifRange))
        {
            var matches = MatchesEntityTag(ifRange, entityTag, false) ||
                (TryDate(ifRange, out var date) && WholeSeconds(lastModified) <= date);
            if (!matches) { return new(HttpRangeSelectionKind.Full); }
        }

        if (!RangeHeaderValue.TryParse(raw, out var parsed) || parsed.Unit != "bytes" || parsed.Ranges.Count != 1)
        {
            return new(HttpRangeSelectionKind.Unsupported);
        }

        var selected = parsed.Ranges.Single();
        if (length == 0 || (selected.From.HasValue && selected.From.Value >= length) ||
            (!selected.From.HasValue && (!selected.To.HasValue || selected.To.Value == 0)))
        {
            return new(HttpRangeSelectionKind.Unsatisfiable);
        }

        var offset = selected.From ?? Math.Max(0, length - selected.To.Value);
        var end = selected.From.HasValue ? Math.Min(selected.To ?? length - 1, length - 1) : length - 1;
        return new(HttpRangeSelectionKind.Partial, offset, end - offset + 1);
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset value) => DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds());

    private static bool TryDate(string value, out DateTimeOffset date) => DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
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