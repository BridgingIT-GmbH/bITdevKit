namespace BridgingIT.DevKit.Common.UnitTests.Filtering;

using System.Text.Json;

public class FilterModelQueryExtensionsTests
{
    [Fact]
    public void Search_StringSeek_UsesTheSameOrderAsPaging()
    {
        var sut = Enumerable.Range(1, 35).Select(index => new Row(index, $"job{index:000}", index)).AsQueryable();
        var filter = new FilterModel
        {
            Page = 1, PageSize = 10,
            Filters = [new("Name", FilterOperator.GreaterThan, "job010")],
            Orderings = [new() { Field = "Name" }]
        };

        sut.ApplyFilter(filter).ApplyOrder(filter).ApplyPage(filter).Select(row => row.Id).ShouldBe(Enumerable.Range(11, 10));
    }

    [Fact]
    public void Search_FiltersBeforePagingAndOrdersByMultipleFields()
    {
        var source = new[] { new Row(1, "Beta", 2), new Row(2, "Alpha", 3), new Row(3, "Alpha", 4), new Row(4, null, 0) }.AsQueryable();
        var filter = new FilterModel
        {
            Page = 2, PageSize = 1,
            Filters = [new("Score", FilterOperator.GreaterThan, JsonSerializer.SerializeToElement(1))],
            Orderings = [new() { Field = "Name" }, new() { Field = "Score", Direction = OrderDirection.Descending }]
        };

        var filtered = source.ApplyFilter(filter);
        filtered.Count().ShouldBe(3);
        filtered.ApplyOrder(filter).ApplyPage(filter).Single().Id.ShouldBe(2);
        filter.Page.ShouldBe(2);
    }

    [Fact]
    public void Search_UsesRepositoryAndOrGrouping()
    {
        var source = Enumerable.Range(1, 5).Select(i => new Row(i, "item", i)).AsQueryable();
        var filter = new FilterModel
        {
            Filters = [new("Id", FilterOperator.Equal, 1) { Logic = FilterLogicOperator.Or },
                new("Id", FilterOperator.Equal, 3), new("Score", FilterOperator.GreaterThan, 2)]
        };

        source.ApplyFilter(filter).Single().Id.ShouldBe(3);
    }

    [Fact]
    public void Search_StringOperatorsHandleNulls()
    {
        var source = new[] { new Row(1, null, null), new Row(2, "Alpha", 3) }.AsQueryable();
        source.ApplyFilter(new FilterModel { Filters = [new("Name", FilterOperator.Contains, "ph")] }).Single().Id.ShouldBe(2);
        source.ApplyFilter(new FilterModel { Filters = [new("Score", FilterOperator.IsNull, null)] }).Single().Id.ShouldBe(1);
    }

    [Theory]
    [InlineData("Missing", FilterOperator.Equal, "1")]
    [InlineData("Score", FilterOperator.Equal, "invalid")]
    [InlineData("Name.Length", FilterOperator.Equal, "1")]
    [InlineData("Name", FilterOperator.Any, "a")]
    public void Search_RejectsInvalidFieldsAndValues(string field, FilterOperator operation, string value)
    {
        Should.Throw<ArgumentException>(() => Array.Empty<Row>().AsQueryable()
            .ApplyFilter(new FilterModel { Filters = [new(field, operation, value)] }).ToArray());
    }

    [Fact]
    public void Search_ExplicitSorting_UsesIdentifierToBreakTies()
    {
        var source = new[] { new Row(2, "same", 0), new Row(1, "same", 0) }.AsQueryable();
        var filter = new FilterModel { Orderings = [new() { Field = "Name" }], PageSize = 1 };

        source.ApplyOrder(filter, row => row.Id).ApplyPage(filter).Single().Id.ShouldBe(1);
    }

    [Fact]
    public void Search_CapsPageSizeAndRejectsIncludes()
    {
        var source = Enumerable.Range(1, 1100).Select(i => new Row(i, "item", i)).AsQueryable();
        source.ApplyPage(new FilterModel { PageSize = 2000 }).Count().ShouldBe(1000);
        source.ApplyPage(null).Count().ShouldBe(50);
        Should.Throw<ArgumentException>(() => source.ApplyFilter(new FilterModel { Includes = ["Details"] }));
        Should.Throw<ArgumentException>(() => source.ApplyPage(new FilterModel { Page = int.MaxValue, PageSize = 1000 }));
    }

    private sealed record Row(int Id, string Name, int? Score);
}
