// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Filtering;

using Xunit;
using Shouldly;

public class FilterModelExtensionsTests
{
    [Fact]
    public void Merge_WithNull_ShouldReturnNull()
    {
        // Arrange
        FilterModel source = null;
        var other = new FilterModel();

        // Act
        var result = source.Merge(other);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Merge_WithNullParameter_ShouldReturnSource()
    {
        // Arrange
        var source = new FilterModel();

        // Act
        var result = source.Merge(null);

        // Assert
        result.ShouldBe(source);
    }

    [Fact]
    public void Merge_PagingProperties_ShouldOverrideWhenGreaterThanZero()
    {
        // Arrange
        var source = new FilterModel { Page = 1, PageSize = 10 };
        var other = new FilterModel { Page = 2, PageSize = 20 };

        // Act
        var result = source.Merge(other);

        // Assert
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(20);
    }

    [Fact]
    public void Merge_Orderings_ShouldReplaceExistingAndAddNew()
    {
        // Arrange
        var source = new FilterModel
        {
            Orderings =
            [
                new() { Field = "Name", Direction = OrderDirection.Ascending },
                new() { Field = "Age", Direction = OrderDirection.Descending }
            ]
        };

        var other = new FilterModel
        {
            Orderings =
            [
                new() { Field = "Name", Direction = OrderDirection.Descending },
                new() { Field = "Email", Direction = OrderDirection.Ascending }
            ]
        };

        // Act
        var result = source.Merge(other);

        // Assert
        result.Orderings.Count.ShouldBe(3);
        result.Orderings.ShouldContain(o => o.Field == "Name" && o.Direction == OrderDirection.Descending);
        result.Orderings.ShouldContain(o => o.Field == "Age" && o.Direction == OrderDirection.Descending);
        result.Orderings.ShouldContain(o => o.Field == "Email" && o.Direction == OrderDirection.Ascending);
    }

    [Fact]
    public void Merge_Filters_ShouldReplaceExistingAndAddNew()
    {
        // Arrange
        var source = new FilterModel
        {
            Filters =
            [
                new() { Field = "Name", Operator = FilterOperator.Equal, Value = "John" },
                new() { Field = "Age", Operator = FilterOperator.GreaterThan, Value = 18 }
            ]
        };

        var other = new FilterModel
        {
            Filters =
            [
                new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Jane" },
                new() { Field = "Email", Operator = FilterOperator.Contains, Value = "@" }
            ]
        };

        // Act
        var result = source.Merge(other);

        // Assert
        result.Filters.Count.ShouldBe(3);
        result.Filters.ShouldContain(f => f.Field == "Name" && f.Value.ToString() == "Jane");
        result.Filters.ShouldContain(f => f.Field == "Age" && f.Value.ToString() == "18");
        result.Filters.ShouldContain(f => f.Field == "Email" && f.Value.ToString() == "@");
    }

    [Fact]
    public void Merge_Includes_ShouldRemoveDuplicatesAndAddNew()
    {
        // Arrange
        var source = new FilterModel { Includes = ["User", "Address"] };
        var other = new FilterModel { Includes = ["Address", "Orders"] };

        // Act
        var result = source.Merge(other);

        // Assert
        result.Includes.Count.ShouldBe(3);
        result.Includes.ShouldContain("User");
        result.Includes.ShouldContain("Address");
        result.Includes.ShouldContain("Orders");
    }

    [Fact]
    public void Merge_Hierarchy_ShouldOverrideWhenNotEmpty()
    {
        // Arrange
        var source = new FilterModel
        {
            Hierarchy = "Parent",
            HierarchyMaxDepth = 3
        };

        var other = new FilterModel
        {
            Hierarchy = "Child",
            HierarchyMaxDepth = 5
        };

        // Act
        var result = source.Merge(other);

        // Assert
        result.Hierarchy.ShouldBe("Child");
        result.HierarchyMaxDepth.ShouldBe(5);
    }

    [Fact]
    public void Clear_ShouldResetToDefaultValues()
    {
        // Arrange
        var source = new FilterModel
        {
            Page = 2,
            PageSize = 20,
            NoTracking = false,
            Orderings = [new() { Field = "Name" }],
            Filters = [new() { Field = "Age" }],
            Includes = ["User"],
            Hierarchy = "Parent",
            HierarchyMaxDepth = 3
        };

        // Act
        var result = source.Clear();

        // Assert
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(10);
        result.NoTracking.ShouldBeTrue();
        result.Orderings.ShouldBeEmpty();
        result.Filters.ShouldBeEmpty();
        result.Includes.ShouldBeEmpty();
        result.Hierarchy.ShouldBeNull();
        result.HierarchyMaxDepth.ShouldBe(5);
    }

    [Fact]
    public void Clear_WithField_ShouldRemoveAllReferencesToField()
    {
        // Arrange
        var source = new FilterModel
        {
            Orderings = [new() { Field = "Name" }, new() { Field = "Age" }],
            Filters =
            [
                new() { Field = "Name", Operator = FilterOperator.Equal, Value = "John" },
                new()
                {
                    Field = "Locations",
                    Operator = FilterOperator.Any,
                    Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Home" }]
                }
            ],
            Includes = ["Name", "Age"],
            Hierarchy = "Name"
        };

        // Act
        var result = source.Clear("Name");

        // Assert
        result.Orderings.Count.ShouldBe(1);
        result.Orderings[0].Field.ShouldBe("Age");

        result.Filters.Count.ShouldBe(1);
        result.Filters[0].Field.ShouldBe("Locations");
        result.Filters[0].Filters.ShouldBeEmpty();

        result.Includes.Count.ShouldBe(1);
        result.Includes[0].ShouldBe("Age");

        result.Hierarchy.ShouldBeNull();
        result.HierarchyMaxDepth.ShouldBe(5);
    }
}