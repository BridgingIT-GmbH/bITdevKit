// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Http;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;

/// <summary>Checks shared HTTP condition ordering, validator comparisons and byte-range selection.</summary>
public sealed class HttpRepresentationConditionsTests
{
    private static readonly DateTimeOffset ModifiedAt = new(2026, 2, 5, 10, 30, 20, 900, TimeSpan.Zero);
    private const string EntityTag = "\"report-v1\"";

    /// <summary>Requests without conditions proceed whether the representation exists or is missing.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Evaluate_NoConditions_ProceedsRegardlessOfExistence(bool exists, bool read)
    {
        var request = new DefaultHttpContext().Request;

        var result = HttpRepresentationConditions.Evaluate(request, exists, exists ? EntityTag : null, ModifiedAt, read);

        result.ShouldBe(HttpPreconditionOutcome.Proceed);
    }

    /// <summary>A failed If-Match takes precedence over a matching If-None-Match.</summary>
    [Fact]
    public void Evaluate_FailedIfMatchBeforeMatchingIfNoneMatch_Fails()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfMatch = "\"other\"";
        request.Headers.IfNoneMatch = "*";

        var result = HttpRepresentationConditions.Evaluate(request, true, EntityTag, ModifiedAt, read: true);

        result.ShouldBe(HttpPreconditionOutcome.Failed);
    }

    /// <summary>A present If-Match suppresses If-Unmodified-Since after its successful comparison.</summary>
    [Fact]
    public void Evaluate_MatchingIfMatchAndStaleUnmodifiedDate_Proceeds()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfMatch = EntityTag;
        request.Headers.IfUnmodifiedSince = ModifiedAt.AddDays(-1).ToString("r", CultureInfo.InvariantCulture);

        var result = HttpRepresentationConditions.Evaluate(request, true, EntityTag, ModifiedAt, read: false);

        result.ShouldBe(HttpPreconditionOutcome.Proceed);
    }

    /// <summary>If-None-Match suppresses If-Modified-Since even when no candidate validator matches.</summary>
    [Fact]
    public void Evaluate_NonmatchingIfNoneMatchAndFutureModifiedDate_Proceeds()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfNoneMatch = "\"other\"";
        request.Headers.IfModifiedSince = ModifiedAt.AddDays(1).ToString("r", CultureInfo.InvariantCulture);

        var result = HttpRepresentationConditions.Evaluate(request, true, EntityTag, ModifiedAt, read: true);

        result.ShouldBe(HttpPreconditionOutcome.Proceed);
    }

    /// <summary>A matching weak If-None-Match has distinct read and mutation outcomes.</summary>
    [Theory]
    [InlineData(true, HttpPreconditionOutcome.NotModified)]
    [InlineData(false, HttpPreconditionOutcome.Failed)]
    public void Evaluate_WeakIfNoneMatch_UsesReadOrMutationOutcome(bool read, HttpPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfNoneMatch = "W/" + EntityTag;

        var result = HttpRepresentationConditions.Evaluate(request, true, EntityTag, ModifiedAt, read);

        result.ShouldBe(expected);
    }

    /// <summary>Wildcard conditions use representation existence rather than the supplied validator.</summary>
    [Theory]
    [InlineData("If-Match", false, HttpPreconditionOutcome.Failed)]
    [InlineData("If-Match", true, HttpPreconditionOutcome.Proceed)]
    [InlineData("If-None-Match", false, HttpPreconditionOutcome.Proceed)]
    [InlineData("If-None-Match", true, HttpPreconditionOutcome.NotModified)]
    public void Evaluate_Wildcards_UseSelectedExistence(string header, bool exists, HttpPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = "*";

        var result = HttpRepresentationConditions.Evaluate(request, exists, exists ? EntityTag : null, ModifiedAt, read: true);

        result.ShouldBe(expected);
    }

    /// <summary>Date comparisons round the selected representation down to HTTP whole seconds.</summary>
    [Theory]
    [InlineData("If-Unmodified-Since", true, 0, HttpPreconditionOutcome.Proceed)]
    [InlineData("If-Unmodified-Since", true, -1, HttpPreconditionOutcome.Failed)]
    [InlineData("If-Modified-Since", true, 0, HttpPreconditionOutcome.NotModified)]
    [InlineData("If-Modified-Since", true, -1, HttpPreconditionOutcome.Proceed)]
    [InlineData("If-Modified-Since", false, 0, HttpPreconditionOutcome.Proceed)]
    public void Evaluate_HttpDates_CompareWholeSeconds(string header, bool read, int seconds, HttpPreconditionOutcome expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[header] = ModifiedAt.AddSeconds(seconds).ToString("r", CultureInfo.InvariantCulture);

        var result = HttpRepresentationConditions.Evaluate(request, true, EntityTag, ModifiedAt, read);

        result.ShouldBe(expected);
    }

    /// <summary>Malformed dates and dates for missing representations do not impose a condition.</summary>
    [Theory]
    [InlineData(true, "not-an-http-date")]
    [InlineData(false, "Thu, 05 Feb 2026 00:00:00 GMT")]
    public void Evaluate_InapplicableDates_Proceeds(bool exists, string date)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfModifiedSince = date;
        request.Headers.IfUnmodifiedSince = date;

        var result = HttpRepresentationConditions.Evaluate(request, exists, exists ? EntityTag : null, ModifiedAt, read: true);

        result.ShouldBe(HttpPreconditionOutcome.Proceed);
    }

    /// <summary>Validator lists compare ordinally, treating only uppercase W/ as a weak candidate prefix.</summary>
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(" ", false, false)]
    [InlineData(EntityTag, false, true)]
    [InlineData("\"other\", \"report-v1\"", false, true)]
    [InlineData("\"other\", W/\"report-v1\"", true, true)]
    [InlineData("W/\"report-v1\"", false, false)]
    [InlineData("w/\"report-v1\"", true, false)]
    [InlineData("\"REPORT-V1\"", true, false)]
    [InlineData("*", true, false)]
    public void MatchesEntityTag_ValidatorList_PreservesComparisonRules(string candidates, bool weak, bool expected)
    {
        var result = HttpRepresentationConditions.MatchesEntityTag(candidates, EntityTag, weak);

        result.ShouldBe(expected);
        HttpRepresentationConditions.MatchesEntityTag(candidates, null, weak).ShouldBeFalse();
    }

    /// <summary>Closed, open-ended and suffix ranges select the same logical bytes and clamp to the representation length.</summary>
    [Theory]
    [InlineData("bytes=2-4", 2L, 3L)]
    [InlineData("bytes=8-", 8L, 2L)]
    [InlineData("bytes=-3", 7L, 3L)]
    [InlineData("bytes=-20", 0L, 10L)]
    [InlineData("bytes=0-99", 0L, 10L)]
    [InlineData("bytes=0-0", 0L, 1L)]
    public void SelectRange_SingleRange_SelectsBoundedBytes(string range, long offset, long length)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = range;

        var result = HttpRepresentationConditions.SelectRange(request, 10, EntityTag, ModifiedAt);

        result.ShouldBe(new HttpRangeSelection(HttpRangeSelectionKind.Partial, offset, length));
    }

    /// <summary>An absent range selects the full representation even when it is empty.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(10L)]
    public void SelectRange_NoRange_SelectsFullRepresentation(long length)
    {
        var request = new DefaultHttpContext().Request;

        var result = HttpRepresentationConditions.SelectRange(request, length, EntityTag, ModifiedAt);

        result.ShouldBe(new HttpRangeSelection(HttpRangeSelectionKind.Full));
    }

    /// <summary>Malformed, multi-range and unsupported-unit headers keep their unsupported classification.</summary>
    [Theory]
    [InlineData("items=0-1")]
    [InlineData("bytes=1-2,4-5")]
    [InlineData("garbage")]
    [InlineData("bytes=5-2")]
    public void SelectRange_UnsupportedRange_ReportsUnsupported(string range)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = range;

        var result = HttpRepresentationConditions.SelectRange(request, 10, EntityTag, ModifiedAt);

        result.Outcome.ShouldBe(HttpRangeSelectionKind.Unsupported);
    }

    /// <summary>Ranges selecting no bytes have a distinct unsatisfiable classification.</summary>
    [Theory]
    [InlineData("bytes=0-", 0L)]
    [InlineData("bytes=10-", 10L)]
    [InlineData("bytes=-0", 10L)]
    public void SelectRange_UnsatisfiableRange_ReportsUnsatisfiable(string range, long length)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = range;

        var result = HttpRepresentationConditions.SelectRange(request, length, EntityTag, ModifiedAt);

        result.Outcome.ShouldBe(HttpRangeSelectionKind.Unsatisfiable);
    }

    /// <summary>If-Range requires a matching strong entity tag or a sufficiently recent HTTP date.</summary>
    [Theory]
    [InlineData(EntityTag, HttpRangeSelectionKind.Partial)]
    [InlineData("W/\"report-v1\"", HttpRangeSelectionKind.Full)]
    [InlineData("\"other\"", HttpRangeSelectionKind.Full)]
    [InlineData("Thu, 05 Feb 2026 10:30:20 GMT", HttpRangeSelectionKind.Partial)]
    [InlineData("Thu, 05 Feb 2026 10:30:19 GMT", HttpRangeSelectionKind.Full)]
    public void SelectRange_IfRange_UsesStrongValidatorOrRoundedDate(string ifRange, HttpRangeSelectionKind expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = "bytes=2-4";
        request.Headers.IfRange = ifRange;

        var result = HttpRepresentationConditions.SelectRange(request, 10, EntityTag, ModifiedAt);

        result.Outcome.ShouldBe(expected);
    }

    /// <summary>A failed If-Range selects the full representation before any malformed Range is parsed.</summary>
    [Fact]
    public void SelectRange_MismatchingIfRangeBeforeMalformedRange_SelectsFullRepresentation()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = "malformed";
        request.Headers.IfRange = "\"other\"";

        var result = HttpRepresentationConditions.SelectRange(request, 10, EntityTag, ModifiedAt);

        result.ShouldBe(new HttpRangeSelection(HttpRangeSelectionKind.Full));
    }
}