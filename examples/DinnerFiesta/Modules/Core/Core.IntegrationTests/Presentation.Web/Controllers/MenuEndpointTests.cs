// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Presentation.Web;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests;
using FluentAssertions;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("DinnerFiesta.Presentation")]
[Module("Core")]
public class MenuEndpointTests : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture;

    public MenuEndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    {
        this.fixture = fixture.WithOutput(output);
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/menus")]
    public async Task Get_SingleExisting_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var menu = await this.PostMenuCreate(route);

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route.Replace("{hostId}", menu.HostId) + $"/{menu.Id}").AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*{menu.HostId}*");
        response.Should().MatchInContent($"*{menu.Name}*");
        response.Should().MatchInContent($"*{menu.Id}*");
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/menus")]
    public async Task Get_SingleNotExisting_ReturnsNotFound(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First();

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()) + $"/{Guid.NewGuid()}").AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be404NotFound(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/menus")]
    public async Task Post_ValidEntity_ReturnsCreated(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First();
        var model = new MenuCreateRequestModel
        {
            HostId = entity.HostId.ToString(),
            Name = entity.Name,
            Description = entity.Description
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be201Created(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Headers.Location.Should().NotBeNull();
        response.Should().MatchInContent($"*{model.HostId}*");
        response.Should().MatchInContent($"*{model.Name}*");
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/menus")]
    public async Task Post_InvalidEntity_ReturnsBadRequest(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First();
        var model = new MenuCreateRequestModel
        {
            HostId = null,
            Name = null,
            Description = entity.Description
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be400BadRequest(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*[ValidationException]*");
        response.Should().MatchInContent($"*{nameof(model.HostId)}*");
        response.Should().MatchInContent($"*{nameof(model.Name)}*");
    }

    private async Task<MenuResponseModel> PostMenuCreate(string route)
    {
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First();
        var model = new MenuCreateRequestModel
        {
            HostId = entity.HostId.ToString(),
            Name = entity.Name,
            Description = entity.Description
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content).AnyContext();
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsAsync<ResultOfMenuResponseModel>()).Value;
    }
}