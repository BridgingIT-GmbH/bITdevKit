// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

public class EndpointRegistrationTests
{
    [Fact]
    public void AddEndpoints_InstanceEnabled_RegistersEndpointInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var endpoint = new CountingEndpoints("test");

        // Act
        services.AddEndpoints(endpoint);

        // Assert
        var registered = services
            .BuildServiceProvider()
            .GetServices<IEndpoints>()
            .Single();

        registered.ShouldBeSameAs(endpoint);
    }

    [Fact]
    public void AddEndpoints_Disabled_DoesNotRegisterEndpointInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var endpoint = new CountingEndpoints("test");

        // Act
        services.AddEndpoints(endpoint, enabled: false);

        // Assert
        services.BuildServiceProvider().GetServices<IEndpoints>().ShouldBeEmpty();
    }

    [Fact]
    public void AddEndpoints_TypeEnabled_RegistersConcreteEndpointType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddEndpoints<RegisteredEndpoints>();

        // Assert
        services
            .BuildServiceProvider()
            .GetServices<IEndpoints>()
            .Single()
            .ShouldBeOfType<RegisteredEndpoints>();
    }

    [Fact]
    public void MapEndpoints_RegisteredEndpoints_MapsOnlyEnabledAndUnregisteredEndpointsOnce()
    {
        // Arrange
        var enabledEndpoint = new CountingEndpoints("enabled");
        var disabledEndpoint = new CountingEndpoints("disabled") { Enabled = false };
        var alreadyRegisteredEndpoint = new CountingEndpoints("registered") { IsRegistered = true };
        var app = CreateApplication(services => services.AddEndpoints([enabledEndpoint, disabledEndpoint, alreadyRegisteredEndpoint]));

        // Act
        app.MapEndpoints();
        app.MapEndpoints();

        // Assert
        enabledEndpoint.MapCount.ShouldBe(1);
        enabledEndpoint.IsRegistered.ShouldBeTrue();
        disabledEndpoint.MapCount.ShouldBe(0);
        disabledEndpoint.IsRegistered.ShouldBeFalse();
        alreadyRegisteredEndpoint.MapCount.ShouldBe(0);
        alreadyRegisteredEndpoint.IsRegistered.ShouldBeTrue();
    }

    [Fact]
    public void MapEndpoints_RouteGroupBuilderProvided_MapsEndpointsIntoProvidedGroup()
    {
        // Arrange
        var endpoint = new CountingEndpoints("ping");
        var app = CreateApplication(services => services.AddEndpoints(endpoint));
        var group = app.MapGroup("api/test");

        // Act
        app.MapEndpoints(group);

        // Assert
        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();

        route.RoutePattern.RawText.ShouldBe("api/test/ping");
        endpoint.MapCount.ShouldBe(1);
        endpoint.IsRegistered.ShouldBeTrue();
    }

    private static WebApplication CreateApplication(Action<IServiceCollection> configureServices)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        configureServices(builder.Services);

        return builder.Build();
    }

    private sealed class CountingEndpoints(string route) : IEndpoints
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; }

        public int MapCount { get; private set; }

        public void Map(IEndpointRouteBuilder app)
        {
            this.MapCount++;
            app.MapGet(route, () => Results.Ok());
        }
    }

    private sealed class RegisteredEndpoints : IEndpoints
    {
        public bool Enabled { get; set; } = true;

        public bool IsRegistered { get; set; }

        public void Map(IEndpointRouteBuilder app)
        {
        }
    }
}