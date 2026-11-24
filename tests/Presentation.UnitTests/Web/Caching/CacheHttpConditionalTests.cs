// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Caching;

using BridgingIT.DevKit.Application.Caching;
using BridgingIT.DevKit.Presentation.Web.Caching;
using Microsoft.AspNetCore.Http;

/// <summary>Checks the public cache presentation behavior.</summary>
public class CacheHttpConditionalTests
{
    /// <summary>Verifies entity tag is strong opaque and bound to the fixed representation.</summary>
    [Fact]
    public void EntityTag_IsStrongOpaqueAndBoundToTheFixedRepresentation()
    {
        var version = new CacheEntryVersion("opaque-version");
        var sut = CacheEntityTag.Create(version, "card/v1/encoded");

        CacheEntityTag.TryGetVersion(sut, "card/v1/encoded", out var decoded).ShouldBeTrue();
        decoded.ShouldBe(version);
        CacheEntityTag.TryGetVersion(sut, "other/v1/encoded", out _).ShouldBeFalse();
        CacheEntityTag.TryGetVersion("W/" + sut, "card/v1/encoded", out _).ShouldBeFalse();
    }

    /// <summary>Verifies get conditions apply if match before if none match.</summary>
    [Fact]
    public void GetConditions_ApplyIfMatchBeforeIfNoneMatch()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"mismatch\"";
        context.Request.Headers.IfNoneMatch = "*";

        var result = CacheHttpPreconditions.Evaluate(context.Request, Positive(), "raw/v1", true);

        result.IsFailure.ShouldBeTrue();
        result.Errors.OfType<CacheError>().Single().Code.ShouldBe(CacheErrorCode.Conflict);
    }

    /// <summary>Verifies matching weak if none match returns payload free not modified.</summary>
    [Fact]
    public void MatchingWeakIfNoneMatch_ReturnsPayloadFreeNotModified()
    {
        var context = new DefaultHttpContext();
        var lookup = Positive();
        context.Request.Headers.IfNoneMatch = "W/" + CacheEntityTag.Create(lookup.Version.Value, "raw/v1");

        var result = CacheHttpPreconditions.Evaluate(context.Request, lookup, "raw/v1", true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.NotModified.ShouldBeTrue();
    }

    /// <summary>Verifies create only treats miss and confirmed absence as absent.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateOnly_TreatsMissAndConfirmedAbsenceAsAbsent(bool negative)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "*";
        var lookup = new CacheLookup<CacheEntryMetadata>(false, null, negative ? CacheLookupOutcome.Hit : CacheLookupOutcome.Miss,
            CacheStorageOutcome.None, isNotFound: negative, version: negative ? new("marker") : null);

        var result = CacheHttpPreconditions.Evaluate(context.Request, lookup, "raw/v1", false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Condition.CreateOnly.ShouldBeTrue();
    }

    /// <summary>Verifies byte ranges resolve against the acquired logical snapshot.</summary>
    [Theory]
    [InlineData("bytes=2-4", 2L, 3L)]
    [InlineData("bytes=8-", 8L, 2L)]
    [InlineData("bytes=-3", 7L, 3L)]
    public void ByteRanges_ResolveAgainstTheAcquiredLogicalSnapshot(string range, long offset, long length)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Range = range;

        var result = CacheHttpPreconditions.ReadOptions(context.Request, Positive().Value, "raw/v1");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Offset.ShouldBe(offset);
        result.Value.Length.ShouldBe(length);
        result.Value.ExpectedVersion.ShouldBe(Positive().Version);
    }

    /// <summary>Verifies unsatisfiable range returns the dedicated416 classification.</summary>
    [Fact]
    public void UnsatisfiableRange_ReturnsTheDedicated416Classification()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Range = "bytes=10-";

        var result = CacheHttpPreconditions.ReadOptions(context.Request, Positive().Value, "raw/v1");

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(error => error is CacheHttpRangeError);
    }

    /// <summary>Verifies unconditioned read requires no metadata preflight.</summary>
    [Fact]
    public void UnconditionedRead_RequiresNoMetadataPreflight()
    {
        CacheHttpPreconditions.RequiresMetadata(new DefaultHttpContext().Request).ShouldBeFalse();
    }

    /// <summary>A date header still maps to an atomic cache version condition even when its date cannot be parsed.</summary>
    [Fact]
    public void Mutation_InvalidDateHeader_PreservesAtomicVersionCondition()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfUnmodifiedSince = "invalid-date";
        var lookup = Positive();

        var result = CacheHttpPreconditions.Evaluate(request, lookup, "raw/v1", read: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.NotModified.ShouldBeFalse();
        result.Value.Condition.ExpectedVersion.ShouldBe(lookup.Version);
    }

    /// <summary>Ignoring a malformed range after failed If-Range still pins the returned cache read to its acquired version.</summary>
    [Fact]
    public void ReadOptions_MismatchingIfRange_PreservesVersionForFullRead()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.Range = "invalid-range";
        request.Headers.IfRange = "\"other\"";
        var metadata = Positive().Value;

        var result = CacheHttpPreconditions.ReadOptions(request, metadata, "raw/v1");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Offset.ShouldBeNull();
        result.Value.Length.ShouldBeNull();
        result.Value.ExpectedVersion.ShouldBe(metadata.Version);
    }

    private static CacheLookup<CacheEntryMetadata> Positive()
    {
        var metadata = new CacheEntryMetadata { Version = new("version"), LogicalSizeBytes = 10, PublishedAt = DateTimeOffset.UtcNow };
        return new(true, metadata, CacheLookupOutcome.Hit, CacheStorageOutcome.None, version: metadata.Version);
    }
}