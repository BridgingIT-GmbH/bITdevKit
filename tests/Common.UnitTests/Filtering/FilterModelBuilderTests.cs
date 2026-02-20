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

    [Fact]
    public void AddInclude_FollowedByOtherBuilderMethods_ShouldWorkWithoutDone()
    {
        // Arrange & Act - Test backward compatibility without requiring .Done()
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.Locations)
            .AddOrdering(p => p.LastName, OrderDirection.Descending)
            .AddFilter(p => p.Age, FilterOperator.GreaterThan, 25)
            .SetPaging(1, 10)
            .Build();

        // Assert
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Orderings.ShouldHaveSingleItem();
        filterModel.Orderings[0].Field.ShouldBe("LastName");
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Field.ShouldBe("Age");
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(10);
    }

    [Fact]
    public void AddInclude_WithDone_ShouldStillWork()
    {
        // Arrange & Act - Test that .Done() still works for those who prefer explicit chaining
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.Locations).Done()
            .AddOrdering(p => p.LastName, OrderDirection.Descending)
            .Build();

        // Assert
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Orderings.ShouldHaveSingleItem();
    }

    [Fact]
    public void ThenInclude_SingleLevel_ShouldAddNestedInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy);
        var filterModel = builder.Build();

        // Assert
        // The AddInclude method adds the base path, ThenInclude adds the nested path
        // So we expect only the nested path "PrimaryLocation.Dummy" (not both separately)
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
    }

    [Fact]
    public void ThenInclude_MultipleLevel_ShouldAddDeeplyNestedIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy)
            .ThenInclude(d => d.Details)
            .ThenInclude(dt => dt.Metadata);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(4);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details.Metadata");
    }

    [Fact]
    public void ThenInclude_TwoLevels_ShouldBuildCorrectPath()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy)
            .ThenInclude(d => d.Details);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(3);
        filterModel.Includes[0].ShouldBe("PrimaryLocation");
        filterModel.Includes[1].ShouldBe("PrimaryLocation.Dummy");
        filterModel.Includes[2].ShouldBe("PrimaryLocation.Dummy.Details");
    }

    [Fact]
    public void ThenInclude_FollowedByOtherBuilderMethods_ShouldWorkWithoutDone()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
            .AddOrdering(p => p.LastName, OrderDirection.Descending)
            .AddFilter(p => p.Age, FilterOperator.GreaterThan, 25)
            .SetPaging(1, 10)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(3);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details");
        filterModel.Orderings.ShouldHaveSingleItem();
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(10);
    }

    [Fact]
    public void ThenInclude_WithDone_ShouldReturnToMainBuilder()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy)
                .Done()
            .AddOrdering(p => p.LastName, OrderDirection.Ascending)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Orderings.ShouldHaveSingleItem();
    }

    [Fact]
    public void ThenInclude_MultipleIncludeChains_ShouldAddAllPaths()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
            .AddInclude(p => p.Email)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(4);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details");
        filterModel.Includes.ShouldContain("Email");
    }

    [Fact]
    public void ThenInclude_WithConditionFalse_ShouldNotAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy, condition: false);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldHaveSingleItem();
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldNotContain("PrimaryLocation.Dummy");
    }

    [Fact]
    public void ThenInclude_WithConditionTrue_ShouldAddInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy, condition: true);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
    }

    [Fact]
    public void ThenInclude_AfterInactiveInclude_ShouldNotAddAnyIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation, condition: false)
            .ThenInclude(l => l.Dummy)
            .ThenInclude(d => d.Details);
        var filterModel = builder.Build();

        // Assert
        // When condition is false, Includes is initialized as an empty list
        filterModel.Includes.ShouldBeEmpty();
    }

    [Fact]
    public void ThenInclude_ConditionalChaining_ShouldOnlyAddActiveIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.PrimaryLocation)
            .ThenInclude(l => l.Dummy, condition: true)
            .ThenInclude(d => d.Details, condition: false)
            .ThenInclude(dt => dt.Metadata); // This should not be added because parent was false
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Includes.ShouldNotContain("PrimaryLocation.Dummy.Details");
        filterModel.Includes.ShouldNotContain("PrimaryLocation.Dummy.Details.Metadata");
    }

    [Fact]
    public void ThenInclude_ComplexScenario_WithMultipleIncludesFilteringAndOrdering()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddFilter(p => p.Age, FilterOperator.GreaterThan, 18)
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
                .ThenInclude(dt => dt.Metadata)
            .AddInclude(p => p.Email)
            .AddOrdering(p => p.FirstName, OrderDirection.Ascending)
            .AddOrdering(p => p.LastName, OrderDirection.Descending)
            .SetPaging(2, 25)
            .Build();

        // Assert
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Field.ShouldBe("Age");

        filterModel.Includes.Count.ShouldBe(5);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy.Details.Metadata");
        filterModel.Includes.ShouldContain("Email");

        filterModel.Orderings.Count.ShouldBe(2);
        filterModel.Orderings[0].Field.ShouldBe("FirstName");
        filterModel.Orderings[1].Field.ShouldBe("LastName");

        filterModel.Page.ShouldBe(2);
        filterModel.PageSize.ShouldBe(25);
    }

    [Fact]
    public void ThenInclude_InvalidNavigationPropertyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => "InvalidProperty"));
    }

    [Fact]
    public void ThenInclude_CanChainAfterAddingFilters_ShouldMaintainFluentAPI()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy)
                .AddFilter(p => p.Age, FilterOperator.Equal, 30)
                .AddOrdering(p => p.FirstName, OrderDirection.Ascending)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Orderings.ShouldHaveSingleItem();
    }

    #region Collection ThenInclude Tests

    [Fact]
    public void ThenInclude_Collection_SingleLevel_ShouldAddNestedInclude()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations)  // IReadOnlyList<LocationStub>
            .ThenInclude(l => l.Dummy);        // From LocationStub element
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
    }

    [Fact]
    public void ThenInclude_Collection_MultipleLevel_ShouldAddDeeplyNestedIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations)      // IReadOnlyList<LocationStub>
            .ThenInclude(l => l.Dummy)             // From LocationStub element
            .ThenInclude(d => d.Details)           // From Dummy
            .ThenInclude(dt => dt.Metadata);       // From DummyDetails
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(4);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details.Metadata");
    }

    [Fact]
    public void ThenInclude_Collection_WithDone_ShouldAllowExplicitChaining()
    {
        // Arrange & Act - Extend the existing test with sub-objects in the collection
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.Locations)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
                .Done()  // Return to main builder
            .AddOrdering(p => p.LastName, OrderDirection.Descending)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(3);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        filterModel.Orderings.ShouldHaveSingleItem();
        filterModel.Orderings[0].Field.ShouldBe("LastName");
    }

    [Fact]
    public void ThenInclude_Collection_WithoutDone_ShouldMaintainFluentAPI()
    {
        // Arrange & Act - Test fluent API without requiring .Done()
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.Locations)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
                .AddOrdering(p => p.FirstName, OrderDirection.Ascending)  // No .Done() needed
                .AddFilter(p => p.Age, FilterOperator.GreaterThan, 18)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(3);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        filterModel.Orderings.ShouldHaveSingleItem();
        filterModel.Filters.ShouldHaveSingleItem();
    }

    [Fact]
    public void ThenInclude_Collection_ConditionalChaining_ShouldOnlyAddActiveIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations)
            .ThenInclude(l => l.Dummy, condition: true)
            .ThenInclude(d => d.Details, condition: false)  // This should not be added
            .ThenInclude(dt => dt.Metadata);  // This should also not be added (parent was false)
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(2);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldNotContain("Locations.Dummy.Details");
        filterModel.Includes.ShouldNotContain("Locations.Dummy.Details.Metadata");
    }

    [Fact]
    public void ThenInclude_Collection_AfterInactiveInclude_ShouldNotAddAnyIncludes()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations, condition: false)
            .ThenInclude(l => l.Dummy)
            .ThenInclude(d => d.Details);
        var filterModel = builder.Build();

        // Assert
        filterModel.Includes.ShouldBeEmpty();
    }

    [Fact]
    public void ThenInclude_Collection_MultipleIndependentChains_ShouldAddAllPaths()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act
        builder.AddInclude(p => p.Locations)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Dummy);
        var filterModel = builder.Build();

        // Assert
        // Note: Each AddInclude adds the base path, each ThenInclude adds one nested path
        filterModel.Includes.Count.ShouldBe(5);
        // First chain (collection)
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        // Second chain (reference)
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Dummy");
    }

    [Fact]
    public void ThenInclude_Collection_CombinedWithFiltersAndOrdering_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddInclude(p => p.Locations)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
                .AddFilter(p => p.Age, FilterOperator.GreaterThanOrEqual, 21)
                .AddOrdering(p => p.LastName, OrderDirection.Ascending)
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.City)
                .AddOrdering(p => p.FirstName, OrderDirection.Descending)
            .SetPaging(1, 20)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(5);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.City");

        filterModel.Filters.ShouldHaveSingleItem();
        filterModel.Filters[0].Field.ShouldBe("Age");

        filterModel.Orderings.Count.ShouldBe(2);
        filterModel.Orderings[0].Field.ShouldBe("LastName");
        filterModel.Orderings[1].Field.ShouldBe("FirstName");

        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(20);
    }

    [Fact]
    public void ThenInclude_Collection_InvalidNavigationPropertyPath_ShouldThrowArgumentException()
    {
        // Arrange
        var builder = FilterModelBuilder.For<PersonStub>();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.AddInclude(p => p.Locations)
                .ThenInclude(l => "InvalidProperty"));
    }

    [Fact]
    public void ThenInclude_Collection_RealWorldScenario_ShouldBuildComplexQuery()
    {
        // Arrange & Act - Simulate a real-world scenario with nested collection navigation
        var filterModel = FilterModelBuilder.For<PersonStub>()
            .AddFilter(p => p.Age, FilterOperator.GreaterThanOrEqual, 18)
            .AddFilter(p => p.Nationality, FilterOperator.Equal, "USA")
            .AddInclude(p => p.Locations)
                .ThenInclude(l => l.Dummy)
                .ThenInclude(d => d.Details)
                .ThenInclude(dt => dt.Metadata)
            .AddInclude(p => p.PrimaryLocation)
                .ThenInclude(l => l.Country)
            .AddOrdering(p => p.LastName, OrderDirection.Ascending)
            .AddOrdering(p => p.FirstName, OrderDirection.Ascending)
            .SetPaging(1, 25)
            .Build();

        // Assert
        filterModel.Includes.Count.ShouldBe(6);
        filterModel.Includes.ShouldContain("Locations");
        filterModel.Includes.ShouldContain("Locations.Dummy");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details");
        filterModel.Includes.ShouldContain("Locations.Dummy.Details.Metadata");
        filterModel.Includes.ShouldContain("PrimaryLocation");
        filterModel.Includes.ShouldContain("PrimaryLocation.Country");

        filterModel.Filters.Count.ShouldBe(2);
        filterModel.Orderings.Count.ShouldBe(2);
        filterModel.Page.ShouldBe(1);
        filterModel.PageSize.ShouldBe(25);
    }

    #endregion
}