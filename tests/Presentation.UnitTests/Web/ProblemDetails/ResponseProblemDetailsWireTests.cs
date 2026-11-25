namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using BridgingIT.DevKit.Domain;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Host;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using global::Presentation.TestHost.ProblemDetails;
using DevKitOptions = BridgingIT.DevKit.Presentation.Web.Errors.ProblemDetailsOptions;
using HttpResult = Microsoft.AspNetCore.Http.IResult;
using NativeOptions = Microsoft.AspNetCore.Http.ProblemDetailsOptions;
using Problem = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>Verifies how the registered Problem Details services and middleware produce HTTP responses.</summary>
/// <example><code>await new ResponseProblemDetailsWireTests().Get_KestrelHttpsHttp2_UsesMiddlewareResponseHeaders();</code></example>
[Collection(ResultMapTestCollection.Name)]
public sealed class ResponseProblemDetailsWireTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddResponseProblemDetails_ExistingResultRegistration_UsesFinalCombinedOptions(bool combinedFirst)
    {
        var builder = CreateBuilder();
        var nativeCalls = 0;
        var devKitCalls = 0;
        void AddCombined()
        {
            builder.Services.AddProblemDetails();
            builder.Services.AddProblemDetails(_ => { });
            builder.Services.AddResponseProblemDetails();
            builder.Services.AddResponseProblemDetails(options => options.OnBeforeWriteDetails = (context, problem) =>
            {
                devKitCalls++;
                problem.ShouldBeOfType<Problem>();
                problem.Extensions["application"] = "fixture";
                problem.Status = 418;
                context.Response.StatusCode = 418;
            });
        }

        if (combinedFirst)
        {
            AddCombined();
            builder.Services.AddResultHttpProblemDetails(true);
        }
        else
        {
            builder.Services.AddResultHttpProblemDetails(true);
            AddCombined();
        }

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            nativeCalls++;
            context.ProblemDetails.Title = "Custom title";
            context.ProblemDetails.Status = 400;
        });
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/problem", () => Result.Failure(new ExceptionError(new InvalidOperationException("diagnostic")))
            .MapHttpNoContent());
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/problem");
        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.InternalServerError);
        body.RootElement.GetProperty("application").GetString().ShouldBe("fixture");
        body.RootElement.GetProperty("title").GetString().ShouldBe("Custom title");
        body.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
        nativeCalls.ShouldBe(1);
        devKitCalls.ShouldBe(1);
        app.Services.GetServices<IExceptionHandler>().Count().ShouldBe(1);
    }

    /// <summary>Verifies that post-configuration runs after configuration binding.</summary>
    /// <example><code>await new ResponseProblemDetailsWireTests().Start_BoundOptions_AppliesPostConfigure();</code></example>
    [Fact]
    public async Task Start_BoundOptions_AppliesPostConfigure()
    {
        var builder = CreateBuilder();
        builder.Configuration["ProblemDetails:IncludeExceptionDetails"] = "true";
        builder.Services.AddResponseProblemDetails();
        builder.Services.Configure<DevKitOptions>(builder.Configuration.GetSection("ProblemDetails"));
        builder.Services.PostConfigure<DevKitOptions>(options =>
        {
            options.IncludeExceptionDetails.ShouldBeTrue();
            options.IncludeExceptionDetails = false;
        });
        await using var app = builder.Build();
        await app.StartAsync();

        var options = app.Services.GetRequiredService<IOptions<DevKitOptions>>().Value;
        options.IncludeExceptionDetails.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Start_BothCompleteHandlers_RejectsRegistration(bool combinedFirst)
    {
        var builder = CreateBuilder();
        if (combinedFirst)
        {
            builder.Services.AddResponseProblemDetails();
            builder.Services.AddExceptionHandler();
        }
        else
        {
            builder.Services.AddExceptionHandler();
            builder.Services.AddResponseProblemDetails();
        }

        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        var failure = await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync());
        failure.Message.ShouldContain("AddResponseProblemDetails");
        failure.Message.ShouldContain("AddExceptionHandler");
    }

    [Fact]
    public async Task Start_InvalidStaticStatus_RejectsOptions()
    {
        var builder = CreateBuilder();
        builder.Services.AddResponseProblemDetails(options => options.Map<InvalidOperationException>(200));
        await using var app = builder.Build();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => app.StartAsync());
    }

    [Theory]
    [InlineData("native-first")]
    [InlineData("devkit-first")]
    public async Task Get_CustomOptions_PreservesNativeStatusPagesAndExtensionOrdering(string order)
    {
        var builder = CreateBuilder();
        builder.Services.AddExceptionHandler<FixtureExceptionHandler>();
        builder.Services.Configure<DevKitOptions>(options => options.MapDefault<ArgumentException>(409));
        void ConfigureNativeStatusPages() => builder.Services.Configure<StatusCodePagesOptions>(options =>
            options.HandleAsync = status => status.HttpContext.Response.WriteAsync("native status page"));

        if (order == "native-first")
        {
            ConfigureNativeStatusPages();
        }

        builder.Services.AddResponseProblemDetails(options =>
        {
            options.Map<ArgumentException>((_, _) => new Problem { Status = 422 }, when: (_, _) => false);
            options.Map<ArgumentException>(400, "Host mapping");
            options.OnBeforeWriteDetails = (_, problem) => problem.Extensions["prepared"] = true;
        });
        if (order == "devkit-first")
        {
            ConfigureNativeStatusPages();
        }

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapGet("/native", HttpResult () => throw new FormatException());
        app.MapGet("/mapped", HttpResult () => throw new ArgumentException());
        app.MapGet("/empty", () => TypedResults.NotFound());
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var native = await client.GetAsync("/native");
        using var nativeJson = await PresentationProblemDetailsHostTests.ReadProblemAsync(native, HttpStatusCode.Conflict);
        nativeJson.RootElement.GetProperty("prepared").GetBoolean().ShouldBeTrue();
        using var mapped = await client.GetAsync("/mapped");
        using var mappedJson = await PresentationProblemDetailsHostTests.ReadProblemAsync(mapped, HttpStatusCode.BadRequest);
        mappedJson.RootElement.GetProperty("title").GetString().ShouldBe("Host mapping");
        using var empty = await client.GetAsync("/empty");
        (await empty.Content.ReadAsStringAsync()).ShouldBe("native status page");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_SelectedCustomWriter_PreparesExactlyOnce(bool callsNative)
    {
        var builder = CreateBuilder();
        var calls = 0;
        builder.Services.AddSingleton<IProblemDetailsWriter>(services =>
            new FixtureWriter(services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.ProblemDetailsOptions>>(), callsNative));
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            calls++;
            context.ProblemDetails.Extensions["native"] = true;
        });
        builder.Services.AddResponseProblemDetails(options =>
            options.OnBeforeWriteDetails = (_, problem) => problem.Extensions["devKit"] = true);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/", HttpResult () => throw new InvalidOperationException());
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/vnd.fixture+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("native").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("devKit").GetBoolean().ShouldBeTrue();

        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_NativePostConfiguration_ComposesOnceForMinimalAndMvc(bool nativeFirst)
    {
        var builder = CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemDetailsController).Assembly);
        var nativeCalls = 0;
        var devKitCalls = 0;
        void PostConfigureNative() => builder.Services.PostConfigure<NativeOptions>(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                nativeCalls++;
                context.ProblemDetails.Extensions["native"] = true;
                context.ProblemDetails.Status = 418;
                context.HttpContext.Response.StatusCode = 418;
            };
        });

        if (nativeFirst)
        {
            PostConfigureNative();
        }

        builder.Services.AddResponseProblemDetails(options => options.OnBeforeWriteDetails = (_, problem) =>
        {
            devKitCalls++;
            problem.Extensions["native"].ShouldBe(true);
            problem.Extensions["devKit"] = true;
        });
        if (!nativeFirst)
        {
            PostConfigureNative();
        }

        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/problem", () => TypedResults.Problem(statusCode: 409));
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var minimal = await client.GetAsync("/problem");
        using var mvc = await client.PostAsJsonAsync(ProblemDetailsScenario.ApiPrefix + "/mvc/model-state", new { });

        using var minimalBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(minimal, HttpStatusCode.Conflict);
        using var mvcBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(mvc, HttpStatusCode.BadRequest);
        minimalBody.RootElement.GetProperty("devKit").GetBoolean().ShouldBeTrue();
        mvcBody.RootElement.GetProperty("devKit").GetBoolean().ShouldBeTrue();
        mvcBody.RootElement.GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Object);
        nativeCalls.ShouldBe(2);
        devKitCalls.ShouldBe(2);
    }

    [Fact]
    public async Task Get_ReusedExceptionProblem_KeepsPerRequestFieldsAndFallbackValidation()
    {
        var builder = CreateBuilder();
        var template = new InventoryProblem("INVALID-SKU") { Status = 400 };
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        builder.Services.AddResponseProblemDetails(options =>
        {
            options.IncludeExceptionDetails = true;
            options.Map<ArgumentException>(_ => template);
            options.OnBeforeWriteDetails = (_, problem) =>
                ((InventoryProblem)problem).Errors["sku"][0] = "Writer-enriched message.";
        });
        await using var app = builder.Build();
        app.UseMiddleware<CorrelationIdProviderMiddleware>();
        app.UseResponseProblemDetails();
        app.MapGet("/problem", HttpResult (HttpContext http) => throw new ArgumentException(http.TryGetCorrelationId()));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var first = new HttpRequestMessage(HttpMethod.Get, "/problem");
        first.Headers.Add(CorrelationId.HeaderName, "first-request");
        first.Headers.Add("Accept", "application/json");
        using var second = new HttpRequestMessage(HttpMethod.Get, "/problem");
        second.Headers.Add(CorrelationId.HeaderName, "second-request");
        second.Headers.Add("Accept", "application/xml");

        using var firstResponse = await client.SendAsync(first);
        using var secondResponse = await client.SendAsync(second);

        using var firstBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(firstResponse, HttpStatusCode.BadRequest);
        firstBody.RootElement.GetProperty("exceptionDetails")[0].GetProperty("message").GetString().ShouldBe("first-request");
        firstBody.RootElement.GetProperty("errors").GetProperty("sku")[0].GetString().ShouldBe("Writer-enriched message.");
        using var secondBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(secondResponse, HttpStatusCode.BadRequest);
        secondBody.RootElement.GetProperty("correlationId").GetString().ShouldBe("second-request");
        secondBody.RootElement.GetProperty("inventory_code").GetString().ShouldBe("INVALID-SKU");
        secondBody.RootElement.GetProperty("errors").GetProperty("sku")[0].GetString().ShouldBe("Supply a valid SKU.");
        secondBody.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
        template.Extensions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("application/json", "request-id")]
    [InlineData("application/xml", "nested-operation")]
    public async Task Get_MappedResultWithNestedCorrelation_PreservesIdentifiersForEachWriterPath(string accept, string expected)
    {
        var builder = CreateBuilder();
        builder.Services.AddResponseProblemDetails();
        await using var app = builder.Build();
        app.UseMiddleware<CorrelationIdProviderMiddleware>();
        app.UseResponseProblemDetails();
        app.MapGet("/problem", () =>
        {
            using var scope = CorrelationId.BeginScope("nested-operation");
            return Result.Failure(new ConflictError()).MapHttpNoContent();
        });
        app.MapGet("/explicit", () =>
        {
            var problem = ResultHttpProblemDetails.Create(409);
            problem.Extensions["correlationId"] = "application-id";
            return TypedResults.Problem(problem);
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Accept", accept);
        client.DefaultRequestHeaders.Add(CorrelationId.HeaderName, "request-id");

        using var response = await client.GetAsync("/problem");
        using var explicitResponse = await client.GetAsync("/explicit");

        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.Conflict);
        using var explicitBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(explicitResponse, HttpStatusCode.Conflict);
        body.RootElement.GetProperty("correlationId").GetString().ShouldBe(expected);
        explicitBody.RootElement.GetProperty("correlationId").GetString().ShouldBe("application-id");
    }

    [Theory]
    [InlineData("application/json", null, "execution-id")]
    [InlineData("application/json", "application-id", "application-id")]
    [InlineData("application/xml", null, "mapping-id")]
    public async Task Get_ResultWithDifferentAmbientScopes_LogsTheResponseCorrelationId(string accept, string overrideId, string expected)
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        builder.Services.AddResponseProblemDetails(options =>
        {
            if (overrideId is not null)
            {
                options.OnBeforeWriteDetails = (_, problem) => problem.Extensions["correlationId"] = overrideId;
            }
        });
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/problem", async (HttpContext http) =>
        {
            HttpResult result;
            using (CorrelationId.BeginScope("mapping-id"))
            {
                result = Result.Failure(new TimeoutError()).MapHttpNoContent();
            }

            using var scope = CorrelationId.BeginScope("execution-id");
            await result.ExecuteAsync(http);
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Accept", accept);

        using var response = await client.GetAsync("/problem");

        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.GatewayTimeout);
        body.RootElement.GetProperty("correlationId").GetString().ShouldBe(expected);
        var log = logs.Messages.Single(message => message.Contains("result - HTTP 504 failure", StringComparison.Ordinal));
        log.ShouldContain($"correlationId={expected}");
    }

    [Fact]
    public async Task Get_CustomServerResult_LogsEachMappingOnceAcrossMinimalAndMvc()
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemDetailsController).Assembly);
        builder.Services.AddResponseProblemDetails();
        var shared = TypedResults.Problem(statusCode: 503, title: "Unavailable inventory");
        ResultMapHttpExtensions.RegisterErrorHandler<ExceptionError>((logger, _) =>
        {
            logger?.LogError("Explicit custom Result log.");
            return shared;
        });
        ResultMapHttpExtensions.RegisterErrorHandler<TimeoutError>((_, _) => TypedResults.StatusCode(502));
        try
        {
            await using var app = builder.Build();
            app.UseResponseProblemDetails();
            app.MapControllers();
            app.MapGet("/custom", () => Result.Failure(new ExceptionError(new FormatException()))
                .WithError(new ConflictError()).MapHttpNoContent());
            app.MapGet("/explicit", (ILoggerFactory loggers) => Result.Failure(new ExceptionError(new FormatException()))
                .MapHttpNoContent(loggers.CreateLogger("Explicit")));
            app.MapGet("/wrapped", () => Result.Failure(new TimeoutError()).MapHttpNoContent());
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add("Accept", "application/xml");

            using var minimal = await client.GetAsync("/custom");
            using var mvc = await client.GetAsync(ProblemDetailsScenario.ApiPrefix + "/mvc/result/server");
            using var explicitLogger = await client.GetAsync("/explicit");
            using var wrapped = await client.GetAsync("/wrapped");

            using var minimalBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(minimal, HttpStatusCode.ServiceUnavailable);
            using var mvcBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(mvc, HttpStatusCode.ServiceUnavailable);
            using var explicitBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(explicitLogger, HttpStatusCode.ServiceUnavailable);
            using var wrappedBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(wrapped, HttpStatusCode.BadGateway);
            minimalBody.RootElement.GetProperty("title").GetString().ShouldBe("Unavailable inventory");
            mvcBody.RootElement.GetProperty("title").GetString().ShouldBe("Unavailable inventory");
            explicitBody.RootElement.GetProperty("title").GetString().ShouldBe("Unavailable inventory");
            var customLogs = logs.Messages.Where(message => message.Contains("result - HTTP 503 failure", StringComparison.Ordinal)).ToArray();
            customLogs.Length.ShouldBe(2);
            customLogs.Count(message => message.Contains(nameof(ConflictError), StringComparison.Ordinal)).ShouldBe(1);
            logs.Messages.Count(message => message.Contains("result - HTTP 502 failure", StringComparison.Ordinal)).ShouldBe(1);
            logs.Messages.Count(message => message == "Explicit custom Result log.").ShouldBe(1);
        }
        finally
        {
            ResultMapHttpExtensions.RemoveErrorHandler<ExceptionError>();
            ResultMapHttpExtensions.RemoveErrorHandler<TimeoutError>();
        }
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Get_CustomMvcResponse_UsesActualStatusForResultLogging(bool logResults, int entries)
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemDetailsController).Assembly);
        builder.Services.AddResponseProblemDetails(options => options.ShouldLogResultErrors = logResults);
        ResultMapHttpExtensions.RegisterErrorHandler<ExceptionError>((_, _) =>
            TypedResults.Json(new { reason = "Inventory unavailable." }, statusCode: StatusCodes.Status503ServiceUnavailable));
        try
        {
            await using var app = builder.Build();
            app.UseMiddleware<CorrelationIdProviderMiddleware>();
            app.UseResponseProblemDetails();
            app.MapControllers();
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add(CorrelationId.HeaderName, "inventory-request");

            using var response = await client.GetAsync(ProblemDetailsScenario.ApiPrefix + "/mvc/result/server");

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("reason").GetString().ShouldBe("Inventory unavailable.");
            var resultLogs = logs.Messages.Where(message => message.Contains("result - HTTP 503 failure", StringComparison.Ordinal)).ToArray();
            resultLogs.Length.ShouldBe(entries);
            resultLogs.ShouldAllBe(message => message.Contains("correlationId=inventory-request", StringComparison.Ordinal) &&
                message.Contains(nameof(ExceptionError), StringComparison.Ordinal));
        }
        finally
        {
            ResultMapHttpExtensions.RemoveErrorHandler<ExceptionError>();
        }
    }

    [Fact]
    public async Task Get_UnsupportedAccept_LogsSelectedResultStatusOnceAndSkipsHooks()
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        var hookCalls = 0;
        builder.Services.AddResponseProblemDetails(options =>
        {
            options.IncludeExceptionDetails = true;
            options.OnBeforeWriteDetails = (_, _) => hookCalls++;
        });
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/result", () => Result.Failure(new ExceptionError(new InvalidOperationException("fixture"))).MapHttpNoContent());
        app.MapGet("/throw", HttpResult () => throw new InvalidOperationException("fixture"));
        app.MapGet("/expected", () => Result.Failure(new ConflictError()).MapHttpNoContent());
        await app.StartAsync();

        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Accept", "application/xml");
        using var result = await client.GetAsync("/result");
        using var resultBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(result, HttpStatusCode.InternalServerError);
        resultBody.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
        using var thrown = await client.GetAsync("/throw");
        using var thrownBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(thrown, HttpStatusCode.InternalServerError);
        thrownBody.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        using var expected = await client.GetAsync("/expected");
        expected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        hookCalls.ShouldBe(0);
        logs.Messages.Count(message => message.Contains("result - HTTP 500 failure", StringComparison.Ordinal)).ShouldBe(1);
        logs.Messages.Count(message => message.Contains("Unhandled exception mapped to HTTP 500", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_HttpResultLoggingDisabledOrResultOnly_SkipsResponseLog(bool resultOnly)
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        if (resultOnly)
        {
            builder.Services.AddResultHttpProblemDetails();
        }
        else
        {
            builder.Services.AddResponseProblemDetails(options => options.ShouldLogResultErrors = false);
        }

        await using var app = builder.Build();
        app.MapGet("/result", () => Result.Failure(new TimeoutError()).MapHttpNoContent());
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/result");

        using var problem = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.GatewayTimeout);
        logs.Messages.ShouldNotContain(message => message.Contains("result - HTTP 504 failure", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_TypedServerErrorArm_UsesOnlyExplicitMapperLogging(bool useMapperLogger)
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        builder.Services.AddResponseProblemDetails();
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/typed", (ILoggerFactory loggers) =>
            Result.Failure(new ExceptionError(new InvalidOperationException("fictional failure")))
                .MapHttp<NoContent, NotFound, UnauthorizedHttpResult, BadRequest, InternalServerError>(
                    () => TypedResults.NoContent(), useMapperLogger ? loggers.CreateLogger("FixtureMapper") : null));
        app.MapGet("/problem", () => Result.Failure(new ExceptionError(new InvalidOperationException("fictional failure")))
            .MapHttpNoContent());
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var typed = await client.GetAsync("/typed");
        using var problem = await client.GetAsync("/problem");

        typed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await typed.Content.ReadAsStringAsync()).ShouldBeEmpty();
        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(problem, HttpStatusCode.InternalServerError);
        logs.Messages.Count(message => message.Contains("result - HTTP 500 failure", StringComparison.Ordinal)).ShouldBe(1);
        logs.Messages.Count(message => message.Contains("result - exception error occurred", StringComparison.Ordinal))
            .ShouldBe(useMapperLogger ? 1 : 0);
    }

    [Theory]
    [InlineData(true, false, false, 1, 0, 0)]
    [InlineData(false, true, true, 0, 0, 1)]
    [InlineData(null, true, false, 0, 1, 0)]
    [InlineData(null, false, false, 0, 0, 0)]
    public async Task Get_IndividualDevKitHandler_UsesTheRegistrationLoggingPolicy(
        bool? combinedLogging, bool legacyLogging, bool nativeDiagnostics, int combinedLogs, int legacyLogs, int nativeLogs)
    {
        var builder = CreateBuilder();
        var logs = new CaptureLogger();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(new GlobalExceptionHandlerOptions { EnableLogging = legacyLogging });
        builder.Services.AddExceptionHandler<HttpRequestExceptionHandler>();
        if (combinedLogging is { } enabled)
        {
            builder.Services.AddResponseProblemDetails(options => options.ShouldLogUnhandledException = enabled);
        }
        else
        {
            builder.Services.AddProblemDetails();
        }

        if (nativeDiagnostics)
        {
            builder.Services.Configure<ExceptionHandlerOptions>(options => options.SuppressDiagnosticsCallback = _ => false);
        }

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/upstream", HttpResult () => throw new HttpRequestException("Fictional upstream failure."));
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/upstream");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        _ = await response.Content.ReadAsStringAsync();
        logs.Messages.Count(message => message.Contains("Unhandled exception mapped to HTTP 503", StringComparison.Ordinal)).ShouldBe(combinedLogs);
        logs.Messages.Count(message => message.Contains("HttpRequestException occurred", StringComparison.Ordinal)).ShouldBe(legacyLogs);
        logs.Messages.Count(message => message.Contains("An unhandled exception has occurred", StringComparison.Ordinal)).ShouldBe(nativeLogs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_OptInEfHandlers_UsesNativeOrderAndSharedPreparation(bool disclose)
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton(new GlobalExceptionHandlerOptions());
        builder.Services.AddExceptionHandler<DbUpdateConcurrencyExceptionHandler>();
        builder.Services.AddExceptionHandler<DbUpdateExceptionHandler>();
        builder.Services.AddExceptionHandler<DbExceptionHandler>();
        builder.Services.AddResponseProblemDetails(options => options.IncludeExceptionDetails = disclose);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/", HttpResult () => throw new DbUpdateConcurrencyException("fictional update conflict"));
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");
        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.Conflict);
        body.RootElement.GetProperty("title").GetString().ShouldBe("Concurrency Conflict");
        body.RootElement.GetProperty("detail").GetString().ShouldNotContain("fictional update conflict");
        body.RootElement.TryGetProperty("affectedEntities", out _).ShouldBeFalse();
        body.RootElement.TryGetProperty("exceptionDetails", out var diagnostics).ShouldBe(disclose);
        if (disclose)
        {
            diagnostics[0].GetProperty("type").GetString().ShouldBe(typeof(DbUpdateConcurrencyException).FullName);
        }
    }

    [Theory]
    [InlineData(false, "application/json", false)]
    [InlineData(true, "application/json", true)]
    [InlineData(true, "application/xml", false)]
    public async Task Get_IndividualDevKitHandler_UsesOnlyTheCombinedDisclosurePolicy(bool disclose, string accept, bool expectDiagnostics)
    {
        const string Diagnostic = "Fictional upstream diagnostic.";
        var builder = CreateBuilder();
        builder.Services.AddSingleton(new GlobalExceptionHandlerOptions());
        builder.Services.AddExceptionHandler<HttpRequestExceptionHandler>();
        builder.Services.AddResponseProblemDetails(options => options.IncludeExceptionDetails = disclose);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/upstream", HttpResult () => throw new HttpRequestException(Diagnostic));
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Accept", accept);

        using var response = await client.GetAsync("/upstream");

        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        body.RootElement.GetProperty("title").GetString().ShouldBe("Service Unavailable");
        body.RootElement.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        body.RootElement.TryGetProperty("exceptionDetails", out var diagnostics).ShouldBe(expectDiagnostics);
        if (expectDiagnostics)
        {
            diagnostics[0].GetProperty("message").GetString().ShouldBe(Diagnostic);
        }
    }

    [Theory]
    [InlineData("rule", 400, "Bad Request", "The request could not be completed.")]
    [InlineData("policy", 400, "Bad Request", "The request could not be completed.")]
    [InlineData("security", 401, "Unauthorized", "Access is not authorized.")]
    [InlineData("aggregate", 404, "Aggregate Not Found", "The requested resource was not found.")]
    [InlineData("entity", 404, "Entity Not Found", "The requested resource was not found.")]
    [InlineData("module", 503, "Module Not Enabled", "The requested feature is unavailable.")]
    [InlineData("unknown", 500, "Internal Server Error", "An unexpected error occurred.")]
    public async Task Get_BuiltInExceptionMapping_UsesFixedPublicFields(string kind, int status, string title, string detail)
    {
        var builder = CreateBuilder();
        builder.Services.AddResponseProblemDetails(options => options.IncludeExceptionDetails = true);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/{kind}", HttpResult (string kind) => throw kind switch
        {
            "rule" => new RuleException("fictional failure"),
            "policy" => new DomainPolicyException("fictional failure"),
            "security" => new System.Security.SecurityException("fictional failure"),
            "aggregate" => new AggregateNotFoundException("fictional failure"),
            "entity" => new EntityNotFoundException("fictional failure"),
            "module" => new ModuleNotEnabledException("fictional failure"),
            _ => new InvalidOperationException("fictional failure")
        });
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/" + kind);
        using var body = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, (HttpStatusCode)status);
        body.RootElement.GetProperty("title").GetString().ShouldBe(title);
        body.RootElement.GetProperty("detail").GetString().ShouldBe(detail);
        body.RootElement.GetProperty("type").GetString().ShouldBe($"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{status}");
        body.RootElement.TryGetProperty("instance", out _).ShouldBeFalse();
        body.RootElement.GetProperty("exceptionDetails")[0].GetProperty("message").GetString().ShouldContain("fictional failure");
    }

    [Fact]
    public async Task Get_InvalidMappingFactory_PropagatesTheFailure()
    {
        var builder = CreateBuilder();
        builder.Services.AddResponseProblemDetails(options => options.Map<ArgumentException>(_ => null));
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/", HttpResult () => throw new ArgumentException("fixture"));
        await app.StartAsync();

        using var client = app.GetTestClient();
        await Should.ThrowAsync<ArgumentException>(async () => await client.GetAsync("/"));
    }

    [Fact]
    public async Task Get_StartedResponse_PropagatesWithoutWritingASecondProblem()
    {
        var builder = CreateBuilder();
        var writes = 0;
        builder.Services.AddResponseProblemDetails(options => options.OnBeforeWriteDetails = (_, _) => writes++);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/", async (HttpContext context) =>
        {
            await context.Response.StartAsync();
            await context.Response.WriteAsync("partial");
            throw new InvalidOperationException("fixture");
        });
        await app.StartAsync();

        using var client = app.GetTestClient();
        await Should.ThrowAsync<Exception>(async () => await client.GetAsync("/"));
        writes.ShouldBe(0);
    }

    [Fact]
    public async Task Get_FailingEnrichment_DoesNotAttemptASecondProblem()
    {
        var builder = CreateBuilder();
        var calls = 0;
        builder.Services.AddResponseProblemDetails(options => options.OnBeforeWriteDetails = (_, _) =>
        {
            calls++;
            throw new InvalidOperationException("fixture");
        });
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapGet("/", () => Result.Failure(new ExceptionError(new ArgumentException("fixture"))).MapHttpNoContent());
        await app.StartAsync();

        using var client = app.GetTestClient();
        var failure = await Record.ExceptionAsync(async () =>
        {
            using var response = await client.GetAsync("/");
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        });
        if (failure is not null)
        {
            failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("fixture");
        }

        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_FailureBeforeWriting_DoesNotReachTheExceptionMappers(bool mvc)
    {
        var builder = CreateBuilder();
        var calls = 0;
        void Fail()
        {
            calls++;
            throw new DbUpdateConcurrencyException("fixture");
        }

        builder.Services.AddControllers().AddApplicationPart(typeof(ProblemDetailsController).Assembly);
        builder.Services.AddSingleton(new GlobalExceptionHandlerOptions());
        builder.Services.AddExceptionHandler<DbUpdateConcurrencyExceptionHandler>();
        if (!mvc)
        {
            builder.Services.AddSingleton<IProblemDetailsWriter>(new FailingSelectionWriter(Fail));
        }

        builder.Services.AddResponseProblemDetails(options =>
        {
            if (mvc)
            {
                options.OnBeforeWriteDetails = (_, _) => Fail();
            }
        });
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapControllers();
        app.MapGet("/problem", () => TypedResults.Problem(statusCode: 409));
        await app.StartAsync();
        using var client = app.GetTestClient();

        var failure = await Record.ExceptionAsync(async () =>
        {
            using var response = mvc
                ? await client.PostAsJsonAsync(ProblemDetailsScenario.ApiPrefix + "/mvc/model-state", new { })
                : await client.GetAsync("/problem");
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
        });

        if (failure is not null)
        {
            failure.ShouldBeOfType<DbUpdateConcurrencyException>().Message.ShouldBe("fixture");
        }

        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public async Task Get_NestedExceptionBoundaries_PreservesHtmlAndDevelopmentOnlyDiagnostics(string environment, bool include)
    {
        var builder = CreateBuilder(environment);
        builder.Services.AddResponseProblemDetails(options =>
            options.IncludeExceptionDetails = builder.Environment.IsDevelopment());
        await using var app = builder.Build();
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/before-boundary")
            {
                throw new InvalidOperationException("Fictional startup failure.");
            }

            await next(context);
        });
        app.UseResponseProblemDetails();
        app.UseRouting();
        app.MapGet("/Error", () => TypedResults.Content("<h1>Fictional error page</h1>", "text/html", statusCode: 500));
        app.MapGet("/throw", HttpResult () => throw new InvalidOperationException("fixture"));
        app.MapGet("/result", () => Result.Failure(new ExceptionError(new InvalidOperationException("fixture"))).MapHttpNoContent());
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var html = await client.GetAsync("/before-boundary");
        html.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        html.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        (await html.Content.ReadAsStringAsync()).ShouldContain("Fictional error page");
        foreach (var path in new[] { "/throw", "/result" })
        {
            using var response = await client.GetAsync(path);
            using var problem = await PresentationProblemDetailsHostTests.ReadProblemAsync(response, HttpStatusCode.InternalServerError);
            problem.RootElement.TryGetProperty("exceptionDetails", out _).ShouldBe(include);
        }
    }

    [Fact]
    public async Task Send_KestrelHeadAndCancellation_RespectsNativeTransportBehavior()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddResponseProblemDetails(options => options.ShouldLogUnhandledException = false);
        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.MapMethods("/head", ["HEAD"], HttpResult () => throw new InvalidOperationException("fixture"));
        app.MapGet("/canceled", HttpResult (HttpContext context) =>
        {
            context.RequestAborted = new CancellationToken(canceled: true);
            throw new OperationCanceledException(context.RequestAborted);
        });
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Head, "/head");
        using var head = await client.SendAsync(request);
        head.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        head.Content.Headers.ContentType.MediaType.ShouldBe("application/problem+json");
        (await head.Content.ReadAsStringAsync()).ShouldBeEmpty();
        using var canceled = await client.GetAsync("/canceled");
        ((int)canceled.StatusCode).ShouldBe(499);
        (await canceled.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    /// <summary>Verifies a public HTML response, empty statuses, challenges, and exceptions over HTTPS with HTTP/2.</summary>
    /// <example><code>await new ResponseProblemDetailsWireTests().Get_KestrelHttpsHttp2_UsesMiddlewareResponseHeaders();</code></example>
    [Fact]
    public async Task Get_KestrelHttpsHttp2_UsesMiddlewareResponseHeaders()
    {
        const string origin = "https://inventory.example";
        const string versionHeader = "X-Inventory-Version";
        const string indexHtml = "<!doctype html><title>Inventory</title><p>Inventory is available.</p>";
        var finishIndexResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, endpoint =>
        {
            endpoint.Protocols = HttpProtocols.Http2;
            endpoint.UseHttps(certificate);
        }));
        builder.Services.AddResponseProblemDetails(options => options.ShouldLogUnhandledException = false);
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origin, "https://warehouse.example")
            .AllowCredentials()
            .WithExposedHeaders(versionHeader)));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization();

        await using var app = builder.Build();
        app.UseResponseProblemDetails();
        app.UseRouting();
        app.UseCors();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["FlowId"] = "inventory-flow";
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[versionHeader] = "42";
                return Task.CompletedTask;
            });
            await next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/index.html", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(indexHtml, context.RequestAborted);
            // Kestrel must send the headers while the request pipeline is still active.
            await finishIndexResponse.Task.WaitAsync(context.RequestAborted);
        });
        app.MapGet("/api/inventory/missing", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "private, max-age=60";
            context.Response.Headers["X-Inventory-State"] = "missing";
            return TypedResults.NotFound();
        });
        app.MapGet("/api/inventory/private", () => TypedResults.Ok()).RequireAuthorization();
        app.MapGet("/api/inventory/failure", HttpResult () => throw new InvalidOperationException("Inventory failure."));
        await app.StartAsync();

        using var sut = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, remoteCertificate, _, _) => certificate.Equals(remoteCertificate)
        })
        {
            BaseAddress = new Uri(app.Urls.Single()),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        sut.DefaultRequestHeaders.Add(HeaderNames.Origin, origin);

        using var indexTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        HttpResponseMessage indexResponse;
        try
        {
            indexResponse = await sut.GetAsync("/index.html", HttpCompletionOption.ResponseHeadersRead, indexTimeout.Token);
        }
        finally
        {
            finishIndexResponse.TrySetResult();
        }

        using var success = indexResponse;
        using var missing = await sut.GetAsync("/api/inventory/missing");
        using var challenge = await sut.GetAsync("/api/inventory/private");
        using var failure = await sut.GetAsync("/api/inventory/failure");

        foreach (var response in new[] { success, missing, challenge, failure })
        {
            response.Version.ShouldBe(HttpVersion.Version20);
            response.Headers.GetValues(HeaderNames.AccessControlAllowOrigin).ShouldBe([origin]);
            response.Headers.GetValues(HeaderNames.AccessControlAllowCredentials).ShouldBe(["true"]);
            response.Headers.GetValues(HeaderNames.AccessControlExposeHeaders).ShouldBe([versionHeader]);
            response.Headers.Vary.ShouldContain(HeaderNames.Origin);
            response.Headers.GetValues(versionHeader).ShouldBe(["42"]);
        }

        success.StatusCode.ShouldBe(HttpStatusCode.OK);
        success.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        (await success.Content.ReadAsStringAsync()).ShouldBe(indexHtml);
        success.Headers.GetValues("FlowId").ShouldBe(["inventory-flow"]);

        using var missingBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(missing, HttpStatusCode.NotFound);
        missing.Headers.GetValues("FlowId").ShouldBe(["inventory-flow"]);
        missing.Headers.GetValues("X-Inventory-State").ShouldBe(["missing"]);
        missing.Headers.CacheControl.Private.ShouldBeTrue();
        missing.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromMinutes(1));
        missing.Headers.CacheControl.NoStore.ShouldBeFalse();

        using var challengeBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(challenge, HttpStatusCode.Unauthorized);
        challenge.Headers.WwwAuthenticate.Single().Scheme.ShouldBe(JwtBearerDefaults.AuthenticationScheme);
        challenge.Headers.GetValues("FlowId").ShouldBe(["inventory-flow"]);
        challenge.Headers.CacheControl.ShouldBeNull();

        using var failureBody = await PresentationProblemDetailsHostTests.ReadProblemAsync(failure, HttpStatusCode.InternalServerError);
        failure.Headers.Contains("FlowId").ShouldBeFalse();
        failure.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    private static WebApplicationBuilder CreateBuilder(string environment = "Production")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        return builder;
    }

    private sealed class FixtureExceptionHandler(IProblemDetailsService service) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is not FormatException)
            {
                return false;
            }

            context.Response.StatusCode = 409;
            await service.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = new Problem { Status = 409 }
            });
            return true;
        }
    }

    private sealed class InventoryProblem(string inventoryCode)
        : Microsoft.AspNetCore.Mvc.ValidationProblemDetails(new Dictionary<string, string[]> { ["sku"] = ["Supply a valid SKU."] })
    {
        public string InventoryCode { get; } = inventoryCode;
    }

    private sealed class FailingSelectionWriter(Action fail) : IProblemDetailsWriter
    {
        public bool CanWrite(ProblemDetailsContext context)
        {
            fail();
            return false;
        }

        public ValueTask WriteAsync(ProblemDetailsContext context) => throw new InvalidOperationException("The writer must never be selected.");
    }

    private sealed class FixtureWriter(IOptions<Microsoft.AspNetCore.Http.ProblemDetailsOptions> options, bool callsNative) : IProblemDetailsWriter
    {
        public bool CanWrite(ProblemDetailsContext context) => true;

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            if (callsNative)
            {
                options.Value.CustomizeProblemDetails?.Invoke(context);
            }

            return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(context.ProblemDetails,
                options: null, "application/vnd.fixture+json"));
        }
    }

    private sealed class CaptureLogger : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => this;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (this.IsEnabled(logLevel))
            {
                this.Messages.Enqueue(formatter(state, exception));
            }
        }

        public void Dispose()
        {
        }
    }
}
