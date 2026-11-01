namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Filtering;

using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public class FilterSchemaTests
{
    [Fact]
    public async Task Search_WithManualBodyReader_GeneratesRequestSchema()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi();
        await using var app = builder.Build();
        app.MapPost("/search", (HttpContext context) => Results.Ok())
            .WithFilterSchema(isRequestBody: true);
        app.MapOpenApi();
        await app.StartAsync();

        using var response = await app.GetTestClient().GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.GetProperty("paths").GetProperty("/search")
            .GetProperty("post").GetProperty("requestBody");
        body.GetProperty("required").GetBoolean().ShouldBeTrue();
        body.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("properties").GetProperty("filters").GetProperty("type").GetString().ShouldBe("array");
    }
}
