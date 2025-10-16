// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Core.IntegrationTests.Presentation.Web;

using System.Net.Mime;
using System.Text.Json;
using Core.Presentation.Web.Controllers;
using Dumpify;
using FluentAssertions;
using UnitTests;
using HttpContentExtensions = Common.HttpContentExtensions;

//[Collection(nameof(PresentationCollection))] // https://xunit.net/docs/shared-context#collection-fixture
[IntegrationTest("DinnerFiesta.Presentation")]
[Module("Core")]
public class DinnerEndpointTests(ITestOutputHelper output, CustomWebApplicationFactoryFixture<Program> fixture)
    : IClassFixture<CustomWebApplicationFactoryFixture<Program>> // https://xunit.net/docs/shared-context#class-fixture
{
    private readonly CustomWebApplicationFactoryFixture<Program> fixture = fixture.WithOutput(output);

    [Theory]
    [InlineData("api/core/hosts/{hostId}/dinners")]
    public async Task Get_SingleExisting_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var menu = await this.PostMenuCreate(route.Replace("/dinners", "/menus"));
        var model = await this.PostDinnerCreate(route, menu.HostId, menu.Id);
        route = route.Replace("{hostId}", model.HostId) + $"/{model.Id}";

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route)
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*{model.HostId}*");
        response.Should().MatchInContent($"*{model.Name}*");
        var responseModel = await HttpContentExtensions.ReadAsAsync<DinnerModel>(response.Content);
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/dinners")]
    public async Task Get_MultipleExisting_ReturnsOk(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var menu = await this.PostMenuCreate(route.Replace("/dinners", "/menus"));
        var model = await this.PostDinnerCreate(route, menu.HostId, menu.Id);
        route = route.Replace("{hostId}", model.HostId);

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route)
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be200Ok(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent($"*{model.HostId}*");
        response.Should().MatchInContent($"*{model.Name}*");
        var responseModel = await HttpContentExtensions.ReadAsAsync<ICollection<DinnerModel>>(response.Content);
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/dinners")]
    public async Task Get_SingleNotExisting_ReturnsNotFound(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var entity = Stubs.Dinners(DateTime.UtcNow.Ticks).First();

        // Act
        var response = await this.fixture.CreateClient()
            .GetAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()) + $"/{Guid.NewGuid()}")
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be404NotFound(); // https://github.com/adrianiftode/FluentAssertions.Web
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/dinners")]
    public async Task Post_ValidModel_ReturnsCreated(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var menu = await this.PostMenuCreate(route.Replace("/dinners", "/menus"));
        var ticks = DateTime.UtcNow.Ticks;
        var entity = Stubs.Dinners(ticks).First();
        var model = new DinnerModel
        {
            Name = entity.Name,
            Description = entity.Description,
            HostId = menu.HostId,
            MenuId = menu.Id,
            MaxGuests = entity.MaxGuests,
            Price = new PriceModel { Amount = entity.Price.Amount, Currency = entity.Price.Currency },
            Location = new DinnerLocationModel
            {
                Name = entity.Location.Name,
                AddressLine1 = entity.Location.AddressLine1,
                AddressLine2 = entity.Location.AddressLine2,
                PostalCode = entity.Location.PostalCode,
                City = entity.Location.City,
                Country = entity.Location.Country
            },
            Schedule = new DinnerScheduleModel
            {
                StartDateTime = entity.Schedule.StartDateTime,
                EndDateTime = entity.Schedule.EndDateTime
            }
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultJsonSerializerOptions.Create()),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content)
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be201Created(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Headers.Location.Should().NotBeNull();
        response.Should().MatchInContent($"*{model.HostId}*");
        response.Should().MatchInContent($"*{model.Name}*");
        var responseModel = await HttpContentExtensions.ReadAsAsync<DinnerModel>(response.Content);
        responseModel.ShouldNotBeNull();
        this.fixture.Output.WriteLine($"ResponseModel: {responseModel.DumpText()}");
    }

    [Theory]
    [InlineData("api/core/hosts/{hostId}/dinners")]
    public async Task Post_InvalidEntity_ReturnsBadRequest(string route)
    {
        // Arrange
        this.fixture.Output.WriteLine($"Start Endpoint test for route: {route}");
        var menu = await this.PostMenuCreate(route.Replace("/dinners", "/menus"));
        var entity = Stubs.Dinners(DateTime.UtcNow.Ticks).First();
        var model = new DinnerModel
        {
            HostId = menu.HostId,
            MenuId = menu.Id,
            Name = null,
            Description = entity.Description
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultJsonSerializerOptions.Create()),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        // Act
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content)
            .AnyContext();
        this.fixture.Output.WriteLine($"Finish Endpoint test for route: {route} (status={(int)response.StatusCode})");

        // Assert
        response.Should().Be400BadRequest(); // https://github.com/adrianiftode/FluentAssertions.Web
        response.Should().MatchInContent("*[ValidationException]*");
        response.Should().MatchInContent($"*{nameof(model.Name)}*");
        response.Should().MatchInContent($"*{nameof(model.Schedule)}*");
        response.Should().MatchInContent($"*{nameof(model.Price)}*");
        response.Should().MatchInContent($"*{nameof(model.Location)}*");
    }

    private async Task<DinnerModel> PostDinnerCreate(string route, string hostId, string menuId)
    {
        var entity = Stubs.Dinners(DateTime.UtcNow.Ticks).First();
        var model = new DinnerModel
        {
            Name = entity.Name,
            Description = entity.Description,
            HostId = hostId,
            MenuId = menuId,
            MaxGuests = entity.MaxGuests,
            Price = new PriceModel { Amount = entity.Price.Amount, Currency = entity.Price.Currency },
            Location = new DinnerLocationModel
            {
                Name = entity.Location.Name,
                AddressLine1 = entity.Location.AddressLine1,
                AddressLine2 = entity.Location.AddressLine2,
                PostalCode = entity.Location.PostalCode,
                City = entity.Location.City,
                Country = entity.Location.Country
            },
            Schedule = new DinnerScheduleModel
            {
                StartDateTime = entity.Schedule.StartDateTime,
                EndDateTime = entity.Schedule.EndDateTime
            }
        };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultJsonSerializerOptions.Create()),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content).AnyContext();
        response.EnsureSuccessStatusCode();

        return await HttpContentExtensions.ReadAsAsync<DinnerModel>(response.Content);
    }

    private async Task<MenuModel> PostMenuCreate(string route)
    {
        var entity = Stubs.Menus(DateTime.UtcNow.Ticks).First();
        var model = new MenuModel { HostId = entity.HostId, Name = entity.Name, Description = entity.Description };
        var content = new StringContent(
            JsonSerializer.Serialize(model, DefaultJsonSerializerOptions.Create()),
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        var response = await this.fixture.CreateClient()
            .PostAsync(route.Replace("{hostId}", entity.HostId.Value.ToString()), content).AnyContext();
        response.EnsureSuccessStatusCode();

        return await HttpContentExtensions.ReadAsAsync<MenuModel>(response.Content);
    }
}