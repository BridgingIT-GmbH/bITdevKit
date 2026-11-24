namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using global::Presentation.TestHost;

public sealed class PresentationProblemDetailsHostTests
{
    private const string Prefix = "/api/problem-details";

    [Fact]
    public async Task Get_DisabledProfile_KeepsExistingHtmlRoutes()
    {
        await using var fixture = CreateHost("Disabled");
        using var client = fixture.CreateClient();
        foreach (var path in new[] { "/", "/lifecycle" })
        {
            using var response = await client.GetAsync(path);
            response.IsSuccessStatusCode.ShouldBeTrue();
            response.Content.Headers.ContentType.MediaType.ShouldBe("text/html");
        }

        using var absent = await client.GetAsync(Prefix + "/throw/server");
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var openApi = await client.GetAsync("/openapi/problem-details.json");
        openApi.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("Api", false)]
    [InlineData("Api", true)]
    [InlineData("Mixed", true)]
    public async Task Get_ValidationAndServerFailures_ShareTheContract(string profile, bool disclose)
    {
        await using var fixture = CreateHost(profile, disclose);
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("CorrelationId", "inventory-test");
        foreach (var path in new[] { "throw/validation", "result/validation", "mvc/throw/validation", "mvc/result/validation" })
        {
            using var response = await client.GetAsync($"{Prefix}/{path}");
            using var body = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
            body.RootElement.GetProperty("detail").GetString().ShouldBe("One or more validation errors occurred.");
            body.RootElement.GetProperty("data").GetProperty("errors").GetProperty("Name")[0].GetString().ShouldBe("Name is required.");
            body.RootElement.GetProperty("correlationId").GetString().ShouldBe("inventory-test");
        }

        foreach (var path in new[] { "throw/server", "result/server", "mvc/throw/server", "mvc/result/server" })
        {
            using var response = await client.GetAsync($"{Prefix}/{path}");
            using var problem = await ReadProblemAsync(response, HttpStatusCode.InternalServerError);
            var root = problem.RootElement;
            root.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
            root.GetProperty("correlationId").GetString().ShouldBe("inventory-test");
            root.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
            root.TryGetProperty("data", out _).ShouldBeFalse();
            root.TryGetProperty("exceptionDetails", out var diagnostics).ShouldBe(disclose);
            if (disclose)
            {
                diagnostics[0].GetProperty("type").GetString().ShouldBe(typeof(InvalidOperationException).FullName);
                diagnostics[0].GetProperty("innerExceptions")[0].GetProperty("message").GetString().ShouldBe("Fictional inner failure.");
            }

            if (path == "throw/server")
            {
                response.Headers.GetValues("X-Inventory-Version").ShouldBe(["42"]);
                response.Headers.Contains("X-Discard").ShouldBeFalse();
                response.Headers.CacheControl.NoStore.ShouldBeTrue();
            }
        }

        using var notSupported = await client.GetAsync(Prefix + "/throw/not-supported");
        using var mapped = await client.GetAsync(Prefix + "/throw/mapped");
        notSupported.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        using var mappedProblem = await ReadProblemAsync(mapped, HttpStatusCode.Conflict);
        mappedProblem.RootElement.GetProperty("type").GetString().ShouldBe("https://inventory.example/problems/item-conflict");
    }

    [Theory]
    [InlineData("not-implemented", HttpStatusCode.NotImplemented, "Not Implemented", false)]
    [InlineData("not-implemented", HttpStatusCode.NotImplemented, "Not Implemented", true)]
    [InlineData("upstream", HttpStatusCode.ServiceUnavailable, "Service Unavailable", false)]
    [InlineData("upstream", HttpStatusCode.ServiceUnavailable, "Service Unavailable", true)]
    public async Task Get_BuiltInServerException_UsesMappedStatusAndConfiguredDiagnostics(
        string route, HttpStatusCode expectedStatus, string expectedTitle, bool disclose)
    {
        await using var fixture = CreateHost("ApiDefaults", disclose);
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync($"{Prefix}/throw/{route}");

        using var problem = await ReadProblemAsync(response, expectedStatus);
        problem.RootElement.GetProperty("title").GetString().ShouldBe(expectedTitle);
        problem.RootElement.GetProperty("type").GetString().ShouldBe($"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{(int)expectedStatus}");
        problem.RootElement.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        problem.RootElement.TryGetProperty("exceptionDetails", out var diagnostics).ShouldBe(disclose);
        if (disclose)
        {
            diagnostics[0].GetProperty("message").GetString().ShouldBe("Fictional inventory failure.");
        }
    }

