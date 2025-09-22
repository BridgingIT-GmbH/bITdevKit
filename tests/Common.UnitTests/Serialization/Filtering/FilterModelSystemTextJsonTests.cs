// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Runtime.Serialization;
using System.Text.Json;
using Xunit;
using Shouldly;
using BridgingIT.DevKit.Common;

public class FilterModelSystemTextJsonTests
{
    private readonly SystemTextJsonSerializer serializer = new();

    [Fact]
    public void SerializeAndDeserialize_FilterModel_ShouldMatchOriginal()
    {
        // Arrange
        var filterModel = this.CreateSampleFilterModel();

        // Act
        var json = this.serializer.SerializeToString(filterModel);
        var deserializedModel = this.serializer.Deserialize<FilterModel>(json);

        // Assert
        deserializedModel.ShouldNotBeNull();
        this.AssertFilterModelEquals(deserializedModel, filterModel);
    }

    [Fact]
    public void SerializeToJsonString_FilterModel_ShouldProduceValidJson()
    {
        // Arrange
        var filterModel = this.CreateSampleFilterModel();

        // Act
        var jsonString = this.serializer.SerializeToString(filterModel);

        // Assert
        this.AssertJsonStringContains(jsonString, filterModel);
    }

    [Fact]
    public void RoundtripSerialization_FilterModel_ShouldProduceValidFilterModel()
    {
        // Arrange
        var filterModel = this.CreateSampleFilterModel();

        // Act
        var jsonString = this.serializer.SerializeToString(filterModel);
        var deserializedModel = this.serializer.Deserialize<FilterModel>(jsonString);

        // Assert
        //this.AssertJsonStringContains(jsonString, filterModel);
        deserializedModel.HasFilters().ShouldBeTrue();
        deserializedModel.HasFilters("Status").ShouldBeTrue();
        deserializedModel.GetFilter("Status", FilterOperator.Equal).Value.ShouldBe(ActiveStatus.Active);
    }

    [Fact]
    public void DeserializeFromJsonString_FilterModel_ShouldProduceValidObject()
    {
        // Arrange
        var jsonString = this.CreateSampleFilterModelJson();

        // Act
        var filterModel = this.serializer.Deserialize<FilterModel>(jsonString);

        // Assert
        this.AssertFilterModelMatchesJson(filterModel);
    }

    [Fact]
    public void SerializeAndDeserialize_ComplexFilterCriteria_ShouldMatchOriginal()
    {
        // Arrange
        var filterCriteria = this.CreateComplexFilterCriteria();

        // Act
        var json = this.serializer.SerializeToString(filterCriteria);
        var deserializedCriteria = this.serializer.Deserialize<FilterCriteria>(json);

        // Assert
        deserializedCriteria.ShouldNotBeNull();
        this.AssertComplexFilterCriteriaEquals(deserializedCriteria, filterCriteria);
    }

    [Fact]
    public void SerializeToJsonString_ComplexFilterCriteria_ShouldProduceValidJson()
    {
        // Arrange
        var filterCriteria = this.CreateComplexFilterCriteria();

        // Act
        var jsonString = this.serializer.SerializeToString(filterCriteria);

        // Assert
        this.AssertJsonStringContainsComplexFilterCriteria(jsonString);
    }

    [Fact]
    public void DeserializeFromJsonString_ComplexFilterCriteria_ShouldProduceValidObject()
    {
        // Arrange
        var jsonString = this.CreateComplexFilterCriteriaJson();

        // Act
        var filterCriteria = this.serializer.Deserialize<FilterCriteria>(jsonString);

        // Assert
        this.AssertComplexFilterCriteriaMatchesJson(filterCriteria);
    }

    // Helper methods for creating sample data and assertions

