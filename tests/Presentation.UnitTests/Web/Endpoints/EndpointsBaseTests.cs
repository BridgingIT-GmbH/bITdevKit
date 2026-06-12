// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public class EndpointsBaseTests
{
    [Fact]
    public void MapGroup_ConfiguredOptions_AppliesGroupConfigurationMetadata()
    {
        // Arrange
        var customMetadata = new TestMetadata("custom");
        var options = new TestEndpointsOptions
        {
            GroupPath = "api//v1\\test/",
            NormalizeGroupPath = true,
            GroupTag = "primary",
            GroupTags = ["secondary", "tertiary"],
            GroupName = "test-group",
            Summary = "test summary",
            Description = "test description",
            Deprecated = true,
            RouteNamePrefix = "Test.Group",
            RequireAuthorization = true,
            RequireRoles = ["Admin", " ", "Operator"],
            RequireAuthenticationSchemes = ["Bearer", " ", "ApiKey"],
            RequireCorsPolicy = "cors-policy",
            RequireRateLimitingPolicy = "rate-policy"
        };
        options.Metadata.Add(customMetadata);

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);
        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        endpoint.RoutePattern.RawText.ShouldBe("/api/v1/test/items");
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>().EndpointName.ShouldBe("Test.Group.ListItems");
        endpoint.Metadata.GetMetadata<EndpointRouteNamePrefixMetadata>().Prefix.ShouldBe("Test.Group");
        GetMetadataProperty(endpoint, "Tags", "Tags").ShouldBe("primary, secondary, tertiary");
        GetMetadataProperty(endpoint, "EndpointGroupName", "EndpointGroupName").ShouldBe("test-group");
        GetMetadataProperty(endpoint, "EndpointSummary", "Summary").ShouldBe("test summary");
        GetMetadataProperty(endpoint, "EndpointDescription", "Description").ShouldBe("test description");
        endpoint.Metadata.GetMetadata<ObsoleteAttribute>().ShouldNotBeNull();
        authorizeData.ShouldNotBeNull();
        authorizeData.Roles.ShouldBe("Admin,Operator");
        authorizeData.AuthenticationSchemes.ShouldBe("Bearer,ApiKey");
        GetMetadataProperty(endpoint, "EnableCors", "PolicyName").ShouldBe("cors-policy");
        GetMetadataProperty(endpoint, "EnableRateLimiting", "PolicyName").ShouldBe("rate-policy");
        endpoint.Metadata.GetMetadata<TestMetadata>().ShouldBe(customMetadata);
    }

    [Fact]
    public void BuildRouteName_RouteNamePrefixConfigured_ReturnsPrefixedRouteName()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            RouteNamePrefix = "Test.Group."
        };

        // Act
        var result = EndpointsBase.BuildRouteName(options, ".ListItems");

        // Assert
        result.ShouldBe("Test.Group.ListItems");
    }

    [Fact]
    public void BuildRouteName_RouteNamePrefixNotConfigured_ReturnsRouteName()
    {
        // Arrange
        var options = new TestEndpointsOptions();

        // Act
        var result = EndpointsBase.BuildRouteName(options, "ListItems");

        // Assert
        result.ShouldBe("ListItems");
    }

    [Fact]
    public void MapGroup_AllowAnonymousConfigured_SkipsAuthorizationMetadata()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            AllowAnonymous = true,
            RequireAuthorization = true,
            RequireRoles = ["Admin"]
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);

        endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull();
        endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldBeNull();
    }

    [Fact]
    public void MapGroup_DisableCorsAndRateLimitingConfigured_AppliesDisableMetadata()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            DisableCors = true,
            RequireCorsPolicy = "cors-policy",
            DisableRateLimiting = true,
            RequireRateLimitingPolicy = "rate-policy"
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);

        endpoint.Metadata.ShouldContain(metadata => metadata.GetType().Name.Contains("DisableCors"));
        endpoint.Metadata.ShouldContain(metadata => metadata.GetType().Name.Contains("DisableRateLimiting"));
        endpoint.Metadata.ShouldNotContain(metadata => metadata.GetType().Name.Contains("EnableCors"));
        endpoint.Metadata.ShouldNotContain(metadata => metadata.GetType().Name.Contains("EnableRateLimiting"));
    }

    [Fact]
    public void MapGroup_RolesWithoutAuthenticationSchemes_PreservesDefaultSchemeBehavior()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            RequireAuthorization = true,
            RequireRoles = ["Admin"]
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);
        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        authorizeData.ShouldNotBeNull();
        authorizeData.Roles.ShouldBe("Admin");
        authorizeData.AuthenticationSchemes.ShouldBeNull();
    }

    [Fact]
    public void MapGroup_PolicyWithoutAuthenticationSchemes_PreservesDefaultSchemeBehavior()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            RequireAuthorization = true,
            RequirePolicy = "AdminOnly"
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);
        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        authorizeData.ShouldNotBeNull();
        authorizeData.Policy.ShouldBe("AdminOnly");
        authorizeData.AuthenticationSchemes.ShouldBeNull();
    }

    [Fact]
    public void MapGroup_AuthenticationSchemesWithoutRolesOrPolicy_AppliesSchemeAuthorization()
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            RequireAuthorization = true,
            RequireAuthenticationSchemes = ["Bearer", "ApiKey"]
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);
        var authorizeData = endpoint.Metadata.GetMetadata<IAuthorizeData>();

        authorizeData.ShouldNotBeNull();
        authorizeData.Roles.ShouldBeNull();
        authorizeData.Policy.ShouldBeNull();
        authorizeData.AuthenticationSchemes.ShouldBe("Bearer,ApiKey");
    }

    [Theory]
    [InlineData(null, "/items")]
    [InlineData("", "/items")]
    [InlineData("/", "/items")]
    [InlineData("api//v1\\items/", "/api/v1/items/items")]
    public void MapGroup_NormalizeGroupPathConfigured_NormalizesRoutePattern(string path, string expectedPattern)
    {
        // Arrange
        var options = new TestEndpointsOptions
        {
            GroupPath = path,
            NormalizeGroupPath = true
        };

        var app = CreateApplication();
        var sut = new TestEndpoints(options);

        // Act
        sut.Map(app);

        // Assert
        var endpoint = GetSingleEndpoint(app);

        endpoint.RoutePattern.RawText.ShouldBe(expectedPattern);
    }

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();

        return builder.Build();
    }

    private static RouteEndpoint GetSingleEndpoint(WebApplication app)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();
    }

    private static string GetMetadataProperty(Endpoint endpoint, string typeNamePart, string propertyName)
    {
        var metadata = endpoint.Metadata.Single(item => item.GetType().Name.Contains(typeNamePart));
        var value = metadata.GetType().GetProperty(propertyName)?.GetValue(metadata);

        return value switch
        {
            IEnumerable<string> values => string.Join(", ", values),
            _ => value?.ToString()
        };
    }

    private sealed class TestEndpoints(TestEndpointsOptions options) : EndpointsBase
    {
        public override void Map(IEndpointRouteBuilder app)
        {
            var group = this.MapGroup(app, options);
            group.MapGet("items", () => Results.Ok())
                .WithName(options, "ListItems");
        }
    }

    private sealed class TestEndpointsOptions : EndpointsOptionsBase;

    private sealed record TestMetadata(string Value);
}