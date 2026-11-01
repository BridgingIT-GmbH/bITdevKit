// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Broadcasting;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class BroadcastingManagementEndpointsTests
{
    [Fact]
    public async Task SearchNodes_ForwardsFilterToDiagnostics()
    {
        var diagnostics = Substitute.For<IBroadcastingDiagnostics>();
        diagnostics.SearchNodesAsync(Arg.Any<FilterModel>(), Arg.Any<CancellationToken>())
            .Returns(ResultPaged<BroadcastNodeRegistration>.Success([new() { NodeIdentity = "node-a" }], 2, 1, 1));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(diagnostics);
        builder.Services.AddBroadcastingManagementEndpoints(options => options.RequireAuthorization = false);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync("/_bdk/api/broadcasting/search", new FilterModel { PageSize = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("node-a");
        await diagnostics.Received(1).SearchNodesAsync(Arg.Is<FilterModel>(filter => filter.PageSize == 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchNodes_DefaultAuthorization_IsIndependentOfAnonymousTransportReceiver()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Substitute.For<IBroadcastingDiagnostics>());
        builder.Services.AddBroadcastingManagementEndpoints();
        await using var app = builder.Build();
        app.MapEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .Single(endpoint => endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IEndpointNameMetadata>()?.EndpointName == "_bdk.Broadcasting.SearchNodes");

        endpoint.Metadata.GetMetadata<IAuthorizeData>().ShouldNotBeNull();
        endpoint.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
    }
}