    private FilterModel CreateSampleFilterModel()
    {
        return new FilterModel
        {
            Page = 2,
            PageSize = 15,
            Filters =
            [
                new FilterCriteria
                {
                    Field = "Age",
                    Operator = FilterOperator.GreaterThanOrEqual,
                    Value = 18
                },
                new FilterCriteria
                {
                    Field = "Name",
                    Operator = FilterOperator.Contains,
                    Value = "John"
                },
                new FilterCriteria
                {
                    Field = "Addresses",
                    Operator = FilterOperator.Any,
                    Filters = [
                        new FilterCriteria { Field = "City", Operator = FilterOperator.Equal, Value = "Berlin" }
                    ]
                },
                new FilterCriteria
                {
                    Field = "Status",
                    Operator = FilterOperator.Equal,
                    Value = ActiveStatus.Active
                },
                new FilterCriteria(FilterCustomType.DateRange,
                    new Dictionary<string, object>
                    {
                        ["Field"] = "BirthDate",
                        ["StartDate"] = "1900-01-01",
                        ["EndDate"] = "2030-12-31"
                    })
            ],
            Orderings =
            [
                new FilterOrderCriteria
                {
                    Field = "LastName",
                    Direction = OrderDirection.Ascending
                }
            ],
            Includes = ["Orders", "Address"]
        };
    }

    private void AssertFilterModelEquals(FilterModel actual, FilterModel expected)
    {
        actual.Page.ShouldBe(expected.Page);
        actual.PageSize.ShouldBe(expected.PageSize);
        actual.Filters.Count.ShouldBe(expected.Filters.Count);
        actual.Orderings.Count.ShouldBe(expected.Orderings.Count);
        actual.Includes.ShouldBe(expected.Includes);

        for (var i = 0; i < expected.Filters.Count; i++)
        {
            actual.Filters[i].Field.ShouldBe(expected.Filters[i].Field);
            actual.Filters[i].Operator.ShouldBe(expected.Filters[i].Operator);
            //actual.Filters[i].Value.ShouldBe(expected.Filters[i].Value);
        }

        for (var i = 0; i < expected.Orderings.Count; i++)
        {
            actual.Orderings[i].Field.ShouldBe(expected.Orderings[i].Field);
            actual.Orderings[i].Direction.ShouldBe(expected.Orderings[i].Direction);
        }
    }

    private void AssertJsonStringContains(string jsonString, FilterModel model)
    {
        jsonString.ShouldContain($"page\": {model.Page}");
        jsonString.ShouldContain($"pageSize\": {model.PageSize}");
        foreach (var filter in model.Filters)
        {
            if (filter.Field != null)
            {
                jsonString.ShouldContain($"field\": \"{filter.Field}\"");
                jsonString.ShouldContain($"operator\": \"gte");
                jsonString.ShouldContain($"Value\": \"John\"");
            }
        }

        foreach (var ordering in model.Orderings)
        {
            jsonString.ShouldContain($"field\": \"{ordering.Field}\"");
            jsonString.ShouldContain($"direction\": \"asc");
        }

        foreach (var include in model.Includes)
        {
            jsonString.ShouldContain($"{include}\"");
        }
    }

    private string CreateSampleFilterModelJson()
    {
        return """
               {
                           "page": 2,
                           "pageSize": 15,
                           "filters": [
                               {
                                   "field": "Age",
                                   "operator": "gte",
                                   "value": 18
                               },
                               {
                                   "field": "Name",
                                   "operator": "contains",
                                   "value": "John"
                               },
                               {
                                   "field": "Addresses",
                                   "operator": "any",
                                   "filters": [
                                       {
                                           "field": "City",
                                           "operator": "eq",
                                           "value": "Berlin"
                                       }
                                   ]
                               }
                           ],
                           "orderings": [
                               {
                                   "field": "LastName",
                                   "direction": "asc"
                               }
                           ],
                           "includes": ["Orders", "Address"]
                       }
               """;
    }

    private void AssertFilterModelMatchesJson(FilterModel model)
    {
        model.ShouldNotBeNull();
        model.Page.ShouldBe(2);
        model.PageSize.ShouldBe(15);
        model.Filters.Count.ShouldBe(3);
        model.Orderings.Count.ShouldBe(1);
        model.Includes.Count.ShouldBe(2);

        model.Filters[0].Field.ShouldBe("Age");
        model.Filters[0].Operator.ShouldBe(FilterOperator.GreaterThanOrEqual);
        //model.Filters[0].Value.ShouldBe(18);

        model.Filters[1].Field.ShouldBe("Name");
        model.Filters[1].Operator.ShouldBe(FilterOperator.Contains);
        //model.Filters[1].Value.ShouldBe("John");

        model.Filters[2].Field.ShouldBe("Addresses");
        model.Filters[2].Operator.ShouldBe(FilterOperator.Any);
        //model.Filters[1].Value.ShouldBe("John");

        model.Orderings[0].Field.ShouldBe("LastName");
        model.Orderings[0].Direction.ShouldBe(OrderDirection.Ascending);

        model.Includes.ShouldContain("Orders");
        model.Includes.ShouldContain("Address");
    }

