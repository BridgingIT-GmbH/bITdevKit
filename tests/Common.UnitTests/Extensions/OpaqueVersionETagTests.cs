// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Common.UnitTests.Extensions;

using Microsoft.AspNetCore.Http;

public sealed class OpaqueVersionETagTests
{
    [Theory]
    [InlineData("\"v1\"", "\"bdk.InYxIg\"")]
    [InlineData("é,a", "\"bdk.w6ksYQ\"")]
    [InlineData(" v1 ", "\"bdk.IHYxIA\"")]
    public void Write_WithOpaqueVersion_PreservesExactBytesAndRoundTrips(string version, string expectedHeader)
    {
        var response = new DefaultHttpContext().Response;

        OpaqueVersionETag.Write(response, version);
        var condition = OpaqueVersionETag.ReadCondition(response.Headers.ETag);

        response.Headers.ETag.ToString().ShouldBe(expectedHeader);
        condition.IsSuccess.ShouldBeTrue();
        condition.Value.ShouldBe(version);
    }

    [Theory]
    [InlineData("\"bdk.djE\"", "v1", true)]
    [InlineData("\"bdk.InYxIg\"", "v1", false)]
    [InlineData("v1", "V1", false)]
    public void ReadCondition_WithHeaderAndBody_ComparesDecodedValuesOrdinally(string header, string body, bool equivalent)
    {
        var condition = OpaqueVersionETag.ReadCondition(header, body);

        condition.IsSuccess.ShouldBe(equivalent);
        if (equivalent)
        {
            condition.Value.ShouldBe(body);
        }
        else
        {
            condition.Errors.Single().ShouldBeOfType<ValidationError>().Message
                .ShouldBe("If-Match and the body ETag must identify the same version.");
        }
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"v1\", \"v2\"")]
    [InlineData("\"bdk._w\"")]
    [InlineData("\"bdk.djE=\"")]
    public void ReadCondition_WithUnsupportedToken_ReturnsValidationError(string header)
    {
        var condition = OpaqueVersionETag.ReadCondition(header);

        condition.IsFailure.ShouldBeTrue();
        condition.Errors.Single().ShouldBeOfType<ValidationError>().Message
            .ShouldBe("Supply one concrete ETag returned by this API; wildcards and ETag lists are unsupported.");
    }
}
