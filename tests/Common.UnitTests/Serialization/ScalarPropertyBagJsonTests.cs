// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common.UnitTests.Serialization;

using System.Globalization;

public sealed class ScalarPropertyBagJsonTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("null")]
    [InlineData(" \r\n null \t")]
    public void Parse_WithOmittedOrNullMetadata_UsesRequestedPreservationMode(string json)
    {
        var replacement = ScalarPropertyBagJson.Parse(json);
        var preservation = ScalarPropertyBagJson.Parse(json, preserveMissing: true);

        replacement.IsSuccess.ShouldBeTrue();
        replacement.Value.ShouldNotBeNull();
        replacement.Value.ShouldBeEmpty();
        preservation.IsSuccess.ShouldBeTrue();
        preservation.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_WithEmptyObject_ReturnsExplicitEmptyReplacement(bool preserveMissing)
    {
        var result = ScalarPropertyBagJson.Parse(" \n{ } ", preserveMissing);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WithScalarValues_ReturnsPortableClrValuesWithoutInferringStringTypes()
    {
        var result = ScalarPropertyBagJson.Parse("""
            {
              "text": "München",
              "empty": "",
              "true": true,
              "false": false,
              "null": null,
              "min": -9223372036854775808,
              "max": 9223372036854775807,
              "largeInteger": 9223372036854775808,
              "decimal": 123.45,
              "floating": 1e100,
              "dateText": "2030-01-02T03:04:05Z",
              "numericText": "00123",
              "booleanText": "false"
            }
            """);

        result.IsSuccess.ShouldBeTrue();
        var properties = result.Value;
        properties.Get("TEXT").ShouldBe("München");
        properties.Get("empty").ShouldBe(string.Empty);
        properties.Get("true").ShouldBe(true);
        properties.Get("false").ShouldBe(false);
        properties.Contains("null").ShouldBeTrue();
        properties.Get("null").ShouldBeNull();
        properties.Get("min").ShouldBeOfType<long>().ShouldBe(long.MinValue);
        properties.Get("max").ShouldBeOfType<long>().ShouldBe(long.MaxValue);
        properties.Get("largeInteger").ShouldBeOfType<decimal>().ShouldBe(9223372036854775808m);
        properties.Get("decimal").ShouldBeOfType<decimal>().ShouldBe(123.45m);
        properties.Get("floating").ShouldBeOfType<double>().ShouldBe(1e100);
        properties.Get("dateText").ShouldBe("2030-01-02T03:04:05Z");
        properties.Get("numericText").ShouldBe("00123");
        properties.Get("booleanText").ShouldBe("false");
    }

    [Fact]
    public void Parse_WithNonEnglishCulture_ParsesJsonNumbersIndependentlyOfCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var result = ScalarPropertyBagJson.Parse("{\"amount\":1.25}");

            result.IsSuccess.ShouldBeTrue();
            result.Value.Get("amount").ShouldBeOfType<decimal>().ShouldBe(1.25m);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Parse_AcrossCalls_DoesNotShareMutableDefaults()
    {
        var first = ScalarPropertyBagJson.Parse(null).Value;
        var second = ScalarPropertyBagJson.Parse(null).Value;

        first.Set("reviewed", true);

        second.ShouldBeEmpty();
        ScalarPropertyBagJson.Parse("{}").Value.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WithCaseVariants_UsesLastValueAndKeepsFirstKeySpelling()
    {
        var result = ScalarPropertyBagJson.Parse(json: """{"Inspection":1,"inspection":2," Inspection ":3}""");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(property => property.Key).ShouldBe(["Inspection", " Inspection "]);
        result.Value.Get("INSPECTION").ShouldBe(2L);
        result.Value.Get(" Inspection ").ShouldBe(3L);
    }

    [Theory]
    [InlineData("[]", "Properties must be an object.")]
    [InlineData("{\" \":1}", "Property names must not be blank.")]
    [InlineData("{\"nested\":{}}", "Property values must be JSON scalars.")]
    [InlineData("{\"number\":1e400}", "Property numbers must be finite and within the supported numeric range.")]
    [InlineData("{\"number\":1e-400}", "Property numbers must be finite and within the supported numeric range.")]
    public void Parse_WithUnsupportedInput_ReturnsSpecificValidationMessage(string json, string message)
    {
        var result = ScalarPropertyBagJson.Parse(json, preserveMissing: true);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>().Message.ShouldBe(message);
    }

    [Theory]
    [InlineData("invalid JSON")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("{\"nested\":{\"name\":\"Ada\"}}")]
    [InlineData("{\"array\":[1,2]}")]
    [InlineData("{\"\":1}")]
    [InlineData("{\" \":1}")]
    [InlineData("{\"\\t\":1}")]
    [InlineData("{\"overflow\":1e400}")]
    [InlineData("{\"overflow\":-1e400}")]
    [InlineData("{\"underflow\":1e-400}")]
    [InlineData("{\"underflow\":-1e-400}")]
    [InlineData("{\"underflow\":1e-10000}")]
    [InlineData("{\"number\":NaN}")]
    [InlineData("{\"number\":Infinity}")]
    [InlineData("{\"valid\":true,\"invalid\":[]}")]
    public void Parse_WithInvalidMetadata_ReturnsValidationFailureInEitherMode(string json)
    {
        foreach (var preserveMissing in new[] { false, true })
        {
            var result = ScalarPropertyBagJson.Parse(json, preserveMissing);

            result.IsFailure.ShouldBeTrue();
            result.Errors.Single().ShouldBeOfType<ValidationError>();
        }
    }

    [Theory]
    [InlineData("1e-29", 1e-29)]
    [InlineData("-1e-29", -1e-29)]
    [InlineData("6e-29", 6e-29)]
    [InlineData("1.1e-28", 1.1e-28)]
    [InlineData("1e-100", 1e-100)]
    [InlineData("-1e-100", -1e-100)]
    [InlineData("5e-324", double.Epsilon)]
    public void Parse_WithSmallFiniteNumber_PreservesNonzeroValue(string number, double expected)
    {
        var result = ScalarPropertyBagJson.Parse("{\"amount\":" + number + "}");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Get("amount").ShouldBeOfType<double>().ShouldBe(expected);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("-0.000")]
    [InlineData("0e-10000")]
    [InlineData("0e10000")]
    public void Parse_WithZeroSignificand_DoesNotMistakeExponentForUnderflow(string number)
    {
        var result = ScalarPropertyBagJson.Parse("{\"amount\":" + number + "}");

        result.IsSuccess.ShouldBeTrue();
        Convert.ToDouble(result.Value.Get("amount"), CultureInfo.InvariantCulture).ShouldBe(0);
    }
}
