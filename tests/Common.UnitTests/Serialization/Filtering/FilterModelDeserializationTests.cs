// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Serialization.Filtering;

public class FilterModelDeserializationTests
{
    public FilterModelDeserializationTests()
    {
        // Configure JSON options so tests reflect runtime behavior.
        // Option A: Use default web-like options (no external deps):
        //FilterModel.AddJsonOptions(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        // Option B: If available, use your full options (converters, etc.):
        FilterModel.ConfigureJson(DefaultJsonSerializerOptions.Create());
    }

    [Fact]
    public void TryParse_FilterJsonInQuery_Should_DeserializeModel()
    {
        // Arrange
        // This is the single "filter" query value string passed into TryParse by ASP.NET.
        var queryValue = "{\"page\":2,\"pageSize\":25,\"filters\":[{\"field\":\"type.name\",\"operator\":\"isnotnull\"},{\"field\":\"type.name\",\"operator\":\"eq\",\"value\":\"AAA\"},{\"field\":\"temperatureMin\",\"operator\":\"gte\",\"value\":16.1},{\"field\":\"timestamp\",\"operator\":\"gte\",\"value\":\"2024-10-24T10:00:00\"}],\"orderings\":[{\"field\":\"createdAt\",\"direction\":\"Desc\"}]}";

        // Act
        var success = FilterModel.TryParse(queryValue, provider: null, out var model);

        // Assert
        success.ShouldBeTrue();
        model.ShouldNotBeNull();

        model.Page.ShouldBe(2);
        model.PageSize.ShouldBe(25);

        model.Filters.Count.ShouldBe(4);
        model.Filters[0].Field.ShouldBe("type.name");
        // If using default enum parsing, this expects the enum name "isnotnull" to map.
        // If you rely on EnumMember or custom mapping, ensure ConfigureJsonOptions uses your converters.
        model.Filters[0].Operator.ToString().ToLowerInvariant().ShouldBe("isnotnull");

        model.Filters[1].Field.ShouldBe("type.name");
        model.Filters[1].Operator.ToString().ToLowerInvariant().ShouldBe("equal");
        model.Filters[1].Value.ShouldBe("AAA");

        model.Filters[2].Field.ShouldBe("temperatureMin");
        model.Filters[2].Operator.ToString().ToLowerInvariant().ShouldBe("greaterthanorequal");
        model.Filters[2].Value.ShouldBe(16.1d);

        model.Filters[3].Field.ShouldBe("timestamp");
        model.Filters[3].Operator.ToString().ToLowerInvariant().ShouldBe("greaterthanorequal");
        model.Filters[3].Value.ShouldBe("2024-10-24T10:00:00");

        model.Orderings.Count.ShouldBe(1);
        model.Orderings[0].Field.ShouldBe("createdAt");
        model.Orderings[0].Direction.ToString().ToLowerInvariant().ShouldBe("descending");
    }

    [Fact]
    public void TryParse_EmptyString_Should_Fail()
    {
        var success = FilterModel.TryParse("", provider: null, out var model);
        success.ShouldBeFalse();
        model.ShouldNotBeNull(); // result is initialized but ignored when false
    }

    [Fact]
    public void TryParse_InvalidJson_Should_Fail()
    {
        var success = FilterModel.TryParse("{invalid", provider: null, out var model);
        success.ShouldBeFalse();
        model.ShouldNotBeNull();
    }
}