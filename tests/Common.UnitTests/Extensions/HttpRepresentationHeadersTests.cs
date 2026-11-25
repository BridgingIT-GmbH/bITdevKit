// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Microsoft.AspNetCore.Http;

/// <summary>Verifies Common's representation preconditions and range selection.</summary>
/// <example><code>request.Headers.Range = "bytes=-3";</code></example>
public sealed class HttpRepresentationHeadersTests
{
    private const string EntityTag = "\"report-v1\"";
    private const string TagWithComma = "\"report,2026\"";
    private const string ModifiedDate = "Thu, 05 Feb 2026 10:30:20 GMT";
    private const string StaleDate = "Thu, 05 Feb 2026 10:30:19 GMT";
    private const string LaterDate = "Thu, 05 Feb 2026 10:30:21 GMT";
    private static readonly DateTimeOffset ModifiedAt = new(2026, 2, 5, 10, 30, 20, 900, TimeSpan.Zero);

    /// <summary>Checks opt-out handling for dates and the retained behavior for strong tags.</summary>
    /// <example><code>var selection = HttpRepresentationHeaders.SelectRange(request, 10, entityTag, modifiedAt, HttpRepresentationHeaderMode.Strict, allowIfRangeDate: false);</code></example>
    [Theory]
    [InlineData(HttpRepresentationHeaderMode.Strict, ModifiedDate, false, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.LegacyPresentation, ModifiedDate, false, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.LegacyPresentation, LaterDate, false, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.Strict, EntityTag, false, HttpRepresentationRangeKind.Partial)]
    [InlineData(HttpRepresentationHeaderMode.LegacyPresentation, EntityTag, false, HttpRepresentationRangeKind.Partial)]
    [InlineData(HttpRepresentationHeaderMode.Strict, "W/" + EntityTag, false, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.Strict, "\"other\"", false, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.Strict, null, false, HttpRepresentationRangeKind.Partial)]
    [InlineData(HttpRepresentationHeaderMode.Strict, ModifiedDate, true, HttpRepresentationRangeKind.Partial)]
    [InlineData(HttpRepresentationHeaderMode.Strict, LaterDate, true, HttpRepresentationRangeKind.Full)]
    [InlineData(HttpRepresentationHeaderMode.LegacyPresentation, LaterDate, true, HttpRepresentationRangeKind.Partial)]
    public void SelectRange_WithDateValidationOption_AppliesSelectedPolicy(
        HttpRepresentationHeaderMode mode, string ifRange, bool allowIfRangeDate, HttpRepresentationRangeKind expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = "bytes=2-4";
        request.Headers.IfRange = ifRange;

        var result = HttpRepresentationHeaders.SelectRange(request, 10, EntityTag, ModifiedAt, mode, allowIfRangeDate);

        result.Outcome.ShouldBe(expected);
        if (allowIfRangeDate && mode == HttpRepresentationHeaderMode.Strict)
        {
            HttpRepresentationHeaders.SelectRange(request, 10, EntityTag, ModifiedAt, default).ShouldBe(result);
        }
    }

    /// <summary>Checks validator precedence, wildcard existence, and read and mutation outcomes.</summary>
    /// <example><code>request.Headers.IfNoneMatch = "W/\"report-v1\"";</code></example>
    [Theory]
    [InlineData("\"other\"", "*", true, true, HttpRepresentationPreconditionOutcome.Failed)]
    [InlineData(EntityTag, "\"other\"", true, true, HttpRepresentationPreconditionOutcome.Proceed)]
    [InlineData(EntityTag, "W/\"report-v1\"", true, true, HttpRepresentationPreconditionOutcome.NotModified)]
    [InlineData(EntityTag, "W/\"report-v1\"", true, false, HttpRepresentationPreconditionOutcome.Failed)]
    [InlineData("*", null, false, true, HttpRepresentationPreconditionOutcome.Failed)]
    [InlineData(null, "*", false, true, HttpRepresentationPreconditionOutcome.Proceed)]
    public void Evaluate_EntityTagConditions_UsesPrecedenceAndExistence(
        string ifMatch, string ifNoneMatch, bool exists, bool read, HttpRepresentationPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfMatch = ifMatch;
        request.Headers.IfUnmodifiedSince = StaleDate;
        request.Headers.IfNoneMatch = ifNoneMatch;
        request.Headers.IfModifiedSince = ModifiedDate;

        var result = HttpRepresentationHeaders.Evaluate(request, exists, exists ? EntityTag : null, ModifiedAt, read);

        result.ShouldBe(expected);
    }

