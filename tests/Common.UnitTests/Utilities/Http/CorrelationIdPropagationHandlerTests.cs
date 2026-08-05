// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities.Http;

using System.Net;
using Microsoft.Extensions.DependencyInjection;

public class CorrelationIdPropagationHandlerTests
{
    [Fact]
    public async Task SendAsync_WithAmbientCorrelationId_OverridesRequestHeader()
    {
        // Arrange
        var primaryHandler = new RecordingHandler();
        using var handler = new CorrelationIdPropagationHandler
        {
            InnerHandler = primaryHandler
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/test");
        request.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            "request-correlation");
        using var scope = CorrelationId.BeginScope("ambient-correlation");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        primaryHandler.CorrelationIds.ShouldBe(["ambient-correlation"]);
        primaryHandler.AmbientCorrelationId.ShouldBe("ambient-correlation");
        CorrelationId.Current.ShouldBe("ambient-correlation");
    }

    [Fact]
    public async Task SendAsync_WithoutValidAmbientId_PreservesValidRequestHeader()
    {
        // Arrange
        var primaryHandler = new RecordingHandler();
        using var handler = new CorrelationIdPropagationHandler
        {
            InnerHandler = primaryHandler
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/test");
        request.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            "request-correlation");
        using var scope = CorrelationId.BeginScope("invalid correlation");

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        primaryHandler.CorrelationIds.ShouldBe(["request-correlation"]);
        primaryHandler.AmbientCorrelationId.ShouldBe("request-correlation");
        CorrelationId.Current.ShouldBe("invalid correlation");
    }

    [Fact]
    public async Task SendAsync_WithoutValidCorrelationId_GeneratesAndScopesIdentifier()
    {
        // Arrange
        var primaryHandler = new RecordingHandler();
        using var handler = new CorrelationIdPropagationHandler
        {
            InnerHandler = primaryHandler
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/test");
        request.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            ["invalid value", "second-value"]);
        using var scope = CorrelationId.BeginScope(string.Empty);

        // Act
        using var response = await client.SendAsync(request);

        // Assert
        primaryHandler.CorrelationIds.Count.ShouldBe(1);
        primaryHandler.CorrelationIds[0].Length.ShouldBe(12);
        primaryHandler.CorrelationIds[0].All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9').ShouldBeTrue();
        primaryHandler.AmbientCorrelationId.ShouldBe(
            primaryHandler.CorrelationIds[0]);
        CorrelationId.Current.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task AddCorrelationIdPropagation_ForAllFactoryClients_AddsHeader()
    {
        // Arrange
        var primaryHandler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("weather")
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        services.AddCorrelationIdPropagation();
        services.AddCorrelationIdPropagation();
        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = CorrelationId.BeginScope("factory-correlation");
        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient("weather");

        // Act
        using var response = await client.GetAsync("https://localhost/test");

        // Assert
        primaryHandler.CorrelationIds.ShouldBe(["factory-correlation"]);
    }

    [Fact]
    public async Task AddCorrelationIdPropagation_ForNamedClient_DoesNotAffectOtherClient()
    {
        // Arrange
        var propagatedHandler = new RecordingHandler();
        var plainHandler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("propagated")
            .AddCorrelationIdPropagation()
            .AddCorrelationIdPropagation()
            .ConfigurePrimaryHttpMessageHandler(() => propagatedHandler);
        services.AddHttpClient("plain")
            .ConfigurePrimaryHttpMessageHandler(() => plainHandler);
        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = CorrelationId.BeginScope("named-correlation");
        var clientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        // Act
        using var propagatedResponse = await clientFactory
            .CreateClient("propagated")
            .GetAsync("https://localhost/test");
        using var plainResponse = await clientFactory
            .CreateClient("plain")
            .GetAsync("https://localhost/test");

        // Assert
        propagatedHandler.CorrelationIds.ShouldBe(["named-correlation"]);
        plainHandler.CorrelationIds.ShouldBeEmpty();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public IReadOnlyList<string> CorrelationIds { get; private set; } = [];

        public string AmbientCorrelationId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CorrelationIds = request.Headers.TryGetValues(
                CorrelationId.HeaderName,
                out var values)
                    ? values.ToArray()
                    : [];
            this.AmbientCorrelationId = CorrelationId.Current;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
