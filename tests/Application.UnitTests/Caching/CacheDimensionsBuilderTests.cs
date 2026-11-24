// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.Caching;

using BridgingIT.DevKit.Application.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Checks grouped dimension declarations through collection admission, invalidation, and startup composition.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~CacheDimensionsBuilderTests</code></example>
public sealed class CacheDimensionsBuilderTests
{
    /// <summary>Grouped scalar, set, and interval schemas govern actual writes and dependency invalidation.</summary>
    /// <returns>A task that completes after the declared dimensions select and invalidate a retained value.</returns>
    [Fact]
    public async Task Dimensions_DeclaredShapes_ValidateWritesAndSelectInvalidation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplicationCaching(cache => cache
            .WithInMemoryProvider("memory", p => p.Capacity(ByteSize.Megabytes(8)).MaxEntries(256))
            .WithCollection<string>("reports", collection => collection.UseProvider("memory")
                .Limits(l => l.MaxPayloadBytes(ByteSize.Megabytes(2)).MaxDecodedBytes(ByteSize.Megabytes(2)))
                .Dimensions(d => d.Set<string>("tags")
                    .Scalar<string>("region", allowNull: true).Scalar<Guid>("owner")
                    .Interval<long>("sequence").Interval<DateTimeOffset>("coverage"))));
        await using var provider = services.BuildServiceProvider();
        var sut = provider.GetRequiredService<IApplicationCache>().GetCollection<string>("reports");
        var start = DateTimeOffset.UnixEpoch;
        var item = new CacheItem("west").WithSetDimension("tags", "sales", "returns")
            .WithScalarDimension("region", null, CacheDimensionKind.String).WithScalarDimension("owner", Guid.Empty)
            .WithIntervalDimension("sequence", 10L, 20L).WithIntervalDimension("coverage", start, start.AddHours(1));

        // Act
        var written = await sut.SetAsync(item, "summary");
        var read = await sut.GetAsync(item.Key);
        var rejected = await sut.SetAsync(new CacheItem("invalid").WithScalarDimension("region", 42), "wrong kind");
        var invalidated = await sut.InvalidateAsync(CacheSelection.And(
            CacheSelection.Dimension("tags").Contains("sales"),
            CacheSelection.Dimension("region").EqualTo(null, CacheDimensionKind.String),
            CacheSelection.Dimension("owner").EqualTo(Guid.Empty),
            CacheSelection.Dimension("coverage").Overlaps(start, start.AddMinutes(30))));

