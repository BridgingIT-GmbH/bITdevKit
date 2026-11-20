namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class BlobDimensionsQueryTests
{
    private static readonly BlobDimensionCapabilities Sql = new() { SupportsDimensions = true, SupportsQueries = true, SupportsDecimalComparisons = true };
    private static readonly BlobDimensionCapabilities Sqlite = new() { SupportsDimensions = true, SupportsQueries = true };

    [Fact]
    public void Validate_UnsupportedOperators_RejectsEntireExpression()
    {
        var equal = Leaf("amount", BlobDimensionOperator.Equal, 1.0m);
        var range = Leaf("amount", BlobDimensionOperator.GreaterThan, 1.0m);
        BlobDimensionFilterValidator.Validate(equal, Sqlite).IsSuccess.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(range, Sqlite).IsFailure.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(BlobDimensionFilter.Any(equal, range), Sqlite).IsFailure.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(equal, BlobDimensionCapabilities.Unsupported).IsFailure.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(range, Sql).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_GroupBounds_RejectsOversizedAndEmptyTrees()
    {
        var leaf = Leaf("year", BlobDimensionOperator.Equal, 2026);
        var tree = leaf;
        for (var index = 0; index < 4; index++) tree = BlobDimensionFilter.All(tree);
        BlobDimensionFilterValidator.Validate(tree, Sql).IsSuccess.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(BlobDimensionFilter.All(tree), Sql).IsFailure.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(BlobDimensionFilter.All(), Sql).IsFailure.ShouldBeTrue();
        BlobDimensionFilterValidator.Validate(BlobDimensionFilter.All(Enumerable.Repeat(leaf, 17).ToArray()), Sql).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CanonicalIdentity_EqualDecimalsAndNames_RetainsExactStringDifferences()
    {
        Leaf("Amount", BlobDimensionOperator.Equal, 1.0m).ToCanonicalString().ShouldBe(Leaf("AMOUNT", BlobDimensionOperator.Equal, 1.000000m).ToCanonicalString());
        Leaf("country", BlobDimensionOperator.Equal, "EU").ToCanonicalString().ShouldNotBe(Leaf("country", BlobDimensionOperator.Equal, "EU ").ToCanonicalString());
        Leaf("country", BlobDimensionOperator.Equal, "EU").ToCanonicalString().ShouldNotBe(Leaf("country", BlobDimensionOperator.Equal, "eu").ToCanonicalString());
    }

    [Fact]
    public void PositiveAnchor_OrBranches_RequireIndependentConsent()
    {
        var positive = Leaf("year", BlobDimensionOperator.Equal, 2026);
        var negative = Leaf("year", BlobDimensionOperator.Missing);
        BlobDimensionFilter.All(positive, negative).HasPositiveAnchor.ShouldBeTrue();
        BlobDimensionFilter.Any(positive, negative).HasPositiveAnchor.ShouldBeFalse();
        BlobDimensionPredicate.Create("year", BlobDimensionOperator.Equal, null).IsFailure.ShouldBeTrue();
        BlobDimensionPredicate.Create("year", BlobDimensionOperator.IsNull).IsSuccess.ShouldBeTrue();
        BlobDimensionPredicate.Create("country", BlobDimensionOperator.GreaterThan, "EU").IsFailure.ShouldBeTrue();
    }

    private static BlobDimensionFilter Leaf(string name, BlobDimensionOperator operation, object operand = null) =>
        BlobDimensionFilter.Where(BlobDimensionPredicate.Create(name, operation, operand).Value);
}
