// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common;

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

/// <summary>Evaluates request conditions and a single byte range against an already selected representation.</summary>
/// <remarks>The caller supplies authoritative existence, a strong entity tag and the last-modified timestamp.
/// The caller authorizes access, selects content, and applies mutation conditions atomically.</remarks>
/// <example><code>var outcome = HttpRepresentationHeaders.Evaluate(request, true, entityTag, publishedAt, read: true);
/// if (outcome == HttpRepresentationPreconditionOutcome.NotModified) { return Results.StatusCode(StatusCodes.Status304NotModified); }</code></example>
public static class HttpRepresentationHeaders
{
    /// <summary>Evaluates If-Match, If-Unmodified-Since, If-None-Match and If-Modified-Since in precedence order.</summary>
    /// <param name="request">The request containing conditional headers.</param>
    /// <param name="exists">Whether the selected representation exists.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag, or null when it does not exist.</param>
    /// <param name="lastModified">Its last modification timestamp. HTTP dates compare at whole-second precision.</param>
    /// <param name="read">Whether this is a read, where a matching validator can produce a not-modified response.</param>
    /// <returns>The decision before delivering content or attempting an atomic mutation.</returns>
    /// <example><code>var outcome = HttpRepresentationHeaders.Evaluate(request, exists, entityTag, modifiedAt, read: false);</code></example>
    public static HttpRepresentationPreconditionOutcome Evaluate(HttpRequest request, bool exists, string entityTag, DateTimeOffset lastModified, bool read) =>
        Evaluate(request, exists, entityTag, lastModified, read, HttpRepresentationHeaderMode.Strict);