    private FilterCriteria CreateComplexFilterCriteria()
    {
        return new FilterCriteria
        {
            CustomType = FilterCustomType.CompositeSpecification,
            CompositeSpecification = new CompositeSpecification
            {
                Nodes =
                [
                    new SpecificationLeaf
                    {
                        Name = "AdultSpecification",
                        Arguments = [18]
                    },
                    new SpecificationGroup
                    {
                        Logic = FilterLogicOperator.Or,
                        Nodes =
                        [
                            new SpecificationLeaf
                            {
                                Name = "HighValueCustomerSpecification",
                                Arguments = [1000.0m]
                            },

                            new SpecificationLeaf
                            {
                                Name = "LoyalCustomerSpecification",
                                Arguments = [5]
                            }
                        ]
                    }
                ]
            }
        };
    }

    private void AssertComplexFilterCriteriaEquals(FilterCriteria actual, FilterCriteria expected)
    {
        actual.Field.ShouldBe(expected.Field);
        actual.CustomType.ShouldBe(expected.CustomType);
        actual.CompositeSpecification.ShouldNotBeNull();
        actual.CompositeSpecification.Nodes.Count.ShouldBe(expected.CompositeSpecification.Nodes.Count);

        for (var i = 0; i < expected.CompositeSpecification.Nodes.Count; i++)
        {
            var actualNode = actual.CompositeSpecification.Nodes[i];
            var expectedNode = expected.CompositeSpecification.Nodes[i];

            if (expectedNode is SpecificationLeaf expectedLeaf)
            {
                var actualLeaf = actualNode as SpecificationLeaf;
                actualLeaf.ShouldNotBeNull();
                actualLeaf.Name.ShouldBe(expectedLeaf.Name);
                // actualLeaf.Arguments.ShouldBe(expectedLeaf.Arguments);
            }
            else if (expectedNode is SpecificationGroup expectedGroup)
            {
                var actualGroup = actualNode as SpecificationGroup;
                actualGroup.ShouldNotBeNull();
                actualGroup.Logic.ShouldBe(expectedGroup.Logic);
                actualGroup.Nodes.Count.ShouldBe(expectedGroup.Nodes.Count);
                // Recursively check nested nodes if needed
            }
        }
    }

    private void AssertJsonStringContainsComplexFilterCriteria(string jsonString)
    {
        jsonString.ShouldContain("customType\": \"compositespecification\"");
        jsonString.ShouldContain("compositeSpecification\"");
        jsonString.ShouldContain("name\": \"AdultSpecification\"");
        //jsonString.ShouldContain("arguments\": [18]");
        jsonString.ShouldContain("logic\": \"or\"");
        jsonString.ShouldContain("name\": \"HighValueCustomerSpecification\"");
        //jsonString.ShouldContain("arguments\": [1000.0]");
        jsonString.ShouldContain("name\": \"LoyalCustomerSpecification\"");
        //jsonString.ShouldContain("arguments\": [5]");
    }

    private string CreateComplexFilterCriteriaJson()
    {
        return """
               {
                           "name": "ComplexFilter",
                           "customType": "compositespecification",
                           "compositeSpecification": {
                               "nodes": [
                                   {
                                       "name": "AdultSpecification",
                                       "arguments": [18]
                                   },
                                   {
                                       "logic": "or",
                                       "nodes": [
                                           {
                                               "name": "HighValueCustomerSpecification",
                                               "arguments": [1000.0]
                                           },
                                           {
                                               "name": "LoyalCustomerSpecification",
                                               "arguments": [5]
                                           }
                                       ]
                                   }
                               ]
                           }
                       }
               """;
    }

