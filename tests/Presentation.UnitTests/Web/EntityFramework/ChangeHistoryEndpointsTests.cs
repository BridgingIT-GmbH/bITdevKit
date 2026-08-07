// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.EntityFramework;

using BridgingIT.DevKit.Domain.Model;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory;
using BridgingIT.DevKit.Presentation.Web.EntityFramework.ChangeHistory.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class ChangeHistoryEndpointsTests
{
    [Fact]
    public void Map_ConfiguredOptions_MapsQueryAndRestoreRoutes()
    {
        var app = CreateApplication();
        var sut = new ChangeHistoryEndpoints<ChangeHistoryEndpointEntity, ChangeHistoryEndpointDbContext>(new ChangeHistoryEndpointsOptions
        {
            GroupPath = "/api/entities/history"
        });

        sut.Map(app);

        var routePatterns = GetEndpoints(app).Select(e => e.RoutePattern.RawText).ToArray();

        routePatterns.ShouldContain("/api/entities/history/");
        routePatterns.ShouldContain("/api/entities/history/change-sets");
        routePatterns.ShouldContain("/api/entities/history/change-sets/{changeSetId:guid}");
        routePatterns.ShouldContain("/api/entities/history/{entityId}");
        routePatterns.ShouldContain("/api/entities/history/{entityId}/change-sets/{changeSetId:guid}/restore");
    }

    [Fact]
    public void Map_ReadAndRestorePoliciesConfigured_AppliesPolicyMetadataToExpectedRoutes()
    {
        var app = CreateApplication();
        var sut = new ChangeHistoryEndpoints<ChangeHistoryEndpointEntity, ChangeHistoryEndpointDbContext>(new ChangeHistoryEndpointsOptions
        {
            GroupPath = "/api/entities/history",
            ReadPolicy = "History.Read",
            RestorePolicy = "History.Restore"
        });

        sut.Map(app);

        var endpoints = GetEndpoints(app).ToArray();
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Read");
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/change-sets")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Read");
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/change-sets/{changeSetId:guid}")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Read");
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/{entityId}/change-sets/{changeSetId:guid}/restore")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Restore");
    }

    [Fact]
    public void Options_Defaults_DoNotExposeSerializedValues()
    {
        var options = new ChangeHistoryEndpointsOptions();

        options.IncludeValues.ShouldBeFalse();
    }

    [Fact]
    public void AddChangeHistoryEndpoints_WithGlobalAuthorizationPolicies_AppliesPolicyMetadata()
    {
        var services = new ServiceCollection();
        services.AddRouting();
        services.AddChangeHistory(options => options
            .UseReadAuthorizationPolicy("History.Read")
            .UseRestoreAuthorizationPolicy("History.Restore"));
        services.AddChangeHistoryEndpoints<ChangeHistoryEndpointEntity, ChangeHistoryEndpointDbContext>(new ChangeHistoryEndpointsOptions
        {
            GroupPath = "/api/entities/history"
        });
        var app = CreateApplication();
        var endpoint = services.Single(descriptor => descriptor.ServiceType == typeof(IEndpoints)).ImplementationInstance as IEndpoints;
        endpoint.Map(app);

        var endpoints = GetEndpoints(app).ToArray();
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Read");
        endpoints.Single(e => e.RoutePattern.RawText == "/api/entities/history/{entityId}/change-sets/{changeSetId:guid}/restore")
            .Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldContain(data => data.Policy == "History.Restore");
    }

    [Fact]
    public void AddChangeHistoryEndpoints_RegistersDashboardDescriptor()
    {
        var services = new ServiceCollection();
        services.AddRouting();
        services.AddChangeHistoryEndpoints<ChangeHistoryEndpointEntity, ChangeHistoryEndpointDbContext>(options => options
            .GroupPath("/api/entities/history")
            .IncludeValues());

        var descriptor = services.Single(service => service.ServiceType == typeof(ChangeHistoryDashboardDescriptor))
            .ImplementationInstance as ChangeHistoryDashboardDescriptor;

        descriptor.ShouldNotBeNull();
        descriptor.EntityType.ShouldBe(typeof(ChangeHistoryEndpointEntity));
        descriptor.ContextType.ShouldBe(typeof(ChangeHistoryEndpointDbContext));
        descriptor.ManagementPath.ShouldBe("/api/entities/history");
        descriptor.Options.IncludeValues.ShouldBeTrue();
    }

    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();

        return builder.Build();
    }

    private static IEnumerable<RouteEndpoint> GetEndpoints(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Cast<RouteEndpoint>();

    private sealed class ChangeHistoryEndpointEntity : Entity<Guid>
    {
    }

    private sealed class ChangeHistoryEndpointDbContext(DbContextOptions<ChangeHistoryEndpointDbContext> options) : DbContext(options)
    {
    }
}