        // Assert
        written.IsSuccess.ShouldBeTrue(string.Join("; ", written.Errors.Select(error => error.Message)));
        read.IsSuccess.ShouldBeTrue();
        read.Value.Value.ShouldBe("summary");
        rejected.IsFailure.ShouldBeTrue();
        invalidated.IsSuccess.ShouldBeTrue();
        (await sut.GetAsync(item.Key)).Value.Found.ShouldBeFalse();
    }

    /// <summary>Repeated grouped callbacks append to individual declarations without resetting their order.</summary>
    [Fact]
    public void Dimensions_RepeatedAndIndividualDeclarations_AppendInOrder()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching(cache => cache.WithCollection<string>("reports", collection => collection
            .WithScalarDimension<long>("initial")
            .Dimensions(d => d.Set<string>("tags"))
            .Dimensions(d => d.Scalar<bool>("active").Interval<DateTimeOffset>("coverage"))
            .WithDimension(new("owner", CacheDimensionKind.Identifier))));

        // Act
        var sut = builder.BuildCatalog().Collections["reports"].Options.Dimensions;

        // Assert
        sut.Select(schema => schema.Name).ShouldBe(new[] { "INITIAL", "TAGS", "ACTIVE", "COVERAGE", "OWNER" });
        sut.Select(schema => schema.Form).ShouldBe(new[]
        {
            CacheDimensionForm.Scalar, CacheDimensionForm.Set, CacheDimensionForm.Scalar,
            CacheDimensionForm.Interval, CacheDimensionForm.Scalar
        });
    }

    /// <summary>Empty and omitted callbacks leave inherited schemas available to the collection.</summary>
    [Fact]
    public void Dimensions_EmptyCallbacks_PreserveInheritedSchema()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching(cache => cache
            .Defaults(collection => collection.Dimensions(d => d.Scalar<string>("region")))
            .WithCollection<string>("reports", collection => collection.Dimensions(null).Dimensions(_ => { })));

        // Act
        var sut = builder.BuildCatalog().Collections["reports"].Options.Dimensions;

        // Assert
        sut.Single().Name.ShouldBe("REGION");
        sut.Single().Kind.ShouldBe(CacheDimensionKind.String);
    }

    /// <summary>Names remain unique after normalization within a callback, across callbacks, and alongside individual declarations.</summary>
    /// <param name="declaration">Zero selects one callback, one selects repeated callbacks, and two selects an individual declaration first.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Dimensions_DuplicateNormalizedName_RejectsImmediately(int declaration)
    {
        // Arrange
        var sut = new CacheCollectionBuilder();
        if (declaration == 1) { sut.Dimensions(d => d.Scalar<string>("region")); }

        if (declaration == 2) { sut.WithScalarDimension<string>("region"); }

        // Act and assert
        if (declaration == 0)
        {
            Should.Throw<ArgumentException>(() => sut.Dimensions(d => d
                .Scalar<string>("region").Set<string>("REGION")));
        }
        else
        {
            Should.Throw<ArgumentException>(() => sut.Dimensions(d => d.Set<string>("REGION")));
        }
    }

    /// <summary>Nullable CLR kinds keep null admission disabled unless the declaration opts in explicitly.</summary>
    [Fact]
    public void Dimensions_NullableTypes_RequireExplicitNullPermission()
    {
        // Arrange
        var builder = new ServiceCollection().AddApplicationCaching(cache => cache.WithCollection<string>("reports", collection => collection
            .Dimensions(d => d.Scalar<int?>("priority").Set<Guid?>("owners", allowNull: true))));

        // Act
        var sut = builder.BuildCatalog().Collections["reports"].Options.Dimensions;

        // Assert
        sut[0].Kind.ShouldBe(CacheDimensionKind.Integer);
        sut[0].AllowNull.ShouldBeFalse();
        sut[1].Kind.ShouldBe(CacheDimensionKind.Identifier);
        sut[1].AllowNull.ShouldBeTrue();
    }

    /// <summary>Unsupported types, interval kinds, and oversized schema lists retain the existing validation boundaries.</summary>
    [Fact]
    public void Dimensions_InvalidDeclarations_RejectsUnsupportedTypesAndBounds()
    {
        // Act and assert
        Should.Throw<ArgumentException>(() => new CacheCollectionBuilder().Dimensions(d => d.Scalar<decimal>("price")));
        Should.Throw<ArgumentException>(() => new CacheCollectionBuilder().Dimensions(d => d.Set<object>("tags")));
        var invalidInterval = new ServiceCollection().AddApplicationCaching(cache => cache.WithCollection<string>("reports", collection => collection
            .Dimensions(d => d.Interval<string>("coverage"))));
        Should.Throw<ArgumentException>(() => invalidInterval.BuildCatalog());
        var oversized = new ServiceCollection().AddApplicationCaching(cache => cache.WithCollection<string>("reports", collection => collection
            .Dimensions(d =>
            {
                for (var index = 0; index < 17; index++) { d.Scalar<int>("d" + index); }
            })));
        Should.Throw<ArgumentException>(() => oversized.BuildCatalog());
    }

    /// <summary>Configuration replaces the complete declared schema list instead of appending to grouped declarations.</summary>
    [Fact]
    public void Dimensions_ConfigurationOverride_ReplacesTheCompleteSchemaList()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Caching:Application:Collections:reports:Dimensions:0:Name"] = "CONFIGURED",
            ["Caching:Application:Collections:reports:Dimensions:0:Kind"] = "Boolean"
        }).Build();
        var builder = new ServiceCollection().AddApplicationCaching(configuration, cache => cache.WithCollection<string>("reports", collection => collection
            .Dimensions(d => d.Scalar<string>("region").Set<string>("tags"))));

        // Act
        var sut = builder.BuildCatalog().Collections["reports"].Options.Dimensions;

        // Assert
        sut.Single().Name.ShouldBe("CONFIGURED");
        sut.Single().Kind.ShouldBe(CacheDimensionKind.Boolean);
        sut.Single().Form.ShouldBe(CacheDimensionForm.Scalar);
    }
}