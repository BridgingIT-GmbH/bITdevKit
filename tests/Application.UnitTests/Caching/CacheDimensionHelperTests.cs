// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;

/// <summary>Exercises item and descriptor dimension helpers through schema admission and portable selections.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheDimensionHelperTests</code></example>
public sealed class CacheDimensionHelperTests
{
    /// <summary>Set helpers copy caller members and preserve canonical deduplication and selection behavior.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithSetDimension_CallerMutatesMembers_PreservesSnapshotAndMembership(bool discovered)
    {
        // Arrange
        var members = new[] { "West", "WEST", "east" };
        var schemas = new CacheDimensionSchema[] { new("regions", CacheDimensionKind.String, CacheDimensionForm.Set) };

        // Act
        var sut = SetDimensions(discovered, "regions", members);
        members[0] = "north";
        members[1] = "south";

        // Assert
        CacheSelectionValidator.ValidateDimensions(sut, schemas).IsSuccess.ShouldBeTrue();
        sut[0].Values.Count.ShouldBe(2);
        Matches(CacheSelection.Dimension("regions").Contains("west"), sut, schemas).ShouldBeTrue();
        Matches(CacheSelection.Dimension("regions").Contains("north"), sut, schemas).ShouldBeFalse();
    }

    /// <summary>Empty sets keep all supported declared kinds and remain present for selection.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithSetDimension_EmptySets_PreservePortableKindsAndPresence(bool discovered)
    {
        // Arrange
        var sets = new IReadOnlyList<CacheDimension>[]
        {
            SetDimensions<string>(discovered, "labels"),
            SetDimensions<bool>(discovered, "flags"),
            SetDimensions<short?>(discovered, "numbers"),
            SetDimensions<Guid>(discovered, "owners"),
            SetDimensions<DateTimeOffset>(discovered, "instants")
        };
        var schemas = new CacheDimensionSchema[]
        {
            new("labels", CacheDimensionKind.String, CacheDimensionForm.Set),
            new("flags", CacheDimensionKind.Boolean, CacheDimensionForm.Set),
            new("numbers", CacheDimensionKind.Integer, CacheDimensionForm.Set),
            new("owners", CacheDimensionKind.Identifier, CacheDimensionForm.Set),
            new("instants", CacheDimensionKind.Timestamp, CacheDimensionForm.Set)
        };

        // Act and assert
        for (var i = 0; i < sets.Length; i++)
        {
            var sut = sets[i];
            sut[0].Kind.ShouldBe(schemas[i].Kind);
            sut[0].Values.ShouldBeEmpty();
            CacheSelectionValidator.ValidateDimensions(sut, schemas).IsSuccess.ShouldBeTrue();
            Matches(CacheSelection.Dimension(schemas[i].Name).IsPresent(), sut, schemas).ShouldBeTrue();
            Matches(CacheSelection.Dimension(schemas[i].Name).IsAbsent(), sut, schemas).ShouldBeFalse();
        }

        Matches(CacheSelection.Dimension("labels").Contains("west"), sets[0], schemas).ShouldBeFalse();
    }

    /// <summary>Nullable members preserve their declared kind and require explicit schema permission.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithSetDimension_TypedNull_RequiresSchemaNullPermission(bool discovered)
    {
        // Arrange
        var denied = new CacheDimensionSchema[] { new("numbers", CacheDimensionKind.Integer, CacheDimensionForm.Set) };
        var allowed = new CacheDimensionSchema[] { denied[0] with { AllowNull = true } };

        // Act
        var sut = SetDimensions<int?>(discovered, "numbers", 42, null);

        // Assert
        sut[0].Kind.ShouldBe(CacheDimensionKind.Integer);
        sut[0].Values.Count.ShouldBe(2);
        CacheSelectionValidator.ValidateDimensions(sut, denied).IsFailure.ShouldBeTrue();
        CacheSelectionValidator.ValidateDimensions(sut, allowed).IsSuccess.ShouldBeTrue();
        Matches(CacheSelection.Dimension("numbers").Contains(null, CacheDimensionKind.Integer), sut, allowed).ShouldBeTrue();
    }

    /// <summary>Invalid names, unsupported declared types, null arrays, and oversized arrays use factory rejection.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithSetDimension_InvalidArguments_RejectWithoutWideningOrTruncation(bool discovered)
    {
        // Arrange
        var maximum = Enumerable.Repeat("west", 256).ToArray();
        var oversized = Enumerable.Repeat("west", 257).ToArray();

        // Act and assert
        SetDimensions(discovered, "labels", maximum)[0].Values.Count.ShouldBe(1);
        Should.Throw<ArgumentNullException>(() => SetDimensions<string>(discovered, "labels", null));
        Should.Throw<ArgumentException>(() => SetDimensions(discovered, "labels", oversized));
        Should.Throw<ArgumentException>(() => SetDimensions<decimal>(discovered, "prices"));
        Should.Throw<ArgumentException>(() => SetDimensions<object>(discovered, "labels", "west"));
        Should.Throw<ArgumentException>(() => SetDimensions(discovered, " ", "west"));
    }

    /// <summary>Integer intervals retain Int64 precision and exclude the upper endpoint when selecting overlaps.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithIntervalDimension_IntegerEndpoints_PreservesHalfOpenSelection(bool discovered)
    {
        // Arrange
        var schemas = new CacheDimensionSchema[] { new("sequence", CacheDimensionKind.Integer, CacheDimensionForm.Interval) };

        // Act
        var sut = IntervalDimensions(discovered, "sequence", long.MaxValue - 3, long.MaxValue - 1);

        // Assert
        CacheSelectionValidator.ValidateDimensions(sut, schemas).IsSuccess.ShouldBeTrue();
        Matches(CacheSelection.Dimension("sequence").Overlaps(long.MaxValue - 2, long.MaxValue), sut, schemas).ShouldBeTrue();
        Matches(CacheSelection.Dimension("sequence").Overlaps(long.MaxValue - 1, long.MaxValue), sut, schemas).ShouldBeFalse();
    }

