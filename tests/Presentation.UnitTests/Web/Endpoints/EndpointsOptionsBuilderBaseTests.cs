// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web;

public class EndpointsOptionsBuilderBaseTests
{
    [Fact]
    public void Build_ConfiguredWithBaseBuilderMethods_ReturnsConfiguredOptions()
    {
        // Arrange
        var metadata = new TestMetadata("custom");
        var sut = new TestEndpointsOptionsBuilder();

        // Act
        var result = sut
            .Enabled(false)
            .GroupPath("api//test")
            .NormalizeGroupPath()
            .GroupTag("primary")
            .GroupTags("secondary", "tertiary")
            .GroupName("group")
            .Summary("summary")
            .Description("description")
            .Deprecated()
            .RouteNamePrefix("Prefix")
            .RequireAuthorization()
            .AllowAnonymous()
            .ExcludeFromDescription()
            .RequireRoles("Admin", "Operator")
            .RequireAuthenticationSchemes("Bearer", "ApiKey")
            .RequirePolicy("policy")
            .RequireCorsPolicy("cors")
            .DisableCors()
            .RequireRateLimitingPolicy("rate")
            .DisableRateLimiting()
            .WithMetadata(metadata, null)
            .Build();

        // Assert
        result.Enabled.ShouldBeFalse();
        result.GroupPath.ShouldBe("api//test");
        result.NormalizeGroupPath.ShouldBeTrue();
        result.GroupTag.ShouldBe("primary");
        result.GroupTags.ShouldBe(["secondary", "tertiary"]);
        result.GroupName.ShouldBe("group");
        result.Summary.ShouldBe("summary");
        result.Description.ShouldBe("description");
        result.Deprecated.ShouldBeTrue();
        result.RouteNamePrefix.ShouldBe("Prefix");
        result.RequireAuthorization.ShouldBeTrue();
        result.AllowAnonymous.ShouldBeTrue();
        result.ExcludeFromDescription.ShouldBeTrue();
        result.RequireRoles.ShouldBe(["Admin", "Operator"]);
        result.RequireAuthenticationSchemes.ShouldBe(["Bearer", "ApiKey"]);
        result.RequirePolicy.ShouldBe("policy");
        result.RequireCorsPolicy.ShouldBe("cors");
        result.DisableCors.ShouldBeTrue();
        result.RequireRateLimitingPolicy.ShouldBe("rate");
        result.DisableRateLimiting.ShouldBeTrue();
        result.Metadata.ShouldBe([metadata]);
    }

    [Fact]
    public void Build_NullRoleAndSchemeArrays_NormalizesToEmptyArrays()
    {
        // Arrange
        var sut = new TestEndpointsOptionsBuilder();

        // Act
        var result = sut
            .GroupTags(null)
            .RequireRoles(null)
            .RequireAuthenticationSchemes(null)
            .Build();

        // Assert
        result.GroupTags.ShouldBeEmpty();
        result.RequireRoles.ShouldBeEmpty();
        result.RequireAuthenticationSchemes.ShouldBeEmpty();
    }

    private sealed class TestEndpointsOptionsBuilder : EndpointsOptionsBuilderBase<TestEndpointsOptions, TestEndpointsOptionsBuilder>;

    private sealed class TestEndpointsOptions : EndpointsOptionsBase;

    private sealed record TestMetadata(string Value);
}