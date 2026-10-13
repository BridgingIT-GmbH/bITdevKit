// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Profiling;

/// <summary>Verifies bounded typed dimension discovery independently of visible dashboard rows.</summary>
public sealed class OperationProfilingDimensionTests
{
    /// <summary>Legacy execution IDs do not become choices, while business identifiers remain discoverable on each owner.</summary>
    /// <example><code>await suite.Discovery_ExecutionIdentifiers_ExcludesLegacyIds(ProfilingDimensionScope.Operation);</code></example>
    [Theory]
    [InlineData(ProfilingDimensionScope.Operation)]
    [InlineData(ProfilingDimensionScope.Segment)]
    public async Task Discovery_ExecutionIdentifiers_ExcludesLegacyIds(ProfilingDimensionScope scope)
    {
        // Arrange
        var identifiers = new[] { "orchestration.instanceId", "orchestration.correlationId", "job.executionId", "pipeline.executionId" };
        var dimensions = identifiers.Select(key => new ProfilingDimension
        {
            Key = key.ToUpperInvariant(), Value = new(ProfilingValueType.String, Guid.NewGuid().ToString("D")),
        }).Append(new() { Key = "cityId", Value = new(ProfilingValueType.String, "berlin") }).ToArray();
        var path = new ProfilingSegmentPath(["Work"]);
        var record = new OperationProfilingRecord
        {
            Dimensions = dimensions,
            Segments = [new() { Key = "Work", Path = path, Dimensions = dimensions.Select(dimension => new ProfilingSegmentDimensionSummary { Key = dimension.Key, Value = dimension.Value }).ToArray() }],
        };
        var sut = Create([record]);
        var query = new OperationProfilingQuery { SegmentPath = scope == ProfilingDimensionScope.Segment ? path : null };

        // Act
        var discovery = await sut.DiscoverDimensionsAsync(query, scope);

        // Assert
        discovery.IsSuccess.ShouldBeTrue();
        discovery.Value.OperationCount.ShouldBe(1);
        discovery.Value.MatchingNameCount.ShouldBe(1);
        discovery.Value.Dimensions.ShouldHaveSingleItem().Key.ShouldBe("cityId");
        foreach (var key in identifiers)
        {
            var searched = await sut.DiscoverDimensionsAsync(query, scope, dimensionKey: key);
            searched.IsSuccess.ShouldBeTrue();
            searched.Value.MatchingNameCount.ShouldBe(0);
            searched.Value.Dimensions.ShouldBeEmpty();
        }
    }

    /// <summary>Names ignore casing while scalars preserve type, casing, empty strings, missing values and integer precision.</summary>
    /// <example><code>await suite.Discovery_TypedValues_PreservesIdentityAndCounts();</code></example>
    [Fact]
    public async Task Discovery_TypedValues_PreservesIdentityAndCounts()
    {
        var records = new[]
        {
            Record("Category", new(ProfilingValueType.String, "20")), Record("category", new(ProfilingValueType.Int64, "20")),
            Record("CATEGORY", new(ProfilingValueType.String, "")), Record("Category", new(ProfilingValueType.String, "Books")),
            Record("category", new(ProfilingValueType.String, "books")), Record("category", new(ProfilingValueType.Int64, "9007199254740993")), new OperationProfilingRecord(),
        };
        var sut = Create(records);

        var result = await sut.DiscoverDimensionsAsync(new() { PageSize = 1, Cursor = "ignored-page" });

        result.IsSuccess.ShouldBeTrue();
        result.Value.OperationCount.ShouldBe(7);
        var choice = result.Value.Dimensions.Single();
        choice.PresentCount.ShouldBe(6);
        choice.MissingCount.ShouldBe(1);
        choice.DistinctValueCount.ShouldBe(6);
        choice.Values.ShouldContain(value => value.Value == new ProfilingValue(ProfilingValueType.Int64, "9007199254740993"));
        choice.Values.Count(value => value.Value.Scalar == "20").ShouldBe(2);
        choice.Values.Count(value => value.Value.Scalar == "Books" || value.Value.Scalar == "books").ShouldBe(2);
        choice.Values.ShouldContain(value => value.Value == new ProfilingValue(ProfilingValueType.String, ""));
    }

    /// <summary>Search can find values beyond the bounded most frequent choices without presenting a partial scan as exact.</summary>
    /// <example><code>await suite.Discovery_BoundedChoices_SearchesAllSelectedRecords();</code></example>
    [Fact]
    public async Task Discovery_BoundedChoices_SearchesAllSelectedRecords()
    {
        var records = Enumerable.Range(0, 40).Select(index => Record("category", new(ProfilingValueType.String, "item-" + index.ToString("D2")))).ToArray();
        var options = new ProfilingOptions { Enabled = true };
        options.Queries.MaximumDimensionValues = 3;
        var sut = Create(records, options);

        var initial = await sut.DiscoverDimensionsAsync(new() { PageSize = 1 }, nameSearch: "CATEG");
        var search = await sut.DiscoverDimensionsAsync(new(), dimensionKey: "CATEGORY", valueSearch: "item-39");

        initial.Value.Dimensions.Single().Values.Count.ShouldBe(3);
        initial.Value.Dimensions.Single().ValuesTruncated.ShouldBeTrue();
        initial.Value.OperationCount.ShouldBe(40);
        search.Value.Dimensions.Single().Values.Single().Value.Scalar.ShouldBe("item-39");
        search.Value.Dimensions.Single().DistinctValueCount.ShouldBe(40);
        search.Value.Dimensions.Single().ValuesTruncated.ShouldBeFalse();
    }