    /// <summary>Evaluates the request preconditions with the selected validator comparison rules.</summary>
    /// <param name="request">The request containing conditional headers.</param>
    /// <param name="exists">Whether the selected representation exists.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag, or null when it does not exist.</param>
    /// <param name="lastModified">Its last modification timestamp, compared at whole-second precision.</param>
    /// <param name="read">Whether a matching cache validator can produce a not-modified decision.</param>
    /// <param name="mode">The validator comparison rules to use.</param>
    /// <returns>The decision before delivering content or attempting an atomic mutation.</returns>
    /// <example><code>var outcome = HttpRepresentationHeaders.Evaluate(request, true, entityTag, modifiedAt, true, HttpRepresentationHeaderMode.Strict);</code></example>
    public static HttpRepresentationPreconditionOutcome Evaluate(
        HttpRequest request, bool exists, string entityTag, DateTimeOffset lastModified, bool read, HttpRepresentationHeaderMode mode)
    {
        var ifMatch = request.Headers[HeaderNames.IfMatch].ToString();
        var ifNoneMatch = request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            if (!exists || (ifMatch.Trim() != "*" && !MatchesEntityTag(ifMatch, entityTag, false, mode)))
            {
                return HttpRepresentationPreconditionOutcome.Failed;
            }
        }
        else if (exists && TryDate(request.Headers[HeaderNames.IfUnmodifiedSince], mode, out var unmodified) && WholeSeconds(lastModified) > unmodified)
        {
            return HttpRepresentationPreconditionOutcome.Failed;
        }

        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            if (exists && (ifNoneMatch.Trim() == "*" || MatchesEntityTag(ifNoneMatch, entityTag, true, mode)))
            {
                return read ? HttpRepresentationPreconditionOutcome.NotModified : HttpRepresentationPreconditionOutcome.Failed;
            }
        }
        else if (read && exists && TryDate(request.Headers[HeaderNames.IfModifiedSince], mode, out var modified) && WholeSeconds(lastModified) <= modified)
        {
            return HttpRepresentationPreconditionOutcome.NotModified;
        }

        return HttpRepresentationPreconditionOutcome.Proceed;
    }

    /// <summary>Compares comma-separated entity tags with a current strong validator, optionally accepting weak candidate tags.</summary>
    /// <param name="values">The comma-separated candidate validators.</param>
    /// <param name="current">The current strong, quoted entity tag.</param>
    /// <param name="weak">Whether a candidate's case-sensitive W/ prefix can be ignored.</param>
    /// <returns>Whether an ordinal candidate matches. Wildcards are handled separately by precondition evaluation.</returns>
    /// <example><code>var matches = HttpRepresentationHeaders.MatchesEntityTag(request.Headers.IfNoneMatch, entityTag, weak: true);</code></example>
    public static bool MatchesEntityTag(string values, string current, bool weak = false) =>
        MatchesEntityTag(values, current, weak, HttpRepresentationHeaderMode.Strict);

    /// <summary>Compares candidate entity tags with the current strong validator using the selected rules.</summary>
    /// <param name="values">The comma-separated candidate validators.</param>
    /// <param name="current">The current strong, quoted entity tag.</param>
    /// <param name="weak">Whether a weak candidate can match.</param>
    /// <param name="mode">The validator comparison rules to use.</param>
    /// <returns>Whether an ordinal candidate matches. Wildcards are handled separately by precondition evaluation.</returns>
    /// <example><code>var matches = HttpRepresentationHeaders.MatchesEntityTag(request.Headers.IfMatch, entityTag, false, HttpRepresentationHeaderMode.Strict);</code></example>
    public static bool MatchesEntityTag(string values, string current, bool weak, HttpRepresentationHeaderMode mode)
    {
        if (string.IsNullOrWhiteSpace(values) || current is null)
        {
            return false;
        }

        if (mode != HttpRepresentationHeaderMode.LegacyPresentation)
        {
            return TryParseStrongEntityTag(current, out var currentTag)
                && EntityTagHeaderValue.TryParseStrictList([values], out var candidates)
                && candidates.All(IsConcreteCandidate)
                && candidates.Any(candidate => candidate.Compare(currentTag, useStrongComparison: !weak));
        }

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
    /// <remarks>If-Range accepts one matching strong entity tag or an exact last-modified date at whole-second precision.
    /// The byte range unit is case insensitive. Multiple ranges are unsupported.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <example><code>var range = HttpRepresentationHeaders.SelectRange(request, contentLength, entityTag, modifiedAt);
    /// if (range.Outcome == HttpRepresentationRangeKind.Partial) { await CopyRangeAsync(range.Offset, range.Length.Value); }</code></example>
    public static HttpRepresentationRangeSelection SelectRange(HttpRequest request, long length, string entityTag, DateTimeOffset lastModified) =>
        SelectRange(request, length, entityTag, lastModified, HttpRepresentationHeaderMode.Strict);

    /// <summary>Selects a range using the selected rules for If-Range validators and range units.</summary>
    /// <param name="request">The request containing Range and optional If-Range headers.</param>
    /// <param name="length">The nonnegative length of the selected representation in bytes.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag.</param>
    /// <param name="lastModified">Its last modification timestamp, compared at whole-second precision.</param>
    /// <param name="mode">The validator and range unit comparison rules to use.</param>
    /// <returns>A full or partial selection, or an unsupported or unsatisfiable range classification.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <example><code>var range = HttpRepresentationHeaders.SelectRange(request, contentLength, entityTag, modifiedAt, HttpRepresentationHeaderMode.Strict);</code></example>
    public static HttpRepresentationRangeSelection SelectRange(
        HttpRequest request, long length, string entityTag, DateTimeOffset lastModified, HttpRepresentationHeaderMode mode) =>
        SelectRange(request, length, entityTag, lastModified, mode, allowIfRangeDate: true);

    /// <summary>Selects a byte range with explicit control over date-based If-Range validation.</summary>
    /// <param name="request">The request containing Range and optional If-Range headers.</param>
    /// <param name="length">The nonnegative length of the selected representation in bytes.</param>
    /// <param name="entityTag">Its current strong, quoted entity tag.</param>
    /// <param name="lastModified">Its last modification timestamp, compared at whole-second precision when date validation is enabled.</param>
    /// <param name="mode">The validator and range unit comparison rules to use.</param>
    /// <param name="allowIfRangeDate">Whether the last modification timestamp can validate an If-Range date.</param>
    /// <returns>A full or partial selection, or an unsupported or unsatisfiable range classification.</returns>
    /// <remarks>When disabled, a date in If-Range selects the full response. Strong entity tags remain eligible for validation.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <example><code>var range = HttpRepresentationHeaders.SelectRange(request, contentLength, entityTag, modifiedAt,
    ///     mode: HttpRepresentationHeaderMode.Strict, allowIfRangeDate: false);</code></example>
    public static HttpRepresentationRangeSelection SelectRange(
        HttpRequest request, long length, string entityTag, DateTimeOffset lastModified, HttpRepresentationHeaderMode mode, bool allowIfRangeDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var raw = request.Headers[HeaderNames.Range].ToString();
        if (string.IsNullOrEmpty(raw))
        {
            return new(HttpRepresentationRangeKind.Full);
        }

        if (request.Headers.TryGetValue(HeaderNames.IfRange, out var ifRange)
            && (mode != HttpRepresentationHeaderMode.LegacyPresentation || !string.IsNullOrWhiteSpace(ifRange))
            && !MatchesIfRange(ifRange, entityTag, lastModified, mode, allowIfRangeDate))
        {
            return new(HttpRepresentationRangeKind.Full);
        }

        var unitComparison = mode == HttpRepresentationHeaderMode.LegacyPresentation ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!RangeHeaderValue.TryParse(raw, out var parsed) || !parsed.Unit.Equals("bytes", unitComparison) || parsed.Ranges.Count != 1)
        {
            return new(HttpRepresentationRangeKind.Unsupported);
        }

        var selected = parsed.Ranges.Single();
        if (length == 0 || (selected.From.HasValue && selected.From.Value >= length) ||
            (!selected.From.HasValue && (!selected.To.HasValue || selected.To.Value == 0)))
        {
            return new(HttpRepresentationRangeKind.Unsatisfiable);
        }

        var offset = selected.From ?? Math.Max(0, length - selected.To.Value);
        var end = selected.From.HasValue ? Math.Min(selected.To ?? length - 1, length - 1) : length - 1;
        return new(HttpRepresentationRangeKind.Partial, offset, end - offset + 1);
    }

    private static bool MatchesIfRange(string value, string entityTag, DateTimeOffset lastModified, HttpRepresentationHeaderMode mode, bool allowIfRangeDate)
    {
        if (mode == HttpRepresentationHeaderMode.LegacyPresentation)
        {
            return MatchesEntityTag(value, entityTag, false, mode)
                || (allowIfRangeDate && TryDate(value, mode, out var legacyDate) && WholeSeconds(lastModified) <= legacyDate);
        }

        if (!RangeConditionHeaderValue.TryParse(value, out var condition))
        {
            return false;
        }

        return condition.LastModified is { } date
            ? allowIfRangeDate && WholeSeconds(lastModified) == date
            : TryParseStrongEntityTag(entityTag, out var currentTag) && condition.EntityTag.Compare(currentTag, useStrongComparison: true);
    }

    private static bool TryParseStrongEntityTag(string value, out EntityTagHeaderValue tag) =>
        EntityTagHeaderValue.TryParse(value, out tag) && !tag.IsWeak && !tag.Equals(EntityTagHeaderValue.Any);

    private static bool IsConcreteCandidate(EntityTagHeaderValue candidate)
    {
        var tag = candidate.Tag;

        // The typed parser also accepts a lowercase weak prefix. Check the original prefix.
        return !candidate.Equals(EntityTagHeaderValue.Any)
            && (!candidate.IsWeak || (tag.Offset >= 2 && tag.Buffer.AsSpan(tag.Offset - 2, 2).SequenceEqual("W/")));
    }

    private static DateTimeOffset WholeSeconds(DateTimeOffset value) => DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds());

    private static bool TryDate(string value, HttpRepresentationHeaderMode mode, out DateTimeOffset date) =>
        mode == HttpRepresentationHeaderMode.LegacyPresentation
            ? DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date)
            : HeaderUtilities.TryParseDate(value, out date);
}

