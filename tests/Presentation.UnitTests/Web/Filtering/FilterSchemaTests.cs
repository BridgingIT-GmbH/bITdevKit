namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Filtering;

using System.Net;
using System.Text.Json;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

public class FilterSchemaTests
{
    [Fact]
    public async Task Search_WithManualBodyReader_GeneratesRequestSchema()
    {
        await using var app = await CreateAppAsync(app =>
            app.MapPost("/search", (HttpContext context) => Results.Ok())
                .WithFilterSchema(isRequestBody: true));

        using var document = await GetDocumentAsync(app);

        var body = GetOperation(document, "/search", "post").GetProperty("requestBody");
        body.GetProperty("required").GetBoolean().ShouldBeTrue();
        body.GetProperty("content").GetProperty("application/json").GetProperty("schema")
            .GetProperty("properties").GetProperty("filters").GetProperty("type").GetString().ShouldBe("array");
    }

    [Fact]
    public async Task Search_WithQueryFilterOnEndpointWithoutParameters_AddsFilterQueryParameter()
    {
        // Arrange: no other parameters, so the operation's parameter list starts out null.
        await using var app = await CreateAppAsync(app =>
            app.MapGet("/search", (HttpContext context) => Results.Ok())
                .WithFilterSchema());

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var parameters = GetParameters(GetOperation(document, "/search", "get"));
        parameters.Count.ShouldBe(1);
        var filter = parameters.Single();
        filter.GetProperty("name").GetString().ShouldBe("filter");
        filter.GetProperty("in").GetString().ShouldBe("query");
        (filter.TryGetProperty("required", out var required) && required.GetBoolean()).ShouldBeFalse();
        filter.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
        filter.GetProperty("schema").GetProperty("description").GetString().ShouldBe("URL-encoded JSON FilterModel");
    }

