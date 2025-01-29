// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherForecast.Presentation.Web.IntegrationTests;

using System.Net.Http.Headers;
using BridgingIT.DevKit.Presentation.Web;
using DevKit.Presentation;
using Microsoft.Extensions.DependencyInjection;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("WeatherForecast.Presentation")]
[Module("Core")]
public class EndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture = fixture.WithOutput(output);

    [Theory]
    [InlineData("api/_system/echo")]
    public async Task SystemEchoGetTest(string route)
    {
        // arrang/act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient()).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent("*echo*");
    }

    [Theory]
    [InlineData("api/_system/info")]
    public async Task SystemInfoGetTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient()).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should()
            .Satisfy<SystemInfo>(model =>
            {
                model.ShouldNotBeNull();
                model.Runtime.ShouldNotBeNull();
                model.Runtime.Count.ShouldBeGreaterThan(0);
                model.Request.ShouldNotBeNull();
                model.Request.Count.ShouldBeGreaterThan(0);
            });
    }

    [Theory]
    [InlineData("api/core/forecasts")]
    public async Task ForecastGetAllTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient(Fakes.UsersStarwars[0])).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    [Theory]
    [InlineData("api/core/forecasts/auth")]
    public async Task ForecastGetAllWithAuthTest(string route)
    {
        // Arrange & Act
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var response = await (await this.CreateClient(Fakes.UsersStarwars[0])).GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // asert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    private async Task<HttpClient> CreateClient(FakeUser user = null)
    {
        var client = this.fixture.CreateClient();

        if (user != null)
        {
            var provider = this.fixture.Services.GetRequiredService<IFakeIdentityProvider>();
            var response = await provider.HandlePasswordGrantAsync(null, user.Email, user.Password, null);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.AccessToken);
        }

        return client;
    }
}