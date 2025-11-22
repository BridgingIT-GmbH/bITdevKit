// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Filtering;

using Bogus;
using Xunit;
using System;
using Shouldly;

public class FilterModelBuilderTests
{
    private readonly Faker<PersonStub> faker;

    public FilterModelBuilderTests() => this.faker = new Faker<PersonStub>()
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.Age, f => f.Random.Int(18, 99))
            .RuleFor(p => p.Email, f => EmailAddressStub.Create(f.Internet.Email()));

    [Fact]
    public void SetPaging_ValidPageAndPageSize_ShouldSetPagingCorrectly()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.SetPaging(1, 10);
        var filterModel = builder.Build();

        // Assert
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(10);
    }

    [Fact]
    public void SetPaging_OnlyPageSize_ShouldSetFirstPage()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.SetPaging(20);
        var filterModel = builder.Build();

        // Assert
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(20);
    }

    [Fact]
    public void SetPaging_StandardPageSize_ShouldSetCorrectPaging()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.SetPaging(1, PageSize.Large);
        var filterModel = builder.Build();

        // Assert
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe((int)PageSize.Large);
    }

    [Fact]
    public void AddFilter_ValidParameters_ShouldAddFilter()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(p => p.FirstName, FilterOperator.Equal, "John");
        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Field.ShouldBe("FirstName");
        filterModel.Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        filterModel.Filters[0].Value.ShouldBe("John");
    }

    [Fact]
    public void AddFilter_InvalidPropertySelector_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddFilter(p => "InvalidProperty", FilterOperator.Equal, "John"));
    }

    [Fact]
    public void AddAnyFilter_ValidParameters_ShouldAddFilter()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(
            p => p.Locations,
            FilterOperator.Any,
            b => b.AddFilter(loc => loc.City, FilterOperator.Equal, "Berlin"));

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters.ShouldNotBeNull();
        filterModel.Filters[0].Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters[0].Field.ShouldBe("City");
        filterModel.Filters[0].Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        filterModel.Filters[0].Filters[0].Value.ShouldBe("Berlin");
    }

    [Fact]
    public void AddAllFilter_ValidParameters_ShouldAddFilter()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(
            p => p.Locations,
            FilterOperator.All,
            b => b.AddFilter(loc => loc.City, FilterOperator.StartsWith, "B"));

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters.ShouldNotBeNull();
        filterModel.Filters[0].Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters[0].Field.ShouldBe("City");
        filterModel.Filters[0].Filters[0].Operator.ShouldBe(FilterOperator.StartsWith);
        filterModel.Filters[0].Filters[0].Value.ShouldBe("B");
    }

    [Fact]
    public void AddAllFilter_ValidParameters_ShouldAddFilter2()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(
            p => p.Locations, // collection property
            FilterOperator.All,
            lb => lb.AddFilter(l => l.Dummy, FilterOperator.Equal, // nested object property
                db => db.AddFilter(d => d.Text, FilterOperator.Equal, "ABC")));

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters.ShouldNotBeNull();
        filterModel.Filters[0].Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters[0].Field.ShouldBe("Dummy");
        filterModel.Filters[0].Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        filterModel.Filters[0].Filters[0].Filters[0].Field.ShouldBe("Text");
        filterModel.Filters[0].Filters[0].Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        filterModel.Filters[0].Filters[0].Filters[0].Value.ShouldBe("ABC");
    }

    [Fact]
    public void AddNoneFilter_ValidParameters_ShouldAddFilter()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(
            p => p.Locations,
            FilterOperator.None,
            b => b.AddFilter(loc => loc.City, FilterOperator.Equal, "Berlin"));

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters.ShouldNotBeNull();
        filterModel.Filters[0].Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Filters[0].Field.ShouldBe("City");
        filterModel.Filters[0].Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        filterModel.Filters[0].Filters[0].Value.ShouldBe("Berlin");
    }

    [Fact]
    public void AddOrdering_ValidParameters_ShouldAddOrdering()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddOrdering(p => p.LastName, OrderDirection.Ascending);
        var filterModel = builder.Build();

        // Assert
        filterModel.Orderings.ShouldHaveSingleItem();
        filterModel.Orderings[0].Field.ShouldBe("LastName");
        filterModel.Orderings[0].Direction.ShouldBe(OrderDirection.Ascending);
    }

    [Fact]
    public void AddInclude_ValidParameters_ShouldAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldHaveSingleItem();
        filterModel.Includes[0].ShouldBe("Locations");
    }

    [Fact]
    public void AddInclude_WithStringPath_ValidParameter_ShouldAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude("Locations");
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldHaveSingleItem();
        filterModel.Includes[0].ShouldBe("Locations");
    }

    [Fact]
    public void AddInclude_WithStringPath_NestedPath_ShouldAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude("Locations.City");
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldHaveSingleItem();
        filterModel.Includes[0].ShouldBe("Locations.City");
    }

    [Fact]
    public void AddInclude_WithStringPath_MultipleIncludes_ShouldAddAllIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude("Locations")
               .AddInclude("Email");
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Email");
    }

    [Fact]
    public void AddInclude_WithStringPath_NullPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddInclude((string)null));
    }

    [Fact]
    public void AddInclude_WithStringPath_EmptyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddInclude(string.Empty));
    }

    [Fact]
    public void AddInclude_WithStringPath_WhitespacePath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.AddInclude("   "));
    }

    [Fact]
    public void AddInclude_WithStringPath_WithConditionTrue_ShouldAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude("Locations", condition: true);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldHaveSingleItem();
        filterModel.Includes[0].ShouldBe("Locations");
    }

    [Fact]
    public void AddInclude_WithStringPath_WithConditionFalse_ShouldNotAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude("Locations", condition: false);
        var filterModel = builder.Build();

        // Assert
        (filterModel.Includes == null || filterModel.Includes.Count == 0).ShouldBeTrue();
    }

    [Fact]
    public void Build_ExtensiveFilterModel_WithFluentSyntax_AndStringIncludes_ShouldBuildCorrectly()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .SetPaging(2, PageSize.Large) // Fluent paging setup
            .AddFilter(p => p.Age, FilterOperator.GreaterThan, 25) // Age > 25
            .AddFilter(p => p.FirstName, FilterOperator.Contains, "A") // FirstName contains "A"
            .AddFilter(p => p.Locations,
                FilterOperator.Any,
                b => b
                    .AddFilter(loc => loc.City, FilterOperator.Equal, "Berlin")
                    .AddFilter(loc => loc.PostalCode, FilterOperator.StartsWith, "100")) // Any location with City = Berlin or PostalCode starts with "100"
            .AddCustomFilter(FilterCustomType.FullTextSearch)
            .AddParameter("searchTerm", "John")
            .AddParameter("fields", ["FirstName", "LastName"]).Done()
            .AddOrdering(p => p.LastName, OrderDirection.Descending) // Order by LastName Descending
            .AddOrdering(p => p.FirstName, OrderDirection.Ascending) // Then order by FirstName Ascending
            .AddInclude("Locations") // String-based include
            .AddInclude("Email") // String-based include
            .Build();

        // Assert
        filterModel.Page.ShouldBe(2);
        filterModel.PageSize.ShouldBe(50);

        filterModel.Filters.Count.ShouldBe(4); // We expect 4 filters (Age, FirstName, Locations, Custom)

        // Age filter
        filterModel.Filters[0].Field.ShouldBe("Age");
        filterModel.Filters[0].Operator.ShouldBe(FilterOperator.GreaterThan);
        filterModel.Filters[0].Value.ShouldBe(25);

        // FirstName filter
        filterModel.Filters[1].Field.ShouldBe("FirstName");
        filterModel.Filters[1].Operator.ShouldBe(FilterOperator.Contains);
        filterModel.Filters[1].Value.ShouldBe("A");

        // Nested Locations filter
        var locationsFilter = filterModel.Filters[2];
        locationsFilter.Filters.ShouldNotBeNull();
        locationsFilter.Filters.Count.ShouldBe(2);
        locationsFilter.Filters[0].Field.ShouldBe("City");
        locationsFilter.Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        locationsFilter.Filters[0].Value.ShouldBe("Berlin");
        locationsFilter.Filters[1].Field.ShouldBe("PostalCode");
        locationsFilter.Filters[1].Operator.ShouldBe(FilterOperator.StartsWith);
        locationsFilter.Filters[1].Value.ShouldBe("100");

        // Custom full-text search filter
        filterModel.Filters[3].CustomType.ShouldBe(FilterCustomType.FullTextSearch);
        filterModel.Filters[3].CustomParameters["searchTerm"].ShouldBe("John");

        // Orderings
        filterModel.Orderings.Count.ShouldBe(2);
        filterModel.Orderings[0].Field.ShouldBe("LastName");
        filterModel.Orderings[0].Direction.ShouldBe(OrderDirection.Descending);
        filterModel.Orderings[1].Field.ShouldBe("FirstName");
        filterModel.Orderings[1].Direction.ShouldBe(OrderDirection.Ascending);

        // Includes (string-based)
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Email");
    }

    [Fact]
    public void AddCustomFilter_ValidParameters_ShouldAddCustomFilter()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        var customFilterBuilder = builder.AddCustomFilter(FilterCustomType.FullTextSearch);
        customFilterBuilder.AddParameter("searchTerm", "John");

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].CustomType.ShouldBe(FilterCustomType.FullTextSearch);
        filterModel.Filters[0].CustomParameters.ShouldContainKey("searchTerm");
        filterModel.Filters[0].CustomParameters["searchTerm"].ShouldBe("John");
    }

    [Fact]
    public void Build_ShouldResetThreadLocalFilterModel()
    {
        // Arrange
        var builder1 = FilterModelBuilder.For<PersonStub>();
        builder1.SetPaging(1, 10);
        var filterModel1 = builder1.Build();

        // Act
        var builder2 = FilterModelBuilder.For<PersonStub>();
        var filterModel2 = builder2.Build();

        // Assert
        filterModel1.ShouldNotBe(filterModel2);
    }

    [Fact]
    public void AddFilter_DuplicateFilter_ShouldAddFilterTwice()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddFilter(p => p.FirstName, FilterOperator.Equal, "John");
        builder.AddFilter(p => p.FirstName, FilterOperator.Equal, "John"); // Duplicate

        var filterModel = builder.Build();

        // Assert
        filterModel.Filters.Count.ShouldBe(2);
    }

    [Fact]
    public void SetPaging_NegativePageSize_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.SetPaging(1, -10));
    }

    [Fact]
    public void SetPaging_NegativePage_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => builder.SetPaging(-1, 10));
    }

    [Fact]
    public void Build_ExtensiveFilterModel_WithFluentSyntax_ShouldBuildCorrectly()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .SetPaging(2, PageSize.Large) // Fluent paging setup
            .AddFilter(p => p.Age, FilterOperator.GreaterThan, 25) // Age > 25
            .AddFilter(p => p.FirstName, FilterOperator.Contains, "A") // FirstName contains "A"
            .AddFilter(p => p.Locations,
                FilterOperator.Any,
                b => b
                    .AddFilter(loc => loc.City, FilterOperator.Equal, "Berlin")
                    .AddFilter(loc => loc.PostalCode, FilterOperator.StartsWith, "100")) // Any location with City = New York or ZipCode starts with "100"
            .AddCustomFilter(FilterCustomType.FullTextSearch)
            .AddParameter("searchTerm", "John")
            .AddParameter("fields", ["FirstName", "LastName"]).Done()
            .AddOrdering(p => p.LastName, OrderDirection.Descending) // Order by LastName Descending
            .AddOrdering(p => p.FirstName, OrderDirection.Ascending) // Then order by FirstName Ascending
            .AddInclude(p => p.Locations)
            .Build();

        // Assert
        filterModel.Page.ShouldBe(2);
        filterModel.PageSize.ShouldBe(50);

        filterModel.Filters.Count.ShouldBe(4); // We expect 4 filters (Age, FirstName, Locations, Custom)

        // Age filter
        filterModel.Filters[0].Field.ShouldBe("Age");
        filterModel.Filters[0].Operator.ShouldBe(FilterOperator.GreaterThan);
        filterModel.Filters[0].Value.ShouldBe(25);

        // FirstName filter
        filterModel.Filters[1].Field.ShouldBe("FirstName");
        filterModel.Filters[1].Operator.ShouldBe(FilterOperator.Contains);
        filterModel.Filters[1].Value.ShouldBe("A");

        // Nested Locations filter
        var locationsFilter = filterModel.Filters[2];
        locationsFilter.Filters.ShouldNotBeNull();
        locationsFilter.Filters.Count.ShouldBe(2);
        locationsFilter.Filters[0].Field.ShouldBe("City");
        locationsFilter.Filters[0].Operator.ShouldBe(FilterOperator.Equal);
        locationsFilter.Filters[0].Value.ShouldBe("Berlin");
        locationsFilter.Filters[1].Field.ShouldBe("PostalCode");
        locationsFilter.Filters[1].Operator.ShouldBe(FilterOperator.StartsWith);
        locationsFilter.Filters[1].Value.ShouldBe("100");

        // Custom full-text search filter
        filterModel.Filters[3].CustomType.ShouldBe(FilterCustomType.FullTextSearch);
        filterModel.Filters[3].CustomParameters["searchTerm"].ShouldBe("John");

        // Orderings
        filterModel.Orderings.Count.ShouldBe(2);
        filterModel.Orderings[0].Field.ShouldBe("LastName");
        filterModel.Orderings[0].Direction.ShouldBe(OrderDirection.Descending);
        filterModel.Orderings[1].Field.ShouldBe("FirstName");
        filterModel.Orderings[1].Direction.ShouldBe(OrderDirection.Ascending);

        // Includes
        filterModel.Includes.ShouldContain("Locations");
    }
}