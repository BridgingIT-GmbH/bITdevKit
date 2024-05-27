// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Presentation.Web;

using System.Text.Json;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.Presentation.Web.Controllers;
using BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.UnitTests;
using Dumpify;
using FluentAssertions;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("DinnerFiesta.Presentation")]
[Module("Core")]
public class HostEndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture) : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture = fixture.WithOutput(output);

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Get_SingleExisting_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var model = await this.PostHostCreate(route);

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route + $"/{model.Id}").AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*{model.FirstName}*");
        response.Should().MatchInContent($"*{model.LastName}*");
        var responseModel = await response.Content.ReadAsAsync<HostModel>();
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Get_MultipleExisting_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var model = await this.PostHostCreate(route);

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*{model.FirstName}*");
        response.Should().MatchInContent($"*{model.LastName}*");
        var responseModel = await response.Content.ReadAsAsync<IEnumerable<HostModel>>();
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Get_SingleNotExisting_ReturnsNotFound(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route + $"/{Guid.NewGuid()}").AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be404NotFound(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Post_ValidModel_ReturnsCreated(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Hosts(DateTime.UtcNow.Ticks).First();
        var model = new HostModel
        {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            UserId = Guid.NewGuid().ToString()
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route, content).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be201Created(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Headers.Location.Should().NotBeNull();
        response.Should().MatchInContent($"*{model.FirstName}*");
        response.Should().MatchInContent($"*{model.LastName}*");
        var responseModel = await response.Content.ReadAsAsync<HostModel>();
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Put_ValidModel_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var model = await this.PostHostCreate(route);
        model.FirstName += "changed";
        model.LastName += "changed";
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PutAsync(route + $"/{model.Id}", content).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Headers.Location.Should().NotBeNull();
        response.Should().MatchInContent($"*{model.FirstName}*");
        response.Should().MatchInContent($"*{model.LastName}*");
        var responseModel = await response.Content.ReadAsAsync<HostModel>();
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts")]
    public async Task Post_InvalidEntity_ReturnsBadRequest(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Hosts(DateTime.UtcNow.Ticks).First();
        var model = new HostModel
        {
            FirstName = string.Empty,
            LastName = string.Empty,
            UserId = entity.UserId.ToString()
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route, content).AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be400BadRequest(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*[ValidationException]*");
        response.Should().MatchInContent($"*{nameof(model.FirstName)}*");
        response.Should().MatchInContent($"*{nameof(model.LastName)}*");
    }

    private async Task<HostModel> PostHostCreate(string route)
    {
        var entity = Stubs.Hosts(DateTime.UtcNow.Ticks).First();
        var model = new HostModel
        {
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            UserId = entity.UserId.ToString()
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultSystemTextJsonSerializerOptions.Create()), Encoding.UTF8, System.Net.Mime.MediaTypeNames.Application.Json);
        var response = await this.fixture.CreateClient()
            .PostAsync(route, content).AnyContext();
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsAsync<HostModel>();
    }
}