    [Fact]
    public async Task Search_WithQueryFilterOnEndpointWithParameters_PreservesExistingParameters()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapGet("/tenants/{tenantId:int}/search", (int tenantId, [FromQuery] string culture) => Results.Ok())
                .WithFilterSchema());

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var parameters = GetParameters(GetOperation(document, "/tenants/{tenantId}/search", "get"));
        parameters.Select(p => $"{p.GetProperty("in").GetString()}:{p.GetProperty("name").GetString()}")
            .ShouldBe(["path:tenantId", "query:culture", "query:filter"], ignoreOrder: true);
    }

    [Fact]
    public async Task Search_WithQueryFilterAndBoundFilterParameter_DoesNotDuplicateFilterParameter()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapGet("/search", ([FromQuery] string filter) => Results.Ok())
                .WithFilterSchema());

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var filters = GetParameters(GetOperation(document, "/search", "get"))
            .Where(p => p.GetProperty("name").GetString() == "filter" && p.GetProperty("in").GetString() == "query").ToList();
        filters.Count.ShouldBe(1);
        filters[0].GetProperty("schema").GetProperty("description").GetString().ShouldBe("URL-encoded JSON FilterModel");
    }

    [Fact]
    public async Task Search_WithQueryFilterSchemaAppliedTwice_DoesNotDuplicateFilterParameter()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapGet("/search", (HttpContext context) => Results.Ok())
                .WithFilterSchema()
                .WithFilterSchema());

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        GetParameters(GetOperation(document, "/search", "get")).Count(p => p.GetProperty("name").GetString() == "filter").ShouldBe(1);
    }

    [Fact]
    public async Task Search_WithRequestBodyOnEndpointWithBoundBody_ReplacesBodySchema()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapPost("/search", ([FromBody] SearchRequest request) => Results.Ok())
                .WithFilterSchema(isRequestBody: true));

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var operation = GetOperation(document, "/search", "post");
        var content = operation.GetProperty("requestBody").GetProperty("content");
        content.EnumerateObject().Select(p => p.Name).ShouldBe(["application/json"]);
        var schema = content.GetProperty("application/json").GetProperty("schema");
        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("description").GetString().ShouldBe("FilterModel");
        schema.GetProperty("properties").TryGetProperty("term", out _).ShouldBeFalse();
        GetParameters(operation).ShouldBeEmpty();
    }

    [Fact]
    public async Task Search_WithRequestBody_DescribesFilterModelProperties()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapPost("/search", (HttpContext context) => Results.Ok())
                .WithFilterSchema(isRequestBody: true));

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var properties = GetOperation(document, "/search", "post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("properties");
        properties.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["page", "pageSize", "orderings", "filters", "includes", "hierarchy", "hierarchyMaxDepth"], ignoreOrder: true);
        properties.GetProperty("page").GetProperty("default").GetInt32().ShouldBe(0);
        properties.GetProperty("pageSize").GetProperty("default").GetInt32().ShouldBe(0);
        properties.GetProperty("hierarchyMaxDepth").GetProperty("default").GetInt32().ShouldBe(5);
        properties.GetProperty("includes").GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
        properties.GetProperty("hierarchy").GetProperty("type").EnumerateArray().Select(e => e.GetString())
            .ShouldBe(["null", "string"], ignoreOrder: true);
        GetEnum(properties.GetProperty("orderings").GetProperty("items").GetProperty("properties").GetProperty("direction"))
            .ShouldBe(["asc", "desc"]);
    }

    [Fact]
    public async Task Search_WithRequestBody_DescribesFilterCriteriaWithEnumMemberValues()
    {
        // Arrange
        await using var app = await CreateAppAsync(app =>
            app.MapPost("/search", (HttpContext context) => Results.Ok())
                .WithFilterSchema(isRequestBody: true));

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        var criteria = GetOperation(document, "/search", "post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("properties").GetProperty("filters")
            .GetProperty("items").GetProperty("properties");
        GetEnum(criteria.GetProperty("operator")).ShouldBe(Common.EnumExtensions.GetEnumMemberValues<FilterOperator>());
        GetEnum(criteria.GetProperty("operator")).ShouldContain("eq");
        GetEnum(criteria.GetProperty("logic")).ShouldBe(["and", "or"]);
        GetEnum(criteria.GetProperty("customType")).ShouldBe(Common.EnumExtensions.GetEnumMemberValues<FilterCustomType>());
        GetEnum(criteria.GetProperty("customType")).ShouldContain("fulltextsearch");
        criteria.GetProperty("customParameters").GetProperty("additionalProperties").GetProperty("type").GetString().ShouldBe("object");
        criteria.GetProperty("specificationArguments").GetProperty("type").GetString().ShouldBe("array");

        var nodes = criteria.GetProperty("compositeSpecification").GetProperty("properties").GetProperty("nodes");
        nodes.GetProperty("type").GetString().ShouldBe("array");
        var oneOf = nodes.GetProperty("items").GetProperty("oneOf").EnumerateArray().ToList();
        oneOf.Count.ShouldBe(2);
        oneOf[0].GetProperty("properties").TryGetProperty("name", out _).ShouldBeTrue();
        GetEnum(oneOf[1].GetProperty("properties").GetProperty("logic")).ShouldBe(["and", "or"]);
    }

    [Fact]
    public async Task Search_WithMultipleDocumentsAndRepeatedRequests_GeneratesSchemaConsistently()
    {
        // Arrange: the transformer reuses the same schema instances for every document generation.
        await using var app = await CreateAppAsync(
            app =>
            {
                app.MapGet("/search", (HttpContext context) => Results.Ok()).WithFilterSchema();
                app.MapPost("/search", (HttpContext context) => Results.Ok()).WithFilterSchema(isRequestBody: true);
            },
            services =>
            {
                services.AddOpenApi("v1");
                services.AddOpenApi("v2");
            });

        foreach (var documentName in new[] { "v1", "v2", "v1" })
        {
            // Act
            using var document = await GetDocumentAsync(app, documentName);

            // Assert
            GetParameters(GetOperation(document, "/search", "get")).Count(p => p.GetProperty("name").GetString() == "filter").ShouldBe(1);
            GetOperation(document, "/search", "post").GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("properties")
                .GetProperty("filters").GetProperty("items").GetProperty("properties").TryGetProperty("operator", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Search_WithOpenApi30Document_SerializesNullableHierarchy()
    {
        // Arrange
        await using var app = await CreateAppAsync(
            app => app.MapPost("/search", (HttpContext context) => Results.Ok()).WithFilterSchema(isRequestBody: true),
            services => services.AddOpenApi(options => options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0));

        // Act
        using var document = await GetDocumentAsync(app);

        // Assert
        document.RootElement.GetProperty("openapi").GetString().ShouldStartWith("3.0");
        var hierarchy = GetOperation(document, "/search", "post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("properties").GetProperty("hierarchy");
        hierarchy.GetProperty("type").GetString().ShouldBe("string");
        hierarchy.GetProperty("nullable").GetBoolean().ShouldBeTrue();
    }

    private static async Task<WebApplication> CreateAppAsync(Action<WebApplication> map, Action<IServiceCollection> configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (configureServices is null)
        {
            builder.Services.AddOpenApi();
        }
        else
        {
            configureServices(builder.Services);
        }

        var app = builder.Build();
        map(app);
        app.MapOpenApi();
        await app.StartAsync();

        return app;
    }

    private static async Task<JsonDocument> GetDocumentAsync(WebApplication app, string documentName = "v1")
    {
        using var response = await app.GetTestClient().GetAsync($"/openapi/{documentName}.json");
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, content);

        return JsonDocument.Parse(content);
    }

    private static JsonElement GetOperation(JsonDocument document, string path, string method) =>
        document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static List<JsonElement> GetParameters(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters) ? [.. parameters.EnumerateArray()] : [];

    private static List<string> GetEnum(JsonElement schema) =>
        [.. schema.GetProperty("enum").EnumerateArray().Select(e => e.GetString())];

    private sealed record SearchRequest(string Term);
}