/// <summary>Selects the validator and range unit comparison rules for representation headers.</summary>
/// <example><code>var mode = HttpRepresentationHeaderMode.Strict;</code></example>
public enum HttpRepresentationHeaderMode
{
    /// <summary>Parses quoted tags, requires an exact If-Range validator, and compares byte range units case-insensitively.</summary>
    Strict,

    /// <summary>Preserves Presentation's comma splitting, RFC 1123 date parsing, If-Range date ordering, and case-sensitive range unit matching.</summary>
    LegacyPresentation
}

/// <summary>Describes the conditional-request decision for a selected representation.</summary>
/// <example><code>if (outcome == HttpRepresentationPreconditionOutcome.Failed) { return Results.StatusCode(StatusCodes.Status412PreconditionFailed); }</code></example>
public enum HttpRepresentationPreconditionOutcome
{
    /// <summary>Continue with content delivery or atomically verify the mutation condition.</summary>
    Proceed = 0,

    /// <summary>The existing representation has not changed for this conditional read.</summary>
    NotModified = 1,

    /// <summary>A required condition does not match the selected representation.</summary>
    Failed = 2
}

/// <summary>Describes whether a single byte range can select content from a representation.</summary>
/// <example><code>if (range.Outcome == HttpRepresentationRangeKind.Unsatisfiable) { return Results.StatusCode(StatusCodes.Status416RangeNotSatisfiable); }</code></example>
public enum HttpRepresentationRangeKind
{
    /// <summary>Deliver the full representation because no range applies.</summary>
    Full = 0,

    /// <summary>Deliver the selected offset and length.</summary>
    Partial = 1,

    /// <summary>The header is malformed, uses an unsupported unit or requests multiple ranges.</summary>
    Unsupported = 2,

    /// <summary>A valid byte range selects no bytes from the current representation.</summary>
    Unsatisfiable = 3
}

/// <summary>Contains the result of selecting a single range without reading representation content.</summary>
/// <param name="Outcome">The selection classification.</param>
/// <param name="Offset">The first byte of a partial selection; zero otherwise.</param>
/// <param name="Length">The number of selected bytes for a partial selection; null otherwise.</param>
/// <example><code>var selectedLength = range.Outcome == HttpRepresentationRangeKind.Partial ? range.Length.Value : contentLength;</code></example>
public readonly record struct HttpRepresentationRangeSelection(HttpRepresentationRangeKind Outcome, long Offset = 0, long? Length = null);
