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

    [Fact]
    public void IsEmpty_WithNewFilterModel_ShouldReturnTrue()
    {
        // Arrange
        var model = new FilterModel();

        // Act
        var result = model.IsEmpty();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_WithPopulatedFilterModel_ShouldReturnFalse()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        var result = model.IsEmpty();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void HasFilters_WithMatchingField_ShouldReturnTrue()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        var result = model.HasFilters("Name");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void HasOrdering_WithMatchingField_ShouldReturnTrue()
    {
        // Arrange
        var model = new FilterModel
        {
            Orderings = [new() { Field = "Name", Direction = OrderDirection.Ascending }]
        };

        // Act
        var result = model.HasOrdering("Name");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AddOrUpdateFilter_NewFilter_ShouldAddFilter()
    {
        // Arrange
        var model = new FilterModel();

        // Act
        model.AddOrUpdateFilter("Name", FilterOperator.Equal, "Test");

        // Assert
        model.Filters.ShouldHaveSingleItem();
        model.Filters[0].Field.ShouldBe("Name");
        model.Filters[0].Value.ShouldBe("Test");
    }

    [Fact]
    public void AddOrUpdateFilter_ExistingFilter_ShouldUpdateFilter()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Old" }]
        };

        // Act
        model.AddOrUpdateFilter("Name", FilterOperator.Equal, "New");

        // Assert
        model.Filters.ShouldHaveSingleItem();
        model.Filters[0].Value.ShouldBe("New");
    }

    [Fact]
    public void RemoveFilter_ExistingFilter_ShouldRemoveFilter()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        model.RemoveFilter("Name", FilterOperator.Equal);

        // Assert
        model.Filters.ShouldBeEmpty();
    }

    [Fact]
    public void SetHierarchy_ValidPath_ShouldSetHierarchy()
    {
        // Arrange
        var model = new FilterModel();

        // Act
        model.SetHierarchy("Parent.Child", 3);

        // Assert
        model.Hierarchy.ShouldBe("Parent.Child");
        model.HierarchyMaxDepth.ShouldBe(3);
    }

    [Fact]
    public void GetFilter_ExistingFilter_ShouldReturnFilter()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        var result = model.GetFilter("Name", FilterOperator.Equal);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe("Test");
    }

    [Fact]
    public void GetFilters_MultipleFilters_ShouldReturnAllMatching()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test1" },
                new() { Field = "Name", Operator = FilterOperator.Contains, Value = "Test2" }
            ]
        };

        // Act
        var results = model.GetFilters("Name").ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public void Clone_PopulatedModel_ShouldCreateDeepCopy()
    {
        // Arrange
        var original = new FilterModel
        {
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }],
            Page = 2,
            PageSize = 20
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.ShouldNotBe(original);
        clone.Filters[0].Field.ShouldBe(original.Filters[0].Field);
        clone.Page.ShouldBe(original.Page);
    }

    [Fact]
    public void WithoutTracking_ShouldSetNoTracking()
    {
        // Arrange
        var model = new FilterModel { NoTracking = false };

        // Act
        model.WithoutTracking();

        // Assert
        model.NoTracking.ShouldBeTrue();
    }

    [Fact]
    public void WithPaging_ValidValues_ShouldSetPaging()
    {
        // Arrange
        var model = new FilterModel();

        // Act
        model.WithPaging(2, 20);

        // Assert
        model.Page.ShouldBe(2);
        model.PageSize.ShouldBe(20);
    }

    [Fact]
    public void ToQueryString_PopulatedModel_ShouldCreateValidQueryString()
    {
        // Arrange
        var model = new FilterModel
        {
            Page = 2,
            PageSize = 20,
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        var queryString = model.ToQueryString();

        // Assert
        queryString.ShouldContain("page=2");
        queryString.ShouldContain("pageSize=20");
        queryString.ShouldContain("filters=");
    }

    [Fact]
    public void FromQueryString_ValidQueryString_ShouldRestoreModel()
    {
        // Arrange
        var model = new FilterModel
        {
            Page = 2,
            PageSize = 20,
            NoTracking = true
        };
        var queryString = model.ToQueryString();

        // Act
        var restored = FilterModelExtensions.FromQueryString(queryString);

        // Assert
        restored.Page.ShouldBe(2);
        restored.PageSize.ShouldBe(20);
        restored.NoTracking.ShouldBeTrue();
    }

    [Fact]
    public void ToDictionary_PopulatedModel_ShouldCreateValidDictionary()
    {
        // Arrange
        var model = new FilterModel
        {
            Page = 2,
            PageSize = 20,
            Filters = [new() { Field = "Name", Operator = FilterOperator.Equal, Value = "Test" }]
        };

        // Act
        var dict = model.ToDictionary();

        // Assert
        dict["page"].ShouldBe(2);
        dict["pageSize"].ShouldBe(20);
        dict["filters"].ShouldNotBeNull();
    }

    [Fact]
    public void HasFilters_WithNestedFilters_ShouldFindInnerField()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "Parent",
                Operator = FilterOperator.Any,
                Filters =
                [
                    new()
                    {
                        Field = "NestedField",
                        Operator = FilterOperator.Equal,
                        Value = "Test"
                    }
                ]
            }
            ]
        };

        // Act
        var result = model.HasFilters("NestedField");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void GetFilter_WithNestedStructure_ShouldFindNestedFilter()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "Parent",
                Operator = FilterOperator.All,
                Filters =
                [
                    new()
                    {
                        Field = "NestedField",
                        Operator = FilterOperator.Equal,
                        Value = "Test"
                    }
                ]
            }
            ]
        };

        // Act
        var result = model.GetFilter("NestedField", FilterOperator.Equal);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe("Test");
    }

    [Fact]
    public void RemoveFilter_WithNestedFilters_ShouldRemoveFromAllLevels()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "TopLevel",
                Operator = FilterOperator.Equal,
                Value = "Top"
            },
            new()
            {
                Field = "Parent",
                Operator = FilterOperator.Any,
                Filters =
                [
                    new()
                    {
                        Field = "TopLevel",
                        Operator = FilterOperator.Equal,
                        Value = "Nested"
                    }
                ]
            }
            ]
        };

        // Act
        model.RemoveFilter("TopLevel", FilterOperator.Equal);

        // Assert
        model.Filters.Count.ShouldBe(1);
        model.Filters[0].Filters.ShouldBeEmpty();
    }

    [Fact]
    public void GetFilters_WithMultiLevelNesting_ShouldReturnAllMatching()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "SearchField",
                Operator = FilterOperator.Equal,
                Value = "Top"
            },
            new()
            {
                Field = "Level1",
                Operator = FilterOperator.Any,
                Filters =
                [
                    new()
                    {
                        Field = "SearchField",
                        Operator = FilterOperator.Contains,
                        Value = "Nested1"
                    },
                    new()
                    {
                        Field = "Level2",
                        Operator = FilterOperator.All,
                        Filters =
                        [
                            new()
                            {
                                Field = "SearchField",
                                Operator = FilterOperator.StartsWith,
                                Value = "Nested2"
                            }
                        ]
                    }
                ]
            }
            ]
        };

        // Act
        var results = model.GetFilters("SearchField").ToList();

        // Assert
        results.Count.ShouldBe(3);
        results.Select(f => f.Value.ToString()).ShouldContain("Top");
        results.Select(f => f.Value.ToString()).ShouldContain("Nested1");
        results.Select(f => f.Value.ToString()).ShouldContain("Nested2");
    }

    [Fact]
    public void Clone_WithNestedStructure_ShouldCreateDeepCopyOfAllLevels()
    {
        // Arrange
        var original = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "Parent",
                Operator = FilterOperator.Any,
                Filters =
                [
                    new()
                    {
                        Field = "Child",
                        Operator = FilterOperator.Equal,
                        Value = "Test",
                        Filters =
                        [
                            new()
                            {
                                Field = "GrandChild",
                                Operator = FilterOperator.Contains,
                                Value = "Nested"
                            }
                        ]
                    }
                ]
            }
            ]
        };

        // Act
        var clone = original.Clone();

        // Assert
        clone.ShouldNotBe(original);
        clone.Filters[0].ShouldNotBe(original.Filters[0]);
        clone.Filters[0].Filters[0].ShouldNotBe(original.Filters[0].Filters[0]);
        clone.Filters[0].Filters[0].Filters[0].ShouldNotBe(original.Filters[0].Filters[0].Filters[0]);

        // Verify content is the same
        clone.Filters[0].Field.ShouldBe("Parent");
        clone.Filters[0].Filters[0].Field.ShouldBe("Child");
        clone.Filters[0].Filters[0].Filters[0].Field.ShouldBe("GrandChild");
        clone.Filters[0].Filters[0].Filters[0].Value.ShouldBe("Nested");
    }

    [Fact]
    public void AddOrUpdateFilter_WithNestedStructure_ShouldPreserveOtherNestedFilters()
    {
        // Arrange
        var model = new FilterModel
        {
            Filters =
            [
                new()
            {
                Field = "Parent",
                Operator = FilterOperator.Any,
                Filters =
                [
                    new() { Field = "Keep", Operator = FilterOperator.Equal, Value = "Test" }
                ]
            }
            ]
        };

        // Act
        model.AddOrUpdateFilter("Parent", FilterOperator.All, "NewValue");

        // Assert
        model.Filters.Count.ShouldBe(2);
        model.Filters.ShouldContain(f => f.Field == "Parent" && f.Operator == FilterOperator.Any);
        model.Filters.ShouldContain(f => f.Field == "Parent" && f.Operator == FilterOperator.All);
        model.Filters.First(f => f.Field == "Parent" && f.Operator == FilterOperator.Any)
            .Filters.ShouldContain(f => f.Field == "Keep");
    }
}