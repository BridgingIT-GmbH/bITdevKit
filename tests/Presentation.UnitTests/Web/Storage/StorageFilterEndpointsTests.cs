// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Text;
using System.Text.Json;
using Application.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Presentation.Web;
using Presentation.Web.Storage;

public sealed class StorageFilterEndpointsTests
{
    private const string Blobs = "/_bdk/api/storage/blobs/reports";
    private const string Documents = "/_bdk/api/storage/documents/invoices";

    [Fact]
    public async Task Filter_MaintenanceAndReadGroupsSharePrefixes_DispatchesToTheCorrectOverloads()
    {
        var blobs = Substitute.For<IBlobStoreClient>();
        var documents = Substitute.For<IDocumentStoreClientAccessor>();
        var storage = Substitute.For<IBlobStoreClientFactory>();
        var documentStorage = Substitute.For<IDocumentStoreClientFactory>();
        storage.CreateClient("reports").Returns(blobs);
        documentStorage.Create("invoices").Returns(documents);

        blobs.ListPageAsync(Arg.Any<BlobFilterScope>(), Arg.Any<FilterModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<BlobPage>.Success(new()
            {
                Items = [new BlobInfo { Key = new("archive", "issued/1"), Dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["Year"] = 2026 }).Value }],
                ContinuationToken = "next"
            }));
        documents.ListPageAsync(Arg.Any<DocumentFilterScope>(), Arg.Any<FilterModel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<DocumentInfoPage>.Success(new() { Items = [new DocumentInfo { Key = new("sales", "issued/1") }] }));
        blobs.ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>()).Returns(Result<BlobPage>.Success(new()));
        documents.FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>()).Returns(Result<DocumentJsonPage>.Success(new()));

        await using var app = await StartAsync(storage, documentStorage);
        using var client = app.GetTestClient();

        var longPosition = new string('漢', 2048);
        var blobToken = StorageFilterQuery.WrapPosition(JsonSerializer.Serialize(new[] { longPosition, new string('a', 64) }),
            BlobFilterQuery.Purpose, "ef", "reports", null, "test-binding", null, BlobFilterQuery.IsValidPosition).Value;
        const string model = """{"filters":[{"field":"Dimensions.Year","operator":"eq","value":{"type":"integer","value":2026}}],"continuationToken":"last"}""";
        using var blob = await PostAsync(client, Blobs + "/search?container=archive", model.Replace("last", blobToken, StringComparison.Ordinal));
        blob.StatusCode.ShouldBe(HttpStatusCode.OK, await blob.Content.ReadAsStringAsync());
        using var blobPage = JsonDocument.Parse(await blob.Content.ReadAsStringAsync());
        blobPage.RootElement.GetProperty("continuationToken").GetString().ShouldBe("next");
        blobPage.RootElement.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        blobPage.RootElement.GetProperty("items")[0].GetProperty("dimensions").GetProperty("Year").GetProperty("value").GetInt32().ShouldBe(2026);
        await blobs.Received(1).ListPageAsync(new BlobFilterScope("archive"), Arg.Is<FilterModel>(filter =>
            filter.Filters.Single().Operator == FilterOperator.Equal && filter.Filters.Single().Value is JsonElement), blobToken, Arg.Any<CancellationToken>());

        using var doc = await PostAsync(client, Documents + "/search?partitionKey=sales", model);
        doc.StatusCode.ShouldBe(HttpStatusCode.OK, await doc.Content.ReadAsStringAsync());
        await documents.Received(1).ListPageAsync(new DocumentFilterScope("sales"), Arg.Any<FilterModel>(), "last", Arg.Any<CancellationToken>());
        using var nativeBlob = await PostAsync(client, Blobs + "/blobs/search", """{"container":"archive"}""");
        using var nativeDocument = await PostAsync(client, Documents + "/documents/search", "{}");
        nativeBlob.StatusCode.ShouldBe(HttpStatusCode.OK);
        nativeDocument.StatusCode.ShouldBe(HttpStatusCode.OK);
        await blobs.Received(1).ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>());
        await documents.Received(1).FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>());

        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = openApi.RootElement.GetProperty("paths");
        foreach (var path in new[] { "/_bdk/api/storage/blobs/{storeName}/search", "/_bdk/api/storage/documents/{clientId}/search" })
        {
            var schema = paths.GetProperty(path).GetProperty("post").GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("properties");
            schema.TryGetProperty("continuationToken", out _).ShouldBeTrue();
            var criterion = schema.GetProperty("filters").GetProperty("items").GetProperty("properties");
            criterion.GetProperty("value").GetProperty("oneOf").GetArrayLength().ShouldBe(2);
            criterion.GetProperty("operator").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ShouldNotContain("contains");
        }
    }

    [Fact]
    public async Task Filter_InvalidTokenOrUnknownClient_ReturnsMaintenanceErrors()
    {
        var blobs = Substitute.For<IBlobStoreClient>();
        var storage = Substitute.For<IBlobStoreClientFactory>();
        storage.CreateClient("reports").Returns(blobs);
        blobs.ListPageAsync(Arg.Any<BlobFilterScope>(), Arg.Any<FilterModel>(), "bad", Arg.Any<CancellationToken>())
            .Returns(Result<BlobPage>.Failure(new BlobStoreInvalidContinuationTokenError("Invalid token.")));
        var documentStorage = Substitute.For<IDocumentStoreClientFactory>();
        documentStorage.Create("invoices").Returns((IDocumentStoreClientAccessor)null);
        await using var app = await StartAsync(storage, documentStorage);
        using var client = app.GetTestClient();

        using var bad = await PostAsync(client, Blobs + "/search?container=archive", """{"continuationToken":"bad"}""");
        using var malformed = await PostAsync(client, Blobs + "/search?container=archive", "{");
        using var missing = await PostAsync(client, Documents + "/search", "{}");
        bad.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Filter_WithReadGroupsOnly_HasNoMaintenanceRoutes()
    {
        await using var app = await StartAsync(Substitute.For<IBlobStoreClientFactory>(), Substitute.For<IDocumentStoreClientFactory>(), maintenance: false);
        using var client = app.GetTestClient();

        using var blob = await PostAsync(client, Blobs + "/search?container=archive", "{}");
        using var doc = await PostAsync(client, Documents + "/search", "{}");
        blob.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        doc.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Filter_ProtectedCustomMaintenanceGroups_RequireAuthentication()
    {
        await using var app = await StartAsync(Substitute.For<IBlobStoreClientFactory>(),
            Substitute.For<IDocumentStoreClientFactory>(), protect: true);
        using var client = app.GetTestClient();

        using var blob = await PostAsync(client, "/ops/blobs/reports/search?container=archive", "{}");
        using var doc = await PostAsync(client, "/ops/documents/invoices/search", "{}");
        blob.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        doc.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        return await client.PostAsync(path, content);
    }

    private static async Task<WebApplication> StartAsync(IBlobStoreClientFactory blobs, IDocumentStoreClientFactory documents,
        bool maintenance = true, bool protect = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddOpenApi();
        var blobStorage = builder.Services.AddBlobStorage().WithInMemoryClient("reports").AddReadEndpoints(options => options.AllowAnonymous());
        var documentStorage = builder.Services.AddDocumentStorage().AddReadEndpoints(options => options.AllowAnonymous());
        if (protect)
        {
            builder.Services.AddAuthentication("test").AddCookie("test", options => options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return Task.CompletedTask;
            });
            builder.Services.AddAuthorization();
            blobStorage.AddMaintenanceEndpoints(options => options.GroupPath("/ops/blobs").RequireAuthorization());
            documentStorage.AddMaintenanceEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization());
        }
        else if (maintenance)
        {
            blobStorage.AddMaintenanceEndpoints(options => options.AllowAnonymous());
            documentStorage.AddMaintenanceEndpoints(options => options.AllowAnonymous());
        }

        builder.Services.AddSingleton(blobs);
        builder.Services.AddSingleton(documents);
        var app = builder.Build();
        app.MapEndpoints();
        app.MapOpenApi();
        await app.StartAsync();
        return app;
    }
}