    /// <summary>Checks that quoted commas remain part of conditional request validators.</summary>
    /// <example><code>request.Headers.IfMatch = "\"report,2026\"";</code></example>
    [Theory]
    [InlineData("If-Match", true, HttpRepresentationPreconditionOutcome.Proceed)]
    [InlineData("If-None-Match", true, HttpRepresentationPreconditionOutcome.NotModified)]
    [InlineData("If-None-Match", false, HttpRepresentationPreconditionOutcome.Failed)]
    public void Evaluate_QuotedCommaInValidator_MatchesTheRepresentation(
        string header, bool read, HttpRepresentationPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = "\"other\", " + TagWithComma;

        var result = HttpRepresentationHeaders.Evaluate(request, true, TagWithComma, ModifiedAt, read);

        result.ShouldBe(expected);
    }

    /// <summary>Checks whole-second comparison and read-only handling of If-Modified-Since.</summary>
    /// <example><code>request.Headers.IfModifiedSince = "Thu, 05 Feb 2026 10:30:20 GMT";</code></example>
    [Theory]
    [InlineData("If-Unmodified-Since", ModifiedDate, false, HttpRepresentationPreconditionOutcome.Proceed)]
    [InlineData("If-Unmodified-Since", StaleDate, false, HttpRepresentationPreconditionOutcome.Failed)]
    [InlineData("If-Modified-Since", ModifiedDate, true, HttpRepresentationPreconditionOutcome.NotModified)]
    [InlineData("If-Modified-Since", ModifiedDate, false, HttpRepresentationPreconditionOutcome.Proceed)]
    [InlineData("If-Modified-Since", "invalid", true, HttpRepresentationPreconditionOutcome.Proceed)]
    public void Evaluate_DateCondition_ComparesWholeSeconds(
        string header, string date, bool read, HttpRepresentationPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = date;

        var result = HttpRepresentationHeaders.Evaluate(request, true, EntityTag, ModifiedAt, read);

        result.ShouldBe(expected);
    }

    /// <summary>Verifies that strict preconditions accept both obsolete HTTP-date formats.</summary>
    /// <example><code>request.Headers.IfModifiedSince = "Thu Feb  5 10:30:20 2026";</code></example>
    [Theory]
    [InlineData("If-Modified-Since", "Thursday, 05-Feb-26 10:30:20 GMT", HttpRepresentationPreconditionOutcome.NotModified)]
    [InlineData("If-Modified-Since", "Thu Feb  5 10:30:20 2026", HttpRepresentationPreconditionOutcome.NotModified)]
    [InlineData("If-Unmodified-Since", "Thursday, 05-Feb-26 10:30:19 GMT", HttpRepresentationPreconditionOutcome.Failed)]
    [InlineData("If-Unmodified-Since", "Thu Feb  5 10:30:19 2026", HttpRepresentationPreconditionOutcome.Failed)]
    public void Evaluate_ObsoleteHttpDate_ValidatesInStrictMode(
        string header, string date, HttpRepresentationPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = date;

        var strict = HttpRepresentationHeaders.Evaluate(request, true, EntityTag, ModifiedAt, read: true);
        var legacy = HttpRepresentationHeaders.Evaluate(
            request, true, EntityTag, ModifiedAt, read: true, mode: HttpRepresentationHeaderMode.LegacyPresentation);

        strict.ShouldBe(expected);
        legacy.ShouldBe(HttpRepresentationPreconditionOutcome.Proceed);
    }

    /// <summary>Checks ordinal comparison of trimmed, optionally weak candidates.</summary>
    /// <example><code>var match = HttpRepresentationHeaders.MatchesEntityTag("W/\"report-v1\"", "\"report-v1\"", weak: true);</code></example>
    [Theory]
    [InlineData("\"other\", \"report-v1\"", false, true)]
    [InlineData("W/\"report-v1\"", false, false)]
    [InlineData("W/\"report-v1\"", true, true)]
    [InlineData("w/\"report-v1\"", true, false)]
    [InlineData("\"other\", w/\"report-v1\"", true, false)]
    [InlineData("W/ \"report-v1\"", true, false)]
    [InlineData("\"REPORT-V1\"", true, false)]
    [InlineData("*", true, false)]
    [InlineData("*, \"report-v1\"", true, false)]
    [InlineData("\"report-v1\", invalid", false, false)]
    public void MatchesEntityTag_CandidateTags_UsesConfiguredComparison(string values, bool weak, bool expected)
    {
        var result = HttpRepresentationHeaders.MatchesEntityTag(values, EntityTag, weak);

        result.ShouldBe(expected);
    }

