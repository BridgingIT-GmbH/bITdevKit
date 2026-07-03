// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Examples.WeatherFiesta.UnitTests.Modules.Core.Mcp;

using System.Text.Json;
using BridgingIT.DevKit.Examples.WeatherFiesta.Presentation.Web.Server.Modules.Core;

[UnitTest("WeatherFiesta")]
public class CoreModuleMcpHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenInspectCityUsesName_ReturnsCityWeatherDiagnostic()
    {
        // Arrange
        var requester = new FakeRequester
        {
            CitiesResult = Result<List<AdminCityModel>>.Success([
                new AdminCityModel
                {
                    Id = "4a6b2d63-95c3-4f20-89df-a4dd642ba7d1",
                    Name = "Berlin",
                    Country = "Germany",
                    CountryCode = "DE",
                    TimeZone = "Europe/Berlin",
                    Latitude = 52.52m,
                    Longitude = 13.405m,
                    ExternalId = 2950159,
                    SubscriptionCount = 2
                }
            ]),
            WeatherResult = Result<CurrentWeatherModel>.Success(new CurrentWeatherModel
            {
                CityId = "4a6b2d63-95c3-4f20-89df-a4dd642ba7d1",
                Temperature = 21.5m,
                WeatherDescription = "Clear sky"
            })
        };
        var sut = new CoreModuleMcpHandler(requester);
        var arguments = JsonDocument.Parse("{\"name\":\"Berlin\",\"countryCode\":\"DE\"}").RootElement;

        // Act
        var response = await sut.HandleAsync(new McpRequest("weatherfiesta_inspect_city", McpToolset.Diagnostics, arguments), CancellationToken.None);

        // Assert
        response.Available.ShouldBeTrue();
        response.Summary.ShouldContain("Berlin");
        response.Summary.ShouldContain("current weather data");
        requester.WeatherQueryCityId.ShouldBe("4a6b2d63-95c3-4f20-89df-a4dd642ba7d1");
    }

    [Fact]
    public async Task HandleAsync_WhenInspectCityArgumentsAreMissing_ReturnsOperationFailed()
    {
        // Arrange
        var sut = new CoreModuleMcpHandler(new FakeRequester());

        // Act
        var response = await sut.HandleAsync(new McpRequest("weatherfiesta_inspect_city", McpToolset.Diagnostics, EmptyJson()), CancellationToken.None);

        // Assert
        response.Available.ShouldBeFalse();
        response.Code.ShouldBe(McpErrorCode.OperationFailed);
    }

    [Fact]
    public void AddMcpHandler_WhenCoreModuleHandlerIsRegistered_AddsProjectHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMcpHandler<CoreModuleMcpHandler>();

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IMcpHandler) &&
            descriptor.ImplementationType == typeof(CoreModuleMcpHandler));
    }

    private static JsonElement EmptyJson()
        => JsonDocument.Parse("{}").RootElement;

    private sealed class FakeRequester : IRequester
    {
        public Result<List<AdminCityModel>> CitiesResult { get; set; } = Result<List<AdminCityModel>>.Success([]);

        public Result<CurrentWeatherModel> WeatherResult { get; set; } = Result<CurrentWeatherModel>.Failure("No current weather.");

        public string WeatherQueryCityId { get; private set; }

        public Task<Result<TValue>> SendAsync<TRequest, TValue>(
            TRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            where TRequest : class, IRequest<TValue>
            => this.SendAsync((IRequest<TValue>)request, options, cancellationToken);

        public Task<Result<TValue>> SendAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (request is AdminCitiesQuery)
            {
                return Task.FromResult((Result<TValue>)(object)this.CitiesResult);
            }

            if (request is AdminCityWeatherQuery weatherQuery)
            {
                this.WeatherQueryCityId = weatherQuery.CityId;
                return Task.FromResult((Result<TValue>)(object)this.WeatherResult);
            }

            return Task.FromResult(Result<TValue>.Failure("Unexpected request."));
        }

        public Task<Result<TValue>> SendAsync<TValue>(
            RequestBase<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => this.SendAsync((IRequest<TValue>)request, options, cancellationToken);

        public Task<Result> SendDynamicAsync(
            IRequest request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Failure("Unexpected request."));

        public Task<Result<TValue>> SendDynamicAsync<TValue>(
            IRequest<TValue> request,
            SendOptions options = null,
            CancellationToken cancellationToken = default)
            => this.SendAsync(request, options, cancellationToken);

        public RegistrationInformation GetRegistrationInformation()
            => new(new Dictionary<string, IReadOnlyList<string>>(), []);
    }
}
