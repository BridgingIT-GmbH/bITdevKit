// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Middleware;

using System.Diagnostics;
using System.Net.Http.Json;
using BridgingIT.DevKit.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class CorrelationIdProviderMiddlewareTests
{
    private const int GeneratedIdLength = 12;

    [Fact]
    public async Task Invoke_WithoutCorrelationId_GeneratesDistinctCorrelationIdsAndStableFlowIds()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Act
        using var firstResponse = await client.GetAsync("/");
        using var secondResponse = await client.GetAsync("/");
        using var otherFlowResponse = await client.GetAsync("/other");
        var firstCorrelationId = GetHeader(firstResponse, CorrelationId.HeaderName);
        var secondCorrelationId = GetHeader(secondResponse, CorrelationId.HeaderName);
        var firstFlowId = GetHeader(firstResponse, "FlowId");
        var secondFlowId = GetHeader(secondResponse, "FlowId");
        var otherFlowId = GetHeader(otherFlowResponse, "FlowId");

        // Assert
        IsGeneratedId(firstCorrelationId).ShouldBeTrue();
        IsGeneratedId(secondCorrelationId).ShouldBeTrue();
        IsGeneratedId(firstFlowId).ShouldBeTrue();
        IsGeneratedId(secondFlowId).ShouldBeTrue();
        IsGeneratedId(otherFlowId).ShouldBeTrue();
        firstCorrelationId.ShouldNotBe(secondCorrelationId);
        firstFlowId.ShouldBe(secondFlowId);
        firstFlowId.ShouldBe(CreateExpectedFlowId(HttpMethod.Get.Method, "/"));
        otherFlowId.ShouldBe(CreateExpectedFlowId(HttpMethod.Get.Method, "/other"));
        otherFlowId.ShouldNotBe(firstFlowId);
        firstCorrelationId.ShouldNotBe(firstFlowId);
    }

    [Fact]
    public async Task Invoke_WithValidHeaderCorrelationId_PreservesIt()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            "UPSTREAM-Correlation_123"
        );

        // Act
        using var response = await app.GetTestClient().SendAsync(request);
        var correlationId = GetHeader(response, CorrelationId.HeaderName);
        var flowId = GetHeader(response, "FlowId");

        // Assert
        correlationId.ShouldBe("UPSTREAM-Correlation_123");
        IsGeneratedId(flowId).ShouldBeTrue();
        flowId.ShouldBe(CreateExpectedFlowId(HttpMethod.Get.Method, "/"));
    }

    [Fact]
    public async Task Invoke_WithValidQueryCorrelationId_PreservesIt()
    {
        // Arrange
        await using var app = await CreateAppAsync();

        // Act
        using var response = await app.GetTestClient()
            .GetAsync($"/?{CorrelationId.HeaderName}=query-correlation_123");

        // Assert
        GetHeader(response, CorrelationId.HeaderName).ShouldBe("query-correlation_123");
    }

    [Fact]
    public async Task Invoke_WithValidHeaderAndQueryCorrelationIds_PrefersHeader()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/?{CorrelationId.HeaderName}=query-correlation"
        );
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, "header-correlation");

        // Act
        using var response = await app.GetTestClient().SendAsync(request);

        // Assert
        GetHeader(response, CorrelationId.HeaderName).ShouldBe("header-correlation");
    }

    [Fact]
    public async Task Invoke_WithMaximumLengthCorrelationId_PreservesIt()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var suppliedCorrelationId = new string('a', CorrelationId.MaximumLength);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, suppliedCorrelationId);

        // Act
        using var response = await app.GetTestClient().SendAsync(request);

        // Assert
        GetHeader(response, CorrelationId.HeaderName).ShouldBe(suppliedCorrelationId);
    }

    [Theory]
    [InlineData("contains whitespace")]
    [InlineData("contains,comma")]
    [InlineData("ümlaut")]
    [InlineData("slash/value")]
    public async Task Invoke_WithInvalidHeaderCorrelationId_GeneratesReplacementWithoutError(
        string suppliedCorrelationId)
    {
        // Arrange
        await using var app = await CreateAppAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, suppliedCorrelationId);

        // Act
        using var response = await app.GetTestClient().SendAsync(request);
        var correlationId = GetHeader(response, CorrelationId.HeaderName);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        IsGeneratedId(correlationId).ShouldBeTrue();
        correlationId.ShouldNotBe(suppliedCorrelationId);
    }

    [Fact]
    public async Task Invoke_WithOversizedHeaderCorrelationId_GeneratesReplacementWithoutError()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var suppliedCorrelationId = new string('a', CorrelationId.MaximumLength + 1);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, suppliedCorrelationId);

        // Act
        using var response = await app.GetTestClient().SendAsync(request);
        var correlationId = GetHeader(response, CorrelationId.HeaderName);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        IsGeneratedId(correlationId).ShouldBeTrue();
        correlationId.ShouldNotBe(suppliedCorrelationId);
    }

    [Fact]
    public async Task Invoke_WithMultipleHeaderCorrelationIds_GeneratesReplacementWithoutError()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation(
            CorrelationId.HeaderName,
            ["first-correlation", "second-correlation"]
        );

        // Act
        using var response = await app.GetTestClient().SendAsync(request);
        var correlationId = GetHeader(response, CorrelationId.HeaderName);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        IsGeneratedId(correlationId).ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_WithInvalidHeaderAndValidQueryCorrelationId_UsesQueryValue()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/?{CorrelationId.HeaderName}=query-correlation"
        );
        request.Headers.TryAddWithoutValidation(CorrelationId.HeaderName, "invalid value");

        // Act
        using var response = await app.GetTestClient().SendAsync(request);

        // Assert
        GetHeader(response, CorrelationId.HeaderName).ShouldBe("query-correlation");
    }

    [Fact]
    public async Task Invoke_WithMultipleQueryCorrelationIds_GeneratesReplacementWithoutError()
    {
        // Arrange
        await using var app = await CreateAppAsync();

        // Act
        using var response = await app.GetTestClient()
            .GetAsync(
                $"/?{CorrelationId.HeaderName}=first&{CorrelationId.HeaderName}=second"
            );
        var correlationId = GetHeader(response, CorrelationId.HeaderName);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        IsGeneratedId(correlationId).ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_WithDifferentMinimalRouteValues_UsesMethodAndRouteFlowId()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Act
        using var firstResponse = await client.GetAsync("/orders/1");
        using var secondResponse = await client.GetAsync("/orders/2");
        using var postResponse = await client.PostAsync("/orders/1", null);
        var firstFlowId = GetHeader(firstResponse, "FlowId");
        var secondFlowId = GetHeader(secondResponse, "FlowId");
        var postFlowId = GetHeader(postResponse, "FlowId");

        // Assert
        firstFlowId.ShouldBe(secondFlowId);
        firstFlowId.ShouldBe(CreateExpectedFlowId(HttpMethod.Get.Method, "/orders/{id}"));
        postFlowId.ShouldBe(CreateExpectedFlowId(HttpMethod.Post.Method, "/orders/{id}"));
        postFlowId.ShouldNotBe(firstFlowId);
    }

    [Fact]
    public async Task Invoke_WithDifferentMvcRouteValues_UsesMethodAndRouteFlowId()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Act
        using var firstResponse = await client.GetAsync("/mvc/orders/1");
        using var secondResponse = await client.GetAsync("/mvc/orders/2");
        using var postResponse = await client.PostAsync("/mvc/orders/1", null);
        var firstFlowId = GetHeader(firstResponse, "FlowId");
        var secondFlowId = GetHeader(secondResponse, "FlowId");
        var postFlowId = GetHeader(postResponse, "FlowId");

        // Assert
        firstFlowId.ShouldBe(secondFlowId);
        firstFlowId.ShouldBe(
            CreateExpectedFlowId(HttpMethod.Get.Method, "mvc/orders/{id}")
        );
        postFlowId.ShouldBe(
            CreateExpectedFlowId(HttpMethod.Post.Method, "mvc/orders/{id}")
        );
        postFlowId.ShouldNotBe(firstFlowId);
    }

    [Fact]
    public async Task Invoke_WithDifferentConventionalMvcRouteValues_UsesStableUseCaseFlowId()
    {
        // Arrange
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Act
        using var firstResponse = await client.GetAsync(
            "/mvc-conventional/CorrelationConventionalFlowTest/Execute/1"
        );
        using var secondResponse = await client.GetAsync(
            "/mvc-conventional/CorrelationConventionalFlowTest/Execute/2"
        );
        using var postResponse = await client.PostAsync(
            "/mvc-conventional/CorrelationConventionalFlowTest/Execute/1",
            null
        );
        using var otherActionResponse = await client.GetAsync(
            "/mvc-conventional/CorrelationConventionalFlowTest/Other/1"
        );
        var firstFlowId = GetHeader(firstResponse, "FlowId");
        var secondFlowId = GetHeader(secondResponse, "FlowId");
        var postFlowId = GetHeader(postResponse, "FlowId");
        var otherActionFlowId = GetHeader(otherActionResponse, "FlowId");

        // Assert
        firstFlowId.ShouldBe(secondFlowId);
        firstFlowId.ShouldBe(
            CreateExpectedFlowId(
                HttpMethod.Get.Method,
                "mvc-conventional/{controller}/{action}/{id?}"
                    + "|controller=CorrelationConventionalFlowTest"
                    + "|action=Execute"
            )
        );
        postFlowId.ShouldNotBe(firstFlowId);
        otherActionFlowId.ShouldNotBe(firstFlowId);
    }

    [Fact]
    public async Task Invoke_WhenRegisteredTwice_ReusesRequestIdentifiers()
    {
        // Arrange
        await using var app = await CreateAppAsync(useDuplicateMiddleware: true);

        // Act
        using var response = await app.GetTestClient().GetAsync("/state");
        var state = await response.Content.ReadFromJsonAsync<RequestStateResponse>();

        // Assert
        state.ShouldNotBeNull();
        state.FirstCorrelationId.ShouldBe(state.CorrelationId);
        state.FirstFlowId.ShouldBe(state.FlowId);
        GetHeader(response, CorrelationId.HeaderName).ShouldBe(state.CorrelationId);
        GetHeader(response, "FlowId").ShouldBe(state.FlowId);
    }

    [Fact]
    public async Task Invoke_DuringRequest_ProvidesMatchingAmbientBaggageAndResponseIdentifiers()
    {
        // Arrange
        await using var app = await CreateAppAsync();

        // Act
        using var response = await app.GetTestClient().GetAsync("/state");
        var state = await response.Content.ReadFromJsonAsync<RequestStateResponse>();

        // Assert
        state.ShouldNotBeNull();
        state.CorrelationId.ShouldBe(GetHeader(response, CorrelationId.HeaderName));
        state.FlowId.ShouldBe(GetHeader(response, "FlowId"));
        state.TraceId.ShouldBe(GetHeader(response, "TraceId"));
        state.CorrelationBaggage.ShouldBe(state.CorrelationId);
        state.FlowBaggage.ShouldBe(state.FlowId);
    }

    [Fact]
    public async Task Invoke_WhenExceptionHandlerReexecutes_ReusesOriginalRequestIdentifiers()
    {
        // Arrange
        await using var app = await CreateAppAsync(useExceptionHandler: true);

        // Act
        using var response = await app.GetTestClient().GetAsync("/throws");
        var state = await response.Content.ReadFromJsonAsync<ReexecutionResponse>();

        // Assert
        state.ShouldNotBeNull();
        state.OriginalCorrelationId.ShouldBe(state.CorrelationId);
        state.OriginalFlowId.ShouldBe(state.FlowId);
        GetHeader(response, CorrelationId.HeaderName).ShouldBe(state.CorrelationId);
        GetHeader(response, "FlowId").ShouldBe(state.FlowId);
    }

    private static async Task<WebApplication> CreateAppAsync(
        bool useDuplicateMiddleware = false,
        bool useExceptionHandler = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(CorrelationAttributeFlowTestController).Assembly);
        var app = builder.Build();

        if (useExceptionHandler)
        {
            app.UseExceptionHandler("/error");
        }

        app.UseRouting();
        app.UseRequestCorrelation();

        if (useDuplicateMiddleware)
        {
            app.Use(async (context, next) =>
            {
                context.Items["FirstCorrelationId"] = CorrelationId.Current;
                context.Items["FirstFlowId"] = context.Items["FlowId"];
                await next(context);
            });
            app.UseRequestCorrelation();
        }

        app.MapGet("/", () => Results.Ok());
        app.MapGet("/other", () => Results.Ok());
        app.MapGet("/orders/{id}", () => Results.Ok());
        app.MapPost("/orders/{id}", () => Results.Ok());
        app.MapGet("/state", (HttpContext context) => Results.Json(
            new RequestStateResponse(
                context.Items["FirstCorrelationId"]?.ToString(),
                context.Items["FirstFlowId"]?.ToString(),
                CorrelationId.Current,
                context.Items["FlowId"]?.ToString(),
                Activity.Current?.TraceId.ToString(),
                Activity.Current?.GetBaggageItem(ActivityConstants.CorrelationIdTagKey),
                Activity.Current?.GetBaggageItem(ActivityConstants.FlowIdTagKey)
            )
        ));
        app.MapGet("/throws", (HttpContext context) =>
        {
            context.Items["OriginalCorrelationId"] = CorrelationId.Current;
            context.Items["OriginalFlowId"] = context.Items["FlowId"];

            return Task.FromException(new InvalidOperationException("Expected test exception."));
        });
        app.MapGet("/error", (HttpContext context) => Results.Json(
            new ReexecutionResponse(
                context.Items["OriginalCorrelationId"]?.ToString(),
                context.Items["OriginalFlowId"]?.ToString(),
                CorrelationId.Current,
                context.Items["FlowId"]?.ToString()
            )
        ));
        app.MapControllers();
        app.MapControllerRoute(
            "correlation-flow-test",
            "mvc-conventional/{controller}/{action}/{id?}"
        );
        await app.StartAsync();
        return app;
    }

    private static string CreateExpectedFlowId(string method, string route) =>
        HashHelper.Compute(
            GuidGenerator.Create($"{method.ToUpperInvariant()} {route}").ToByteArray()
        )[..GeneratedIdLength];

    private static string GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.GetValues(name).Single();

    private static bool IsGeneratedId(string value) =>
        value.Length == GeneratedIdLength
        && value.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9'
        );

    private sealed record RequestStateResponse(
        string FirstCorrelationId,
        string FirstFlowId,
        string CorrelationId,
        string FlowId,
        string TraceId,
        string CorrelationBaggage,
        string FlowBaggage);

    private sealed record ReexecutionResponse(
        string OriginalCorrelationId,
        string OriginalFlowId,
        string CorrelationId,
        string FlowId);
}

[ApiController]
[Route("mvc/orders")]
public sealed class CorrelationAttributeFlowTestController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(string id) => this.Ok(id);

    [HttpPost("{id}")]
    public IActionResult Post(string id) => this.Ok(id);
}

public sealed class CorrelationConventionalFlowTestController : ControllerBase
{
    [AcceptVerbs("GET", "POST")]
    public IActionResult Execute(string id) => this.Ok(id);

    [HttpGet]
    public IActionResult Other(string id) => this.Ok(id);
}