    /// <summary>Checks that one quoted tag can contain a comma in a list of candidates.</summary>
    /// <example><code>var match = HttpRepresentationHeaders.MatchesEntityTag("W/\"report,2026\", \"other\"", "\"report,2026\"", weak: true);</code></example>
    [Theory]
    [InlineData("\"other\", " + TagWithComma, false, true)]
    [InlineData("W/" + TagWithComma + ", \"other\"", true, true)]
    [InlineData("W/" + TagWithComma + ", \"other\"", false, false)]
    public void MatchesEntityTag_QuotedCommaInCandidate_KeepsTheTagIntact(string values, bool weak, bool expected)
    {
        var result = HttpRepresentationHeaders.MatchesEntityTag(values, TagWithComma, weak);

        result.ShouldBe(expected);
    }

    /// <summary>Checks the selection and byte boundaries for a single range request.</summary>
    /// <example><code>request.Headers.Range = "bytes=7-";</code></example>
    [Theory]
    [InlineData(null, 10, null, HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, null, HttpRepresentationRangeKind.Partial, 7, 3)]
    [InlineData("BYTES=7-", 10, null, HttpRepresentationRangeKind.Partial, 7, 3)]
    [InlineData("Bytes=7-", 10, null, HttpRepresentationRangeKind.Partial, 7, 3)]
    [InlineData("bytes=7-15", 10, EntityTag, HttpRepresentationRangeKind.Partial, 7, 3)]
    [InlineData("bytes=-3", 10, ModifiedDate, HttpRepresentationRangeKind.Partial, 7, 3)]
    [InlineData("bytes=-15", 10, null, HttpRepresentationRangeKind.Partial, 0, 10)]
    [InlineData("bytes=-0", 10, null, HttpRepresentationRangeKind.Unsatisfiable, 0, 0)]
    [InlineData("bytes=0-", 0, null, HttpRepresentationRangeKind.Unsatisfiable, 0, 0)]
    [InlineData("bytes=10-", 10, null, HttpRepresentationRangeKind.Unsatisfiable, 0, 0)]
    [InlineData("bytes=0-0,2-2", 10, null, HttpRepresentationRangeKind.Unsupported, 0, 0)]
    [InlineData("items=1-2", 10, null, HttpRepresentationRangeKind.Unsupported, 0, 0)]
    [InlineData("invalid", 10, null, HttpRepresentationRangeKind.Unsupported, 0, 0)]
    [InlineData("invalid", 10, "\"other\"", HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, "W/\"report-v1\"", HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, StaleDate, HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, "Thu, 05 Feb 2026 10:30:21 GMT", HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, EntityTag + ", \"other\"", HttpRepresentationRangeKind.Full, 0, 0)]
    [InlineData("bytes=7-", 10, " ", HttpRepresentationRangeKind.Full, 0, 0)]
    public void SelectRange_WithHeaders_ReturnsExpectedSelection(
        string range, long length, string ifRange, HttpRepresentationRangeKind kind, long offset, long selectedLength)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = range;
        request.Headers.IfRange = ifRange;

        var result = HttpRepresentationHeaders.SelectRange(request, length, EntityTag, ModifiedAt);

        result.ShouldBe(new HttpRepresentationRangeSelection(kind, offset, kind == HttpRepresentationRangeKind.Partial ? selectedLength : null));
    }

    /// <summary>Checks that If-Range recognizes a comma within a single quoted strong validator.</summary>
    /// <example><code>request.Headers.IfRange = "\"report,2026\"";</code></example>
    [Fact]
    public void SelectRange_QuotedCommaInIfRangeValidator_SelectsTheRange()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = "bytes=2-4";
        request.Headers.IfRange = TagWithComma;

        var result = HttpRepresentationHeaders.SelectRange(request, 10, TagWithComma, ModifiedAt);

        result.ShouldBe(new HttpRepresentationRangeSelection(HttpRepresentationRangeKind.Partial, 2, 3));
    }

    /// <summary>Checks that range selection rejects negative representation lengths.</summary>
    /// <example><code>var error = Should.Throw&lt;ArgumentOutOfRangeException&gt;(() => HttpRepresentationHeaders.SelectRange(request, -1, "\"report-v1\"", DateTimeOffset.UnixEpoch));</code></example>
    [Fact]
    public void SelectRange_NegativeLength_Throws()
    {
        var request = new DefaultHttpContext().Request;

        var error = Should.Throw<ArgumentOutOfRangeException>(
            () => HttpRepresentationHeaders.SelectRange(request, -1, EntityTag, ModifiedAt));

        error.ParamName.ShouldBe("length");
    }
}
