namespace BridgingIT.DevKit.Application.UnitTests.Storage;

using Application.Storage;

public class DocumentDimensionsQueryTests
{
    private static readonly DocumentDimensionCapabilities Sql = new() { SupportsDimensions = true, SupportsQueries = true, SupportsDecimalComparisons = true };
    private static readonly DocumentDimensionCapabilities Sqlite = new() { SupportsDimensions = true, SupportsQueries = true };

    [Fact]
    public void Validate_UnsupportedOperators_RejectsEntireExpression()
    {
        var equal = Leaf("amount", DocumentDimensionOperator.Equal, 1.0m);
        var range = Leaf("amount", DocumentDimensionOperator.GreaterThan, 1.0m);
        DocumentDimensionFilterValidator.Validate(equal, Sqlite).IsSuccess.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(range, Sqlite).IsFailure.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(DocumentDimensionFilter.Any(equal, range), Sqlite).IsFailure.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(equal, DocumentDimensionCapabilities.Unsupported).IsFailure.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(range, Sql).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Validate_GroupBounds_RejectsOversizedAndEmptyTrees()
    {
        var leaf = Leaf("year", DocumentDimensionOperator.Equal, 2026);
        var tree = leaf;
        for (var index = 0; index < 4; index++) tree = DocumentDimensionFilter.All(tree);
        DocumentDimensionFilterValidator.Validate(tree, Sql).IsSuccess.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(DocumentDimensionFilter.All(tree), Sql).IsFailure.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(DocumentDimensionFilter.All(), Sql).IsFailure.ShouldBeTrue();
        DocumentDimensionFilterValidator.Validate(DocumentDimensionFilter.All(Enumerable.Repeat(leaf, 17).ToArray()), Sql).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void CanonicalIdentity_EqualDecimalsAndNames_RetainsExactStringDifferences()
    {
        Leaf("Amount", DocumentDimensionOperator.Equal, 1.0m).ToCanonicalString().ShouldBe(Leaf("AMOUNT", DocumentDimensionOperator.Equal, 1.000000m).ToCanonicalString());
        Leaf("country", DocumentDimensionOperator.Equal, "EU").ToCanonicalString().ShouldNotBe(Leaf("country", DocumentDimensionOperator.Equal, "EU ").ToCanonicalString());
        Leaf("country", DocumentDimensionOperator.Equal, "EU").ToCanonicalString().ShouldNotBe(Leaf("country", DocumentDimensionOperator.Equal, "eu").ToCanonicalString());
    }

    [Fact]
    public void PositiveAnchor_OrBranches_RequireIndependentConsent()
    {
        var positive = Leaf("year", DocumentDimensionOperator.Equal, 2026);
        var negative = Leaf("year", DocumentDimensionOperator.Missing);
        DocumentDimensionFilter.All(positive, negative).HasPositiveAnchor.ShouldBeTrue();
        DocumentDimensionFilter.Any(positive, negative).HasPositiveAnchor.ShouldBeFalse();
        DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.Equal, null).IsFailure.ShouldBeTrue();
        DocumentDimensionPredicate.Create("year", DocumentDimensionOperator.IsNull).IsSuccess.ShouldBeTrue();
        DocumentDimensionPredicate.Create("country", DocumentDimensionOperator.GreaterThan, "EU").IsFailure.ShouldBeTrue();
    }

    private static DocumentDimensionFilter Leaf(string name, DocumentDimensionOperator operation, object operand = null) =>
        DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create(name, operation, operand).Value);
}