    /// <summary>Operation and full-path segment metadata stay distinct; mixed and partial summaries are not equality choices.</summary>
    /// <example><code>await suite.Discovery_SegmentScope_RequiresPathAndSeparatesOwners();</code></example>
    [Fact]
    public async Task Discovery_SegmentScope_RequiresPathAndSeparatesOwners()
    {
        var path = new ProfilingSegmentPath(["Load", "Read"]);
        var uniform = Record("source", new(ProfilingValueType.String, "operation")) with
        {
            Segments = [new() { Key = "Read", Path = path, Dimensions = [new() { Key = "source", Value = new(ProfilingValueType.String, "segment") }] },
                new() { Key = "Read", Path = new(["Other", "Read"]), Dimensions = [new() { Key = "source", Value = new(ProfilingValueType.String, "sibling") }] }],
        };
        var mixed = uniform with { Segments = [new() { Key = "Read", Path = path, Dimensions = [new() { Key = "source", Mixed = true }] }] };
        var partial = uniform with { Segments = [new() { Key = "Read", Path = path, Dimensions = [new() { Key = "source", Partial = true, Value = new(ProfilingValueType.String, "partial") }] }] };
        var missing = uniform with { Segments = [new() { Key = "Read", Path = path }] };
        var sut = Create([uniform, mixed, partial, missing]);

        (await sut.DiscoverDimensionsAsync(new(), ProfilingDimensionScope.Segment)).Errors.ShouldContain(error => error is ProfilingValidationError);
        var result = await sut.DiscoverDimensionsAsync(new() { SegmentPath = new(["load", "READ"]) }, ProfilingDimensionScope.Segment);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Scope.ShouldBe(ProfilingDimensionScope.Segment);
        var choice = result.Value.Dimensions.Single();
        choice.PresentCount.ShouldBe(3);
        choice.MissingCount.ShouldBe(1);
        choice.MixedCount.ShouldBe(1);
        choice.PartialCount.ShouldBe(1);
        choice.Values.Single().Value.Scalar.ShouldBe("segment");
        result.Value.SegmentPaths.ShouldContain(value => value.Path.Equals(path) && value.Count == 4);
    }

    /// <summary>Providers cannot bypass the service's exact analysis limit.</summary>
    /// <example><code>await suite.Discovery_ProviderOverLimit_ReturnsExplicitLimit();</code></example>
    [Fact]
    public async Task Discovery_ProviderOverLimit_ReturnsExplicitLimit()
    {
        var sut = Create([new(), new()]);
        var result = await sut.DiscoverDimensionsAsync(new() { MaximumAnalysisCount = 1 });
        result.Errors.ShouldContain(error => error is ProfilingQueryLimitError);
    }

    /// <summary>Names are bounded independently of value choices, and searched names remain discoverable.</summary>
    /// <example><code>await suite.Discovery_NameLimit_ReportsOmissions();</code></example>
    [Fact]
    public async Task Discovery_NameLimit_ReportsOmissions()
    {
        var options = new ProfilingOptions { Enabled = true };
        options.Queries.MaximumDimensionNames = 2;
        var sut = Create(Enumerable.Range(0, 5).Select(index => Record("name-" + index, new(ProfilingValueType.Boolean, "true"))).ToArray(), options);

        var initial = await sut.DiscoverDimensionsAsync(new());
        var searched = await sut.DiscoverDimensionsAsync(new(), nameSearch: "name-4");

        initial.Value.MatchingNameCount.ShouldBe(5);
        initial.Value.NamesTruncated.ShouldBeTrue();
        initial.Value.Dimensions.Count.ShouldBe(2);
        searched.Value.Dimensions.Single().Key.ShouldBe("name-4");
        searched.Value.NamesTruncated.ShouldBeFalse();
    }

    /// <summary>Invalid scope and unbounded search are rejected before provider reads.</summary>
    /// <example><code>await suite.Discovery_InvalidInput_RejectsBeforeReading();</code></example>
    [Fact]
    public async Task Discovery_InvalidInput_RejectsBeforeReading()
    {
        var sut = Create([]);
        (await sut.DiscoverDimensionsAsync(new(), (ProfilingDimensionScope)99)).Errors.ShouldContain(error => error is ProfilingValidationError);
        (await sut.DiscoverDimensionsAsync(new(), nameSearch: new string('x', 257))).Errors.ShouldContain(error => error is ProfilingValidationError);
        (await sut.DiscoverDimensionsAsync(new(), dimensionKey: "")).Errors.ShouldContain(error => error is ProfilingValidationError);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => sut.DiscoverDimensionsAsync(new(), cancellationToken: cancellation.Token));
    }

    private static OperationProfilingRecord Record(string key, ProfilingValue value) => new() { Dimensions = [new() { Key = key, Value = value }] };
    private static OperationProfilingQueryService Create(IReadOnlyList<OperationProfilingRecord> records, ProfilingOptions options = null)
    {
        var store = Substitute.For<IOperationProfilingStore>();
        store.SelectAnalysisAsync(Arg.Any<OperationProfilingQuery>(), Arg.Any<CancellationToken>()).Returns(Result<OperationProfilingAnalysisSelection>.Success(new() { Records = records }));
        var provider = Substitute.For<IProfilingStorageProvider>(); provider.Operations.Returns(store);
        var health = Substitute.For<IOperationProfilingHealthSource>(); health.GetSnapshot().Returns(new OperationProfilingHealth());
        return new(provider, options ?? new() { Enabled = true }, health);
    }
}
