// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Http;

using System.Globalization;
using BridgingIT.DevKit.Presentation.Web;

public sealed class HttpMetadataTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("null")]
    [InlineData(" \r\n null \t")]
    public void ParseProperties_WithOmittedOrNullMetadata_UsesRequestedPreservationMode(string json)
    {
        var replacement = HttpMetadata.ParseProperties(json);
        var preservation = HttpMetadata.ParseProperties(json, preserveMissing: true);

        replacement.IsSuccess.ShouldBeTrue();
        replacement.Value.ShouldNotBeNull();
        replacement.Value.ShouldBeEmpty();
        preservation.IsSuccess.ShouldBeTrue();
        preservation.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseProperties_WithEmptyObject_ReturnsExplicitEmptyReplacement(bool preserveMissing)
    {
        var result = HttpMetadata.ParseProperties(" \n{ } ", preserveMissing);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void ParseProperties_WithScalarValues_ReturnsPortableClrValuesWithoutInferringStringTypes()
    {
        var result = HttpMetadata.ParseProperties("""
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
    public void ParseProperties_WithNonEnglishCulture_ParsesJsonNumbersIndependentlyOfCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var result = HttpMetadata.ParseProperties("{\"amount\":1.25}");

            result.IsSuccess.ShouldBeTrue();
            result.Value.Get("amount").ShouldBeOfType<decimal>().ShouldBe(1.25m);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseProperties_AcrossCalls_DoesNotShareMutableDefaults()
    {
        var first = HttpMetadata.ParseProperties(null).Value;
        var second = HttpMetadata.ParseProperties(null).Value;

        first.Set("reviewed", true);

        second.ShouldBeEmpty();
        HttpMetadata.ParseProperties("{}").Value.ShouldBeEmpty();
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
    public void ParseProperties_WithInvalidMetadata_ReturnsValidationFailureInEitherMode(string json)
    {
        foreach (var preserveMissing in new[] { false, true })
        {
            var result = HttpMetadata.ParseProperties(json, preserveMissing);

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
    public void ParseProperties_WithSmallFiniteNumber_PreservesNonzeroValue(string number, double expected)
    {
        var result = HttpMetadata.ParseProperties("{\"amount\":" + number + "}");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Get("amount").ShouldBeOfType<double>().ShouldBe(expected);
    }

    [Theory]
    [InlineData("0.0")]
    [InlineData("-0.000")]
    [InlineData("0e-10000")]
    [InlineData("0e10000")]
    public void ParseProperties_WithZeroSignificand_DoesNotMistakeExponentForUnderflow(string number)
    {
        var result = HttpMetadata.ParseProperties("{\"amount\":" + number + "}");

        result.IsSuccess.ShouldBeTrue();
        Convert.ToDouble(result.Value.Get("amount"), CultureInfo.InvariantCulture).ShouldBe(0);
    }

    [Theory]
    [InlineData(false, ExpirationChangeMode.Preserve)]
    [InlineData(true, ExpirationChangeMode.Clear)]
    public void ResolveExpiration_WithoutDate_PreservesOrClearsAsRequested(bool clearExpiration, ExpirationChangeMode expected)
    {
        var result = HttpMetadata.ResolveExpiration(null, clearExpiration);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Mode.ShouldBe(expected);
        result.Value.ExpiresAt.ShouldBeNull();
        result.Value.TimeToLive.ShouldBeNull();
    }

    [Fact]
    public void ResolveExpiration_WithDate_ReturnsAbsoluteExpiration()
    {
        var expiresAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));

        var result = HttpMetadata.ResolveExpiration(expiresAt, false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Mode.ShouldBe(ExpirationChangeMode.Set);
        result.Value.ExpiresAt.ShouldBe(expiresAt);
        result.Value.TimeToLive.ShouldBeNull();
    }

    [Fact]
    public void ResolveExpiration_WithDateAndClear_ReturnsValidationFailure()
    {
        var result = HttpMetadata.ResolveExpiration(DateTimeOffset.MaxValue, true);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>();
    }
}