    [Fact]
    public async Task Get_MixedProfile_RoutesByOriginalPathAcrossNegotiationAndReexecution()
    {
        await using var fixture = CreateHost("Mixed", disclose: true);
        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "text/html");
        using var thrown = await client.GetAsync(Prefix + "/throw/server");
        using var absent = await client.GetAsync(Prefix + "/unmatched");
        using var result = await client.GetAsync(Prefix + "/result/server");
        using var thrownBody = await ReadProblemAsync(thrown, HttpStatusCode.InternalServerError);
        using var emptyBody = await ReadProblemAsync(absent, HttpStatusCode.NotFound);
        using var resultBody = await ReadProblemAsync(result, HttpStatusCode.InternalServerError);
        foreach (var body in new[] { thrownBody.RootElement, emptyBody.RootElement, resultBody.RootElement })
        {
            body.TryGetProperty("exceptionDetails", out _).ShouldBeFalse();
        }

        thrownBody.RootElement.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
        emptyBody.RootElement.GetProperty("type").GetString().ShouldBe("https://httpstatuses.io/404");
        resultBody.RootElement.TryGetProperty("traceId", out _).ShouldBeFalse();

        client.DefaultRequestHeaders.Remove("Accept");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        using var uiException = await client.PostAsync("/problem-details/ui/throw", content: null);
        using var uiNotFound = await client.GetAsync("/problem-details/ui/unmatched");
        uiException.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        uiNotFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await uiException.Content.ReadAsStringAsync()).ShouldContain("Inventory error page");
        (await uiNotFound.Content.ReadAsStringAsync()).ShouldContain("Inventory status 404");
        uiNotFound.Content.Headers.ContentType.MediaType.ShouldBe("text/html");

        using var explicitResult = await client.GetAsync("/problem-details/ui/result");
        using var explicitBody = await ReadProblemAsync(explicitResult, HttpStatusCode.InternalServerError);
        explicitBody.RootElement.GetProperty("exceptionDetails").GetArrayLength().ShouldBe(1);

        using var explicitResponse = await client.GetAsync(Prefix + "/explicit");
        (await explicitResponse.Content.ReadAsStringAsync()).ShouldBe("Inventory item unavailable.");
        using var declaredLength = await client.GetAsync(Prefix + "/empty-with-length");
        declaredLength.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await declaredLength.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_StableResponses_MatchCapturedStatusAndWireHeaders()
    {
        var assembly = typeof(PresentationProblemDetailsHostTests).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("legacy-http-responses.json", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resource)!;
        using var baseline = await JsonDocument.ParseAsync(stream);
        var captured = baseline.RootElement.GetProperty("profiles").GetProperty("Staging-False-False-False");
        await using var fixture = CreateHost("Api");
        using var client = fixture.CreateClient();
        foreach (var entry in captured.EnumerateObject().Where(entry => entry.Name != "throw/not-implemented"))
        {
            using var response = await client.GetAsync(Prefix + "/" + entry.Name);
            ((int)response.StatusCode).ShouldBe(entry.Value.GetProperty("status").GetInt32());
            response.Content.Headers.ContentType?.MediaType.ShouldBe(entry.Value.GetProperty("type").GetString());
            if (entry.Value.TryGetProperty("noCache", out var noCache))
            {
                (response.Headers.CacheControl?.NoStore == true).ShouldBe(noCache.GetBoolean());
            }
        }
    }

    /// <summary>Creates the selected Problem Details profile for HTTP tests.</summary>
    public static WebApplicationFactory<TestHostAssemblyMarker> CreateHost(string profile, bool disclose = false) =>
        new WebApplicationFactory<TestHostAssemblyMarker>().WithWebHostBuilder(builder => builder.UseEnvironment("Production")
            .UseSetting("TestScenarios:ProblemDetails:Profile", profile)
            .UseSetting("TestScenarios:ProblemDetails:IncludeExceptionDetails", disclose.ToString()));

    /// <summary>Asserts the response status and media type and returns the parsed problem.</summary>
    public static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.ShouldBe(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetInt32().ShouldBe((int)expected);
        return body;
    }
}
