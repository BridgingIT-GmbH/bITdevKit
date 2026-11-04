// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Examples.DoFiesta.IntegrationTests;

using System.Net;
using Microsoft.AspNetCore.Routing;

/// <summary>Verifies the sample exposes Todo APIs and DevKit dashboard pages without obsolete operational APIs.</summary>
/// <example><code>dotnet test --filter FullyQualifiedName~EndpointSurfaceTests</code></example>
[Collection("DoFiesta")]
public sealed class EndpointSurfaceTests(DoFiestaApplicationFactory factory)
{
    [Fact]
    public void RegisteredEndpoints_OperationsAndStreamingApis_AreAbsent()
    {
        // Arrange
        using var client = factory.CreateClient();
        var sut = factory.Services.GetRequiredService<EndpointDataSource>();

        // Act
        var routes = sut.Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText.Trim('/'))
            .Where(route => route.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                || route.StartsWith("/_bdk/api/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        routes.Where(route => route.StartsWith("/_bdk/api/", StringComparison.OrdinalIgnoreCase)
            && !route.StartsWith("/_bdk/api/system", StringComparison.OrdinalIgnoreCase)
            && !route.StartsWith("/_bdk/api/identity", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty();
        routes.ShouldNotContain("/api/core/streaming/ticks");
        routes.ShouldNotContain("/api/core/todoitems/stream");
        routes.ShouldNotContain("/api/core/todoitems/stream/enveloped");
        routes.ShouldNotContain(route => route.StartsWith("/api/core/subscriptions/", StringComparison.OrdinalIgnoreCase));
        routes.ShouldContain("/api/core/todoitems");
        routes.ShouldContain("/api/core/todoitems/search");
        routes.ShouldContain("/api/core/todoitems/history/change-sets");
    }

    [Theory]
    [InlineData("/_bdk/dashboard")]
    [InlineData("/_bdk/dashboard/auditing")]
    [InlineData("/_bdk/dashboard/change-history")]
    [InlineData("/_bdk/dashboard/entities")]
    [InlineData("/_bdk/dashboard/jobs")]
    [InlineData("/_bdk/dashboard/logentries")]
    [InlineData("/_bdk/dashboard/metrics")]
    [InlineData("/_bdk/dashboard/messaging")]
    [InlineData("/_bdk/dashboard/orchestrations")]
    [InlineData("/_bdk/dashboard/queueing")]
    [InlineData("/_bdk/dashboard/storage/files")]
    [InlineData("/_bdk/dashboard/health")]
    public async Task Dashboard_Administrator_CanOpenPageWithoutOperationalApis(string path)
    {
        // Arrange
        using var sut = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        sut.DefaultRequestHeaders.Add("X-Test-User", factory.UserId);
        sut.DefaultRequestHeaders.Add("X-Test-Admin", "true");

        // Act
        using var response = await sut.GetAsync(path);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
    }
}
