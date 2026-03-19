// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Application.UnitTests.DataPorter;

using System.Text.Json;
using System.Text.Json.Serialization;
using BridgingIT.DevKit.Application.DataPorter;

[UnitTest("Common")]
public class JsonConfigurationTests
{
    [Fact]
    public void GetSerializerOptions_WithoutExplicitOptions_UsesPropertyBasedConfiguration()
    {
        var sut = new JsonConfiguration
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true
        };

        var result = sut.GetSerializerOptions();

        result.WriteIndented.ShouldBeFalse();
        result.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        result.DefaultIgnoreCondition.ShouldBe(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
    }

    [Fact]
    public void GetSerializerOptions_WithExplicitSerializerOptions_UsesConfiguredInstance()
    {
        var serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter());

        var sut = new JsonConfiguration
        {
            SerializerOptions = serializerOptions,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true
        };

        var result = sut.GetSerializerOptions();

        result.ShouldNotBeSameAs(serializerOptions);
        result.WriteIndented.ShouldBeFalse();
        result.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.SnakeCaseLower);
        result.DefaultIgnoreCondition.ShouldBe(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault);
        result.Converters.ShouldContain(converter => converter is JsonStringEnumConverter);
    }
}