    private void AssertComplexFilterCriteriaMatchesJson(FilterCriteria criteria)
    {
        criteria.ShouldNotBeNull();
        //criteria.Field.ShouldBe("ComplexFilter");
        criteria.CustomType.ShouldBe(FilterCustomType.CompositeSpecification);
        criteria.CompositeSpecification.ShouldNotBeNull();
        criteria.CompositeSpecification.Nodes.Count.ShouldBe(2);

        var leaf = criteria.CompositeSpecification.Nodes[0] as SpecificationLeaf;
        leaf.ShouldNotBeNull();
        leaf.Name.ShouldBe("AdultSpecification");
        //leaf.Arguments.ShouldContain(18);

        var group = criteria.CompositeSpecification.Nodes[1] as SpecificationGroup;
        group.ShouldNotBeNull();
        group.Logic.ShouldBe(FilterLogicOperator.Or);
        group.Nodes.Count.ShouldBe(2);

        var highValueLeaf = group.Nodes[0] as SpecificationLeaf;
        highValueLeaf.ShouldNotBeNull();
        highValueLeaf.Name.ShouldBe("HighValueCustomerSpecification");
        //highValueLeaf.Arguments.ShouldContain(1000.0m);

        var loyalLeaf = group.Nodes[1] as SpecificationLeaf;
        loyalLeaf.ShouldNotBeNull();
        loyalLeaf.Name.ShouldBe("LoyalCustomerSpecification");
        //loyalLeaf.Arguments.ShouldContain(5);
    }
}

public class EnumConverterTests
{
    private readonly Faker faker;
    private readonly JsonSerializerOptions options;
    private readonly SystemTextJsonSerializer serializer;

    public EnumConverterTests()
    {
        this.faker = new Faker();
        this.options = DefaultSystemTextJsonSerializerOptions.Create();
        this.options.Converters.Insert(0, new EnumConverter<TestEnum>());
        this.serializer = new SystemTextJsonSerializer(this.options);
    }

    [Fact]
    public void Read_ValidEnumValue_ReturnsCorrectEnum()
    {
        // Arrange
        var json = "\"CustomValue1\"";
        var expectedEnum = TestEnum.Value1;

        // Act
        var result = this.serializer.Deserialize<TestEnum>(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));

        // Assert
        result.ShouldBe(expectedEnum);
    }

    [Fact]
    public void Read_InvalidEnumValue_ThrowsJsonException()
    {
        // Arrange
        var json = "\"InvalidValue\"";

        // Act & Assert
        Should.Throw<JsonException>(() =>
            this.serializer.Deserialize<TestEnum>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
        );
    }

    [Fact]
    public void Write_EnumWithEnumMemberAttribute_WritesAttributeValue()
    {
        // Arrange
        const TestEnum enumValue = TestEnum.Value2;

        // Act
        var result = JsonSerializer.Serialize(enumValue, this.options);

        // Assert
        result.ShouldBe("\"CustomValue2\"");
    }

    [Fact]
    public void Write_EnumWithoutEnumMemberAttribute_WritesEnumName()
    {
        // Arrange
        var enumValue = TestEnum.Value1;

        // Act
        var result = JsonSerializer.Serialize(enumValue, this.options);

        // Assert
        result.ShouldBe("\"CustomValue1\"");
    }

    [Fact]
    public void ReadWrite_RoundTrip_PreservesEnumValue()
    {
        // Arrange
        var originalEnum = TestEnum.Value2;

        // Act
        var json = JsonSerializer.Serialize(originalEnum, this.options);
        var deserializedEnum = JsonSerializer.Deserialize<TestEnum>(json, this.options);

        // Assert
        deserializedEnum.ShouldBe(originalEnum);
    }

    [Fact]
    public void ReadWrite_RoundTrip_PreservesSmartEnumValue() // does not need EnumConverter
    {
        // Arrange
        var originalEnum = ActiveStatus.Active;

        // Act
        var json = JsonSerializer.Serialize(originalEnum, this.options);
        var deserializedEnum = JsonSerializer.Deserialize<ActiveStatus>(json, this.options);

        // Assert
        deserializedEnum.ShouldBe(originalEnum);
    }

    private enum TestEnum
    {
        [EnumMember(Value = "CustomValue1")]
        Value1,

        [EnumMember(Value = "CustomValue2")]
        Value2
    }
}