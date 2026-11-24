namespace BridgingIT.DevKit.Presentation.IntegrationTests;

using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using BridgingIT.DevKit.Application.Auditing;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Common;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using HttpResult = Microsoft.AspNetCore.Http.IResult;

/// <summary>Verifies Problem Details through the referenced sample server's actual entry point.</summary>
public sealed class SampleHostProblemDetailsTests
{
    private static bool IsWeatherFiesta => typeof(Program).Assembly.GetName().Name.Contains("WeatherFiesta", StringComparison.Ordinal);

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public async Task Get_ApiFailures_UseTheHostsDisclosureAndHeaders(string environment, bool disclose)
    {
        await using var host = CreateHost(environment);
        using var client = CreateClient(host);
        foreach (var route in new[] { "throw", "result" })
        {
            using var response = await client.GetAsync(SampleProblemDetailsController.Prefix + "/" + route);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var body = document.RootElement;
            body.GetProperty("status").GetInt32().ShouldBe(500);
            body.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
            body.GetProperty("correlationId").GetString().ShouldBe("sample-host-smoke");
            body.TryGetProperty("exceptionDetails", out var diagnostics).ShouldBe(disclose);
            if (disclose)
            {
                diagnostics[0].GetProperty("message").GetString().ShouldBe(SampleProblemDetailsController.Diagnostic);
            }

            if (IsWeatherFiesta)
            {
                response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("*");
                if (disclose)
                {
                    response.Headers.GetValues("X-Request-Profiling-Id").Single().ShouldNotBeNullOrWhiteSpace();
                }
            }
        }

        using var challenge = await client.GetAsync(SampleProblemDetailsController.Prefix + "/challenge");
        challenge.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        challenge.Headers.WwwAuthenticate.Single().Scheme.ShouldBe("Fixture");
        challenge.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Get_ProductionFailureBeforeDevKitBoundary_UsesTheHostsHtmlErrorPage()
    {
        await using var host = CreateHost("Production");
        using var client = CreateClient(host);

        using var response = await client.GetAsync(SampleProblemDetailsController.Prefix + "/upstream");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("An error occurred");
        html.ShouldNotContain(SampleProblemDetailsController.Diagnostic);
    }

    private static WebApplicationFactory<Program> CreateHost(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("DevAppInstance:Mode", "Disabled");
            builder.ConfigureTestServices(services =>
            {
                // These probes exercise HTTP middleware without starting workers or touching any database.
                services.RemoveAll<IHostedService>();
                // Storage diagnostics resolve these workers by type during Development endpoint registration.
                KeepUnstartedWorker<DocumentRetentionBackgroundService>(services);
                KeepUnstartedWorker<BlobRetentionBackgroundService>(services);
                services.Replace(ServiceDescriptor.Singleton<IAuditingStorageProvider, InMemoryAuditStorageProvider>());
                services.AddControllers().AddApplicationPart(typeof(SampleProblemDetailsController).Assembly);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SampleProblemDetailsAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = SampleProblemDetailsAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, SampleProblemDetailsAuthenticationHandler>(
                    SampleProblemDetailsAuthenticationHandler.SchemeName, _ => { });
            });
        });

    private static void KeepUnstartedWorker<TWorker>(IServiceCollection services)
        where TWorker : class, IHostedService
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TWorker)))
        {
            services.Replace(ServiceDescriptor.Singleton<TWorker, TWorker>());
        }
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Origin", "https://inventory.example");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add(CorrelationId.HeaderName, "sample-host-smoke");
        return client;
    }
}

/// <summary>Supplies fictional API failures to the sample host without storage dependencies.</summary>
[ApiController]
[Route(Prefix)]
public sealed class SampleProblemDetailsController : ControllerBase
{
    /// <summary>The fixture API route prefix.</summary>
    public const string Prefix = "/api/problem-details-smoke";

    /// <summary>The fictional exception message used in the fixture.</summary>
    public const string Diagnostic = "Fictional inventory failure.";

    /// <summary>Throws a failure from within the host's mapped API pipeline.</summary>
    [AllowAnonymous]
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(Diagnostic);

    /// <summary>Returns an exception-bearing Result from within the host's mapped API pipeline.</summary>
    [AllowAnonymous]
    [HttpGet("result")]
    public HttpResult FailedResult() => Result.Failure(new ExceptionError(new InvalidOperationException(Diagnostic))).MapHttpNoContent();

    /// <summary>Exercises the host authorization and challenge pipeline.</summary>
    [Authorize(AuthenticationSchemes = SampleProblemDetailsAuthenticationHandler.SchemeName)]
    [HttpGet("challenge")]
    public IActionResult ChallengeProbe() => this.Ok();
}

/// <summary>Supplies an unauthenticated request and can fail before DevKit's exception boundary.</summary>
public sealed class SampleProblemDetailsAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The fixture authentication scheme.</summary>
    public const string SchemeName = "SampleProblemDetailsFixture";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        this.Request.Path == SampleProblemDetailsController.Prefix + "/upstream"
            ? throw new InvalidOperationException(SampleProblemDetailsController.Diagnostic)
            : Task.FromResult(AuthenticateResult.NoResult());

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.Headers.WWWAuthenticate = "Fixture";
        return base.HandleChallengeAsync(properties);
    }
}