    /// <summary>Timestamp intervals normalize offsets and retain tick precision with the existing factory.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithIntervalDimension_TimestampEndpoints_NormalizesUtcAndPreservesPrecision(bool discovered)
    {
        // Arrange
        var start = DateTimeOffset.UnixEpoch.AddTicks(1).ToOffset(TimeSpan.FromHours(3));
        var schemas = new CacheDimensionSchema[] { new("time", CacheDimensionKind.Timestamp, CacheDimensionForm.Interval) };

        // Act
        var sut = IntervalDimensions(discovered, "time", start, start.AddTicks(1));

        // Assert
        CacheSelectionValidator.ValidateDimensions(sut, schemas).IsSuccess.ShouldBeTrue();
        Matches(CacheSelection.Dimension("time").Overlaps(start.ToUniversalTime(), start.AddTicks(1).ToUniversalTime()), sut, schemas).ShouldBeTrue();
        Matches(CacheSelection.Dimension("time").Overlaps(start.AddTicks(1), start.AddTicks(2)), sut, schemas).ShouldBeFalse();
    }

    /// <summary>Intervals reject null, unordered, and unsupported endpoints rather than creating an empty range.</summary>
    /// <param name="discovered">Whether the dependency is discovered by the source instead of supplied in the item.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithIntervalDimension_InvalidEndpoints_RejectsUnsupportedOrEmptyIntervals(bool discovered)
    {
        // Act and assert
        Should.Throw<ArgumentException>(() => IntervalDimensions(discovered, "sequence", 2, 1));
        Should.Throw<ArgumentException>(() => IntervalDimensions(discovered, "sequence", 1, 1));
        Should.Throw<ArgumentException>(() => IntervalDimensions<int?>(discovered, "sequence", null, 1));
        Should.Throw<ArgumentException>(() => IntervalDimensions(discovered, "labels", "a", "b"));
        Should.Throw<ArgumentException>(() => IntervalDimensions(discovered, "prices", 1m, 2m));
        Should.Throw<ArgumentException>(() => IntervalDimensions<object>(discovered, "sequence", 1L, 2L));
    }

    /// <summary>Both helpers retain existing metadata snapshots and the sixteen-dimension limit.</summary>
    [Fact]
    public void DimensionHelpers_ExistingDependencies_PreserveSnapshotsAndRejectSeventeenthDimension()
    {
        // Arrange
        var original = new List<CacheDimension> { CacheDimension.Scalar("region", "west") };
        var item = new CacheItem("report:42", original);
        var descriptor = new CacheEntryDescriptor { Dimensions = original, AbsoluteTtl = TimeSpan.FromMinutes(2) };

        // Act
        var setItem = item.WithSetDimension("owners", Guid.Empty);
        var intervalItem = item.WithIntervalDimension("sequence", 1, 2);
        var setDescriptor = descriptor.WithSetDimension("owners", Guid.Empty);
        var intervalDescriptor = descriptor.WithIntervalDimension("sequence", 1, 2);
        original.Clear();
        var full = Enumerable.Range(0, 16).Select(index => CacheDimension.Scalar("d" + index, index)).ToArray();

        // Assert
        setItem.Key.ShouldBe("report:42");
        intervalItem.Key.ShouldBe("report:42");
        foreach (var dimensions in new[] { setItem.Dimensions, intervalItem.Dimensions, setDescriptor.Dimensions, intervalDescriptor.Dimensions })
        {
            dimensions.Count.ShouldBe(2);
            dimensions[0].Name.ShouldBe("REGION");
        }

        setDescriptor.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(2));
        intervalDescriptor.AbsoluteTtl.ShouldBe(TimeSpan.FromMinutes(2));
        Should.Throw<ArgumentException>(() => new CacheItem("full", full).WithSetDimension("extra", 1));
        Should.Throw<ArgumentException>(() => new CacheItem("full", full).WithIntervalDimension("extra", 1, 2));
        Should.Throw<ArgumentException>(() => new CacheEntryDescriptor { Dimensions = full }.WithSetDimension("extra", 1));
        Should.Throw<ArgumentException>(() => new CacheEntryDescriptor { Dimensions = full }.WithIntervalDimension("extra", 1, 2));
    }

    private static IReadOnlyList<CacheDimension> SetDimensions<T>(bool discovered, string name, params T[] values) =>
        discovered ? new CacheEntryDescriptor().WithSetDimension(name, values).Dimensions
            : new CacheItem("report:42").WithSetDimension(name, values).Dimensions;

    private static IReadOnlyList<CacheDimension> IntervalDimensions<T>(bool discovered, string name, T lower, T upper) =>
        discovered ? new CacheEntryDescriptor().WithIntervalDimension(name, lower, upper).Dimensions
            : new CacheItem("report:42").WithIntervalDimension(name, lower, upper).Dimensions;

    private static bool Matches(CacheSelection selection, IReadOnlyList<CacheDimension> dimensions, IReadOnlyList<CacheDimensionSchema> schemas) =>
        new CacheSelectionEvaluator(selection, schemas, DateTimeOffset.UnixEpoch).Matches(new CacheEntryMetadata
        {
            Identity = new("app", "reports", "v1", "report:42"),
            PublishedAt = DateTimeOffset.UnixEpoch,
            Dimensions = dimensions
        });
}