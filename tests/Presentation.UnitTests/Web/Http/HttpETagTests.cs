// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Http;

using System.Net.Http.Headers;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Http;

public sealed class HttpETagTests
{
    [Theory]
    [InlineData("revision-1")]
    [InlineData("\"0x8DA123456789ABC\"")]
    [InlineData("W/\"datetime'2026-11-16T10%3A30%3A00Z'\"")]
    [InlineData("版本-é-🔒")]
    [InlineData("revision,with,commas")]
    [InlineData(" revision with spaces ")]
    public void Write_WithOpaqueVersion_ReturnsStrongHttpTagThatRoundTripsExactly(string nativeTag)
    {
        // Arrange
        var response = new DefaultHttpContext().Response;

        // Act
        HttpETag.Write(response, nativeTag);
        var header = response.Headers.ETag.ToString();
        var parsed = EntityTagHeaderValue.Parse(header);
        var result = HttpETag.ReadCondition(header);

        // Assert
        parsed.IsWeak.ShouldBeFalse();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(nativeTag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Write_WithoutVersion_LeavesResponseHeadersUnchanged(string nativeTag)
    {
        var response = new DefaultHttpContext().Response;

        HttpETag.Write(response, nativeTag);

        response.Headers.ContainsKey("ETag").ShouldBeFalse();

        response.Headers.ETag = "\"already-selected\"";
        HttpETag.Write(response, nativeTag);

        response.Headers.ETag.ToString().ShouldBe("\"already-selected\"");
    }

    [Fact]
    public void Write_WithExistingHeader_ReplacesItWithOneReusableVersion()
    {
        var response = new DefaultHttpContext().Response;
        response.Headers.ETag = ["\"old-1\"", "\"old-2\""];
        response.Headers["X-Correlation-Id"] = "request-42";

        HttpETag.Write(response, "current");

        response.Headers.ETag.Count.ShouldBe(1);
        HttpETag.ReadCondition(response.Headers.ETag.ToString()).Value.ShouldBe("current");
        response.Headers["X-Correlation-Id"].ToString().ShouldBe("request-42");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(" \t", null)]
    [InlineData(null, " \t")]
    public void ReadCondition_WithoutVersion_ReturnsNoCondition(string header, string body)
    {
        var result = HttpETag.ReadCondition(header, body);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("v1", null, "v1")]
    [InlineData(null, "v1", "v1")]
    [InlineData(" \tv1\t ", null, "v1")]
    [InlineData("\"v1\"", "\"v1\"", "\"v1\"")]
    [InlineData("W/\"v1\"", null, "W/\"v1\"")]
    [InlineData("\"bdk.djE\"", null, "v1")]
    [InlineData(null, "\"bdk.djE\"", "v1")]
    [InlineData("\"bdk.djE\"", "v1", "v1")]
    [InlineData("v1", "\"bdk.djE\"", "v1")]
    [InlineData("\"bdk.djE\"", "\"bdk.djE\"", "v1")]
    [InlineData("\"bdk.InYxIg\"", "\"v1\"", "\"v1\"")]
    public void ReadCondition_WithOneVersionOrEquivalentHeaderAndBody_ReturnsExactNativeVersion(string header, string body, string expected)
    {
        var result = HttpETag.ReadCondition(header, body);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("v1", "V1")]
    [InlineData("v1", "v2")]
    [InlineData("\"v1\"", "v1")]
    [InlineData("\"bdk.djE\"", "v2")]
    [InlineData("\"bdk.djE\"", "\"bdk.djI\"")]
    public void ReadCondition_WithDifferentHeaderAndBodyVersions_ReturnsValidationFailure(string header, string body)
    {
        var result = HttpETag.ReadCondition(header, body);

        result.IsFailure.ShouldBeTrue();
        result.Errors.Single().ShouldBeOfType<ValidationError>();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"*\"")]
    [InlineData("\"one\", \"two\"")]
    [InlineData("one,two")]
    [InlineData("one\rtwo")]
    [InlineData("one\ntwo")]
    [InlineData("one\0two")]
    [InlineData("\"bdk.djE")]
    [InlineData("\"bdk.%%%\"")]
    [InlineData("\"bdk._w\"")]
    [InlineData("\"bdk.\"")]
    [InlineData("\"bdk.Kg\"")]
    [InlineData("\"bdk.AA\"")]
    [InlineData("\"bdk.IA\"")]
    public void ReadCondition_WithUnsupportedOrMalformedCondition_ReturnsValidationFailureForEitherInput(string condition)
    {
        var results = new[]
        {
            HttpETag.ReadCondition(condition),
            HttpETag.ReadCondition(null, condition),
            HttpETag.ReadCondition("v1", condition)
        };

        foreach (var result in results)
        {
            result.IsFailure.ShouldBeTrue();
            result.Errors.Single().ShouldBeOfType<ValidationError>();
        }
    }
}
