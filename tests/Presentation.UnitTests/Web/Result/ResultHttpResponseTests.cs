// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[UnitTest("Presentation")]
[Collection(ResultMapTestCollection.Name)]
public class ResultHttpResponseTests : IAsyncLifetime
{
    private const string RequestCorrelationId = "results-http-test";
    private WebApplication app;
    private HttpClient client;

    public async Task InitializeAsync()
    {
        ResultMapErrorHandlerRegistry.ClearHandlers();

        this.app = await StartAppAsync(Environments.Production);
        this.client = this.app.GetTestClient();
    }

    private static async Task<WebApplication> StartAppAsync(string environmentName, bool includeCorrelationMiddleware = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseTestServer();
        builder.Services.AddResultHttpProblemDetails(includeExceptionDetails: builder.Environment.IsEnvironment("LocalDebug"));
        builder.Services.ConfigureHttpJsonOptions(Microsoft.Extensions.DependencyInjection.DefaultJsonSerializerOptions.Configure);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ResultHttpTestController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new ResultPagedJsonConverterFactory()));

        var app = builder.Build();
        if (includeCorrelationMiddleware)
        {
            app.UseMiddleware<CorrelationIdProviderMiddleware>();
        }

        app.MapControllers();
        var minimal = app.MapGroup("/test-results/minimal");
        minimal.MapGet("/message-only", () => ResultHttpTestController.CreateMessageFailure().MapHttpNoContent());
        minimal.MapGet("/validation", () => ResultHttpTestController.CreateValidationFailure().MapHttpNoContent());
        minimal.MapGet("/exception", () => ResultHttpTestController.CreateExceptionFailure().MapHttpNoContent());
        minimal.MapGet("/wrapped-exception", () => ResultHttpTestController.CreateExceptionFailure(wrapped: true).MapHttpNoContent());
        minimal.MapGet("/page", () => ResultHttpTestController.CreateEmptyPage().MapHttpOkPaged());
        minimal.MapGet("/unauthorized", () => Result.Failure(new UnauthorizedError()).MapHttpNoContent());
        minimal.MapGet("/forbidden", () => Result.Failure(new ForbiddenError()).MapHttpNoContent());
        minimal.MapGet("/forbidden-typed", () => Result<PersonStub>.Failure(new ForbiddenError()).MapHttpOk());
        minimal.MapGet("/forbidden-paged", () => ResultPaged<PersonStub>.Failure(new ForbiddenError()).MapHttpOkPaged());
        minimal.MapGet("/forbidden-custom-success", () => Result.Failure(new ForbiddenError())
            .MapHttp<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, ProblemHttpResult>(() => TypedResults.NoContent()));
        minimal.MapGet("/not-found", () => Result<PersonStub>.Failure(new NotFoundError()).MapHttpOk());
        minimal.MapGet("/not-found-paged", () => ResultPaged<PersonStub>.Failure(new NotFoundError()).MapHttpOkPaged());
        minimal.MapGet("/not-found-created", () => Result<PersonStub>.Failure(new NotFoundError()).MapHttpCreated("/items/1"));
        minimal.MapGet("/not-found-accepted", () => Result.Failure(new NotFoundError()).MapHttpAccepted("/jobs/1"));
        minimal.MapGet("/conflict", () => Result.Failure(new ConflictError()).MapHttpNoContent());
        minimal.MapGet("/timeout", () => Result.Failure(new TimeoutError()).MapHttpNoContent());
        minimal.MapGet("/mixed", () => ResultHttpTestController.CreateExceptionFailure()
            .WithError(new ValidationError(ResultHttpTestController.ValidationMessage, "name")).MapHttpNoContent());

        await app.StartAsync();
        return app;
    }

    public async Task DisposeAsync()
    {
        this.client?.Dispose();
        if (this.app is not null)
        {
            await this.app.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_MessageOnlyFailure_ReturnsProblemWithDiagnostics(bool useMvc)
    {
        using var response = await this.GetAsync(useMvc, "message-only");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        AssertProblem(response, body, HttpStatusCode.InternalServerError);
        if (useMvc)
        {
            body.GetProperty("IsSuccess").GetBoolean().ShouldBeFalse();
            body.GetProperty("Messages").GetString().ShouldBe(ResultHttpTestController.FailureMessage);
            body.GetProperty("Errors").GetString().ShouldBeEmpty();
        }
        else
        {
            var result = body.GetProperty("data").GetProperty("result");
            result.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
            result.GetProperty("messages").EnumerateArray().Single().GetString()
                .ShouldBe(ResultHttpTestController.FailureMessage);
            result.GetProperty("errors").GetArrayLength().ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_ValidationFailure_ReturnsBadRequestWithValidationMessage(bool useMvc)
    {
        using var response = await this.GetAsync(useMvc, "validation");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        AssertProblem(response, body, HttpStatusCode.BadRequest);
        if (useMvc)
        {
            body.GetProperty("IsSuccess").GetBoolean().ShouldBeFalse();
            body.GetProperty("Errors").GetString().ShouldBe(ResultHttpTestController.ValidationMessage);
        }
        else
        {
            var data = body.GetProperty("data");
            data.GetProperty("result").GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
            data.GetProperty("errors").GetProperty("name").EnumerateArray().Single().GetString()
                .ShouldBe(ResultHttpTestController.ValidationMessage);
        }
    }

    [Theory]
    [InlineData(false, "exception")]
    [InlineData(true, "exception")]
    [InlineData(false, "wrapped-exception")]
    [InlineData(true, "wrapped-exception")]
    public async Task Get_ExceptionFailure_RedactsExceptionByDefault(bool useMvc, string endpoint)
    {
        using var response = await this.GetAsync(useMvc, endpoint);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        AssertProblem(response, body, HttpStatusCode.InternalServerError);
        body.GetRawText().ShouldNotContain(ResultHttpTestController.ExceptionMessage);
        body.GetRawText().ShouldNotContain(nameof(InvalidOperationException));
        body.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, "exception")]
    [InlineData(true, "exception")]
    [InlineData(false, "wrapped-exception")]
    [InlineData(true, "wrapped-exception")]
    public async Task Get_ExceptionFailure_InConfiguredEnvironment_ReturnsFullException(bool useMvc, string endpoint)
    {
        await using var app = await StartAppAsync("LocalDebug");
        using var client = app.GetTestClient();
        using var response = await GetAsync(client, useMvc, endpoint);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        AssertProblem(response, body, HttpStatusCode.InternalServerError);
        var exception = body.GetProperty("exceptionDetails")[0].GetString();
        exception.ShouldContain(typeof(InvalidOperationException).FullName);
        exception.ShouldContain(ResultHttpTestController.ExceptionMessage);
        exception.ShouldContain(nameof(ResultHttpTestController.CreateExceptionFailure));
    }

    [Fact]
    public async Task Get_ExceptionFailure_InOtherDevelopmentEnvironment_StaysRedacted()
    {
        await using var app = await StartAppAsync(Environments.Development);
        using var client = app.GetTestClient();
        using var response = await GetAsync(client, useMvc: false, "exception");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        AssertProblem(response, json.RootElement, HttpStatusCode.InternalServerError);
        json.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
        json.RootElement.GetRawText().ShouldNotContain(ResultHttpTestController.ExceptionMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_FailureWithoutCorrelationMiddleware_UsesRequestTrace(bool useMvc)
    {
        await using var app = await StartAppAsync(Environments.Production, includeCorrelationMiddleware: false);
        using var client = app.GetTestClient();
        using var response = await GetAsync(client, useMvc, "exception");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        json.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(false, "unauthorized", HttpStatusCode.Unauthorized)]
    [InlineData(true, "unauthorized", HttpStatusCode.Unauthorized)]
    [InlineData(false, "forbidden", HttpStatusCode.Forbidden)]
    [InlineData(true, "forbidden", HttpStatusCode.Forbidden)]
    [InlineData(false, "forbidden-typed", HttpStatusCode.Forbidden)]
    [InlineData(false, "forbidden-paged", HttpStatusCode.Forbidden)]
    [InlineData(false, "forbidden-custom-success", HttpStatusCode.Forbidden)]
    [InlineData(false, "not-found", HttpStatusCode.NotFound)]
    [InlineData(true, "not-found", HttpStatusCode.NotFound)]
    [InlineData(false, "not-found-paged", HttpStatusCode.NotFound)]
    [InlineData(false, "not-found-created", HttpStatusCode.NotFound)]
    [InlineData(false, "not-found-accepted", HttpStatusCode.NotFound)]
    [InlineData(false, "conflict", HttpStatusCode.Conflict)]
    [InlineData(false, "timeout", HttpStatusCode.GatewayTimeout)]
    public async Task Get_KnownFailure_ReturnsMappedStatusAndCorrelationId(
        bool useMvc, string endpoint, HttpStatusCode status)
    {
        using var response = await this.GetAsync(useMvc, endpoint);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        AssertProblem(response, json.RootElement, status);
    }

    [Fact]
    public async Task Get_ValidationAndExceptionFailure_PreservesValidationAndRedactsException()
    {
        using var response = await this.GetAsync(useMvc: false, "mixed");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        AssertProblem(response, body, HttpStatusCode.BadRequest);
        body.GetProperty("data").GetProperty("errors").GetProperty("name")[0].GetString()
            .ShouldBe(ResultHttpTestController.ValidationMessage);
        body.GetRawText().ShouldNotContain(ResultHttpTestController.ExceptionMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_EmptyPageBeyondEnd_ReturnsOkWithPagingMetadata(bool useMvc)
    {
        using var response = await this.GetAsync(useMvc, "page");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        body.GetProperty(useMvc ? "value" : "items").GetArrayLength().ShouldBe(0);
        body.GetProperty(useMvc ? "currentPage" : "page").GetInt32().ShouldBe(3);
        body.GetProperty("pageSize").GetInt32().ShouldBe(2);
        body.GetProperty("totalCount").GetInt64().ShouldBe(3L);
        body.GetProperty("totalPages").GetInt32().ShouldBe(2);
        body.GetProperty("hasPreviousPage").GetBoolean().ShouldBeTrue();
        body.GetProperty("hasNextPage").GetBoolean().ShouldBeFalse();
        if (useMvc)
        {
            body.GetProperty("isSuccess").GetBoolean().ShouldBeTrue();
        }
    }

    private Task<HttpResponseMessage> GetAsync(bool useMvc, string endpoint) =>
        GetAsync(this.client, useMvc, endpoint);

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, bool useMvc, string endpoint)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/test-results/{(useMvc ? "mvc" : "minimal")}/{endpoint}");
        request.Headers.Add(CorrelationId.HeaderName, RequestCorrelationId);
        return await client.SendAsync(request);
    }

    private static void AssertProblem(HttpResponseMessage response, JsonElement body, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        body.GetProperty("status").GetInt32().ShouldBe((int)status);
        body.GetProperty("correlationId").GetString().ShouldBe(RequestCorrelationId);
    }
}

/// <summary>Exposes the Result scenarios used by the HTTP response tests.</summary>
[ApiController]
[Route("test-results/mvc")]
public sealed class ResultHttpTestController : ControllerBase
{
    internal const string FailureMessage = "The operation failed.";
    internal const string ValidationMessage = "A name is required.";
    internal const string ExceptionMessage = "The operation threw an exception.";

    [HttpGet("message-only")]
    public ActionResult MessageOnly() => CreateMessageFailure().ToOkActionResult();

    [HttpGet("validation")]
    public ActionResult Validation() => CreateValidationFailure().ToOkActionResult();

    [HttpGet("exception")]
    public ActionResult ExceptionFailure() => CreateExceptionFailure().ToOkActionResult();

    [HttpGet("wrapped-exception")]
    public ActionResult WrappedExceptionFailure() => CreateExceptionFailure(wrapped: true).ToOkActionResult();

    [HttpGet("unauthorized")]
    public ActionResult UnauthorizedFailure() => Result.Failure(new UnauthorizedError()).ToOkActionResult();

    [HttpGet("forbidden")]
    public ActionResult ForbiddenFailure() => Result.Failure(new ForbiddenError()).ToOkActionResult();

    [HttpGet("not-found")]
    public ActionResult MissingFailure() => Result.Failure(new NotFoundError()).ToOkActionResult();

    [HttpGet("page")]
    public ActionResult<ResultPaged<PersonStub>> Page() => CreateEmptyPage().ToOkActionResult();

    internal static Result CreateMessageFailure() => Result.Failure().WithMessage(FailureMessage);

    internal static Result CreateValidationFailure() => Result.Failure(new ValidationError(ValidationMessage, "name"));

    public static Result CreateExceptionFailure(bool wrapped = false)
    {
        try
        {
            throw new InvalidOperationException(ExceptionMessage);
        }
        catch (InvalidOperationException error)
        {
            IResultError resultError = wrapped
                ? new ExternalServiceError("example", error)
                : new ExceptionError(error);
            return Result.Failure(resultError);
        }
    }

    internal static ResultPaged<PersonStub> CreateEmptyPage() => ResultPaged<PersonStub>.Success([], count: 3, page: 3, pageSize: 2);
}
