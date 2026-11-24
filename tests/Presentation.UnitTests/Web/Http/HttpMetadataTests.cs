// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Http;

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

    [Fact]
    public void ParseProperties_WithCaseVariants_UsesLastValueAndKeepsFirstKeySpelling()
    {
        var result = HttpMetadata.ParseProperties(json: """{"Inspection":1,"inspection":2," Inspection ":3}""");

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
    public void ParseProperties_WithUnsupportedInput_ReturnsSpecificValidationMessage(string json, string message)
    {
        var result = HttpMetadata.ParseProperties(json, preserveMissing: true);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>().Message.ShouldBe(message);
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
