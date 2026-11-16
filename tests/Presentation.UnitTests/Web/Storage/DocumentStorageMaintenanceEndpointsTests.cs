// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises document maintenance HTTP operations against shared typed in-memory storage.</summary>
public sealed class DocumentStorageMaintenanceEndpointsTests
{
    private const string DefaultPath = "/_bdk/api/storage/documents";

    [Fact]
    public async Task CreateAndRead_AcrossRequestScopes_ReturnsTypedContentAndCommittedMetadata()
    {
        await using var host = await EndpointHost.StartAsync();
        var url = host.Url("one");

        using var created = await SendAsync(host.Client, HttpMethod.Post, url, Envelope("Ada"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        created.Headers.Location.ShouldNotBeNull();
        created.Headers.ETag.ShouldNotBeNull();
        var committed = await JsonAsync(created);
        var version = committed.GetProperty("eTag").GetString();
        version.ShouldNotBeNullOrWhiteSpace();

        using var read = await host.Client.GetAsync(url);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        (await read.Content.ReadFromJsonAsync<TestDocument>()).Name.ShouldBe("Ada");
        read.Headers.ETag.ShouldBe(created.Headers.ETag);

        using var properties = await host.Client.GetAsync(host.Url("one", "/properties"));
        properties.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(properties)).GetProperty("eTag").GetString().ShouldBe(version);
        properties.Headers.ETag.ShouldBe(created.Headers.ETag);
        using var location = await host.Client.GetAsync(created.Headers.Location);
        location.StatusCode.ShouldBe(HttpStatusCode.OK);
        location.Headers.ETag.ShouldBe(created.Headers.ETag);
        using var exists = await host.Client.GetAsync(host.Url("one", "/exists"));
        exists.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await exists.Content.ReadFromJsonAsync<bool>()).ShouldBeTrue();

        using var duplicate = await SendAsync(host.Client, HttpMethod.Post, url, Envelope("Duplicate"));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Ada");
    }

    [Fact]
    public async Task ClientsAndKeys_ReturnSafeRegistrationAndBoundedPages()
    {
        await using var host = await EndpointHost.StartAsync();
        using var first = await SendAsync(host.Client, HttpMethod.Post, host.Url("a"), Envelope("A"));
        using var second = await SendAsync(host.Client, HttpMethod.Post, host.Url("b"), Envelope("B"));
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var clients = await host.Client.GetAsync(DefaultPath + "/clients");
        clients.StatusCode.ShouldBe(HttpStatusCode.OK);
        var registered = (await JsonAsync(clients)).EnumerateArray().Single();
        registered.GetProperty("clientId").GetString().ShouldBe(host.ClientId);
        registered.GetProperty("name").GetString().ShouldBe("archive");
        registered.EnumerateObject().Select(property => property.Name).ShouldBe(
            new[] { "clientId", "name", "documentTypeName", "providerName", "capabilities" }, ignoreOrder: true);

        var keysUrl = host.DocumentsPath + "/keys?partitionKey=records&rowKeyPrefix=&take=1";
        using var pageResponse = await host.Client.GetAsync(keysUrl);
        pageResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await pageResponse.Content.ReadAsStringAsync());
        var page = await pageResponse.Content.ReadFromJsonAsync<DocumentKeyPage>();
        page.Items.Count.ShouldBe(1);
        page.HasMore.ShouldBeTrue();
        using var nextResponse = await host.Client.GetAsync(keysUrl + "&continuationToken=" + Uri.EscapeDataString(page.ContinuationToken));
        nextResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var next = await nextResponse.Content.ReadFromJsonAsync<DocumentKeyPage>();
        next.Items.Count.ShouldBe(1);
        next.Items.Single().ShouldNotBe(page.Items.Single());
        using var unbounded = await host.Client.GetAsync(host.DocumentsPath + "/keys?take=1");
        unbounded.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_WithEchoedThenStaleHttpEtag_PreservesLatestContent()
    {
        await using var host = await EndpointHost.StartAsync();
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Original"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var originalEtag = created.Headers.ETag.ToString();
        originalEtag.ShouldStartWith("\"bdk.");

        using var updated = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Updated"), originalEtag);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        updated.Headers.ETag.ShouldNotBe(created.Headers.ETag);
        using var stale = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Stale"), originalEtag);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Updated");
        using var current = await host.Client.GetAsync(host.Url("one"));
        current.Headers.ETag.ShouldBe(updated.Headers.ETag);

        using var unconditional = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Unconditional"));
        unconditional.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Unconditional");
    }

    [Fact]
    public async Task PatchAndPut_UseWholeItemVersionsAndReturnReusableTokens()
    {
        await using var host = await EndpointHost.StartAsync();
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Original"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var patched = await SendAsync(host.Client, HttpMethod.Patch, host.Url("one", "/properties"),
            "{\"properties\":{\"reviewed\":true}}", created.Headers.ETag.ToString());
        patched.StatusCode.ShouldBe(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        patched.Headers.ETag.ShouldNotBe(created.Headers.ETag);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Original");

        using var staleContent = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Stale"), created.Headers.ETag.ToString());
        staleContent.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var updated = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Updated"), patched.Headers.ETag.ToString());
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var staleProperties = await SendAsync(host.Client, HttpMethod.Patch, host.Url("one", "/properties"),
            "{\"properties\":{\"reviewed\":false}}", patched.Headers.ETag.ToString());
        staleProperties.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var current = await host.Client.GetAsync(host.Url("one", "/properties"));
        current.Headers.ETag.ShouldBe(updated.Headers.ETag);
        (await JsonAsync(current)).GetProperty("properties").GetProperty("reviewed").GetBoolean().ShouldBeTrue();
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Updated");
    }

    [Fact]
    public async Task Put_TwoWritersWithSameReadVersion_CommitsOneAndReturnsWinningEtag()
    {
        await using var host = await EndpointHost.StartAsync();
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Original"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var original = await host.Client.GetAsync(host.Url("one"));
        original.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = original.Headers.ETag.ToString();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var names = new[] { "Writer one", "Writer two" };
        var writers = names.Select(name => Task.Run(async () =>
        {
            await start.Task;
            return await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope(name), etag);
        })).ToArray();

        start.SetResult();
        var responses = await Task.WhenAll(writers);
        using var first = responses[0];
        using var second = responses[1];

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        var winner = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.OK);
        using var current = await host.Client.GetAsync(host.Url("one"));
        current.StatusCode.ShouldBe(HttpStatusCode.OK);
        current.Headers.ETag.ShouldBe(responses[winner].Headers.ETag);
        current.Headers.ETag.ShouldNotBe(original.Headers.ETag);
        (await current.Content.ReadFromJsonAsync<TestDocument>()).Name.ShouldBe(names[winner]);
    }

    [Fact]
    public async Task Clients_SharedProviderAndSameLogicalKey_IsolateRegisteredDocumentTypes()
    {
        await using var host = await EndpointHost.StartAsync(includeSecondType: true);
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("First type"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var alternateUrl = DefaultPath + "/" + Uri.EscapeDataString(host.AlternateClientId) + "/documents?partitionKey=records&rowKey=one";
        using var absent = await host.Client.GetAsync(alternateUrl);
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var alternate = await SendAsync(host.Client, HttpMethod.Post, alternateUrl,
            JsonSerializer.Serialize(new { content = new AlternateDocument { Title = "Second type" } }));
        alternate.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var secondRead = await host.Client.GetAsync(alternateUrl);
        secondRead.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await secondRead.Content.ReadFromJsonAsync<AlternateDocument>()).Title.ShouldBe("Second type");
        using var wrongTypeVersion = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Wrong type"), alternate.Headers.ETag.ToString());
        wrongTypeVersion.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("First type");
    }

    [Fact]
    public async Task Put_ContentExceedsRegisteredLimit_ReturnsPayloadTooLargeWithoutCreation()
    {
        await using var host = await EndpointHost.StartAsync(options: new DocumentStoreOptions { MaxDocumentSize = 64 });

        using var response = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope(new string('x', 512)));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        using var exists = await host.Client.GetAsync(host.Url("one", "/exists"));
        (await exists.Content.ReadFromJsonAsync<bool>()).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_MissingDocumentRepeatedly_ReturnsSuccess()
    {
        await using var host = await EndpointHost.StartAsync();

        using var first = await SendAsync(host.Client, HttpMethod.Delete, host.Url("missing"));
        using var second = await SendAsync(host.Client, HttpMethod.Delete, host.Url("missing"));

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var read = await host.Client.GetAsync(host.Url("missing"));
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithStaleThenCurrentToken_DoesNotPermitConditionalResurrection()
    {
        await using var host = await EndpointHost.StartAsync();
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Original"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var updated = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Updated"), created.Headers.ETag.ToString());
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var staleDelete = await SendAsync(host.Client, HttpMethod.Delete, host.Url("one"), etag: created.Headers.ETag.ToString());
        staleDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Updated");
        using var deleted = await SendAsync(host.Client, HttpMethod.Delete, host.Url("one"), etag: updated.Headers.ETag.ToString());
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var resurrect = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Resurrected"), updated.Headers.ETag.ToString());
        resurrect.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var read = await host.Client.GetAsync(host.Url("one"));
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var exists = await host.Client.GetAsync(host.Url("one", "/exists"));
        (await exists.Content.ReadFromJsonAsync<bool>()).ShouldBeFalse();
    }

    [Fact]
    public async Task PutAndPatch_OmittedMetadataPreservesAndExplicitClearRemovesExpiration()
    {
        await using var host = await EndpointHost.StartAsync();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var json = JsonSerializer.Serialize(new { content = new TestDocument { Name = "Original" }, properties = new { region = "eu" }, expiresAt });
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), json);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var updated = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Updated"));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preserved = await JsonAsync(updated);
        preserved.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(expiresAt);
        preserved.GetProperty("properties").GetProperty("region").GetString().ShouldBe("eu");

        using var cleared = await SendAsync(host.Client, HttpMethod.Patch, host.Url("one", "/properties"),
            "{\"properties\":{},\"clearExpiration\":true}", updated.Headers.ETag.ToString());
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = await JsonAsync(cleared);
        metadata.GetProperty("expiresAt").ValueKind.ShouldBe(JsonValueKind.Null);
        metadata.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
        (await ReadDocumentAsync(host, "one")).Name.ShouldBe("Updated");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"content\":null}")]
    [InlineData("{\"content\":123}")]
    [InlineData("{\"content\":{\"count\":\"invalid\"}}")]
    [InlineData("{\"content\":{\"name\":\"test\"},\"properties\":{\"nested\":{}}}")]
    [InlineData("{\"content\":{\"name\":\"test\"},\"expiresAt\":\"2030-01-01T00:00:00Z\",\"clearExpiration\":true}")]
    public async Task Write_WithInvalidEnvelope_ReturnsBadRequestWithoutCreation(string envelope)
    {
        await using var host = await EndpointHost.StartAsync();

        using var response = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), envelope);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        using var exists = await host.Client.GetAsync(host.Url("one", "/exists"));
        (await exists.Content.ReadFromJsonAsync<bool>()).ShouldBeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("\"first\", \"second\"")]
    [InlineData("\"bdk.%\"")]
    public async Task Put_WithInvalidCondition_ReturnsBadRequest(string etag)
    {
        await using var host = await EndpointHost.StartAsync();
        using var response = await SendAsync(host.Client, HttpMethod.Put, host.Url("one"), Envelope("Invalid"), etag);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{\"properties\":{\"nested\":[]}}")]
    [InlineData("{\"expiresAt\":\"2030-01-01T00:00:00Z\",\"clearExpiration\":true}")]
    public async Task Patch_WithInvalidMetadata_PreservesStoredVersion(string envelope)
    {
        await using var host = await EndpointHost.StartAsync();
        using var created = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Original"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var invalid = await SendAsync(host.Client, HttpMethod.Patch, host.Url("one", "/properties"), envelope, created.Headers.ETag.ToString());

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var current = await host.Client.GetAsync(host.Url("one"));
        current.Headers.ETag.ShouldBe(created.Headers.ETag);
        (await current.Content.ReadFromJsonAsync<TestDocument>()).Name.ShouldBe("Original");
    }

    [Fact]
    public async Task Post_WithIfMatch_ReturnsBadRequest()
    {
        await using var host = await EndpointHost.StartAsync();
        using var response = await SendAsync(host.Client, HttpMethod.Post, host.Url("one"), Envelope("Invalid"), "some-version");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var exists = await host.Client.GetAsync(host.Url("one", "/exists"));
        (await exists.Content.ReadFromJsonAsync<bool>()).ShouldBeFalse();
    }

    [Fact]
    public async Task Get_WithUnknownClient_ReturnsNotFound()
    {
        await using var host = await EndpointHost.StartAsync();
        using var response = await host.Client.GetAsync(DefaultPath + "/unknown/documents?partitionKey=records&rowKey=one");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Map_WithDefaults_RequiresAuthorization()
    {
        var builder = WebApplication.CreateBuilder();
        var provider = new InMemoryDocumentStoreProvider();
        builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => provider).AddMaintenanceEndpoints();
        await using var app = builder.Build();
        app.MapEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.RoutePattern.RawText.StartsWith(DefaultPath, StringComparison.Ordinal)).ToArray();

        endpoints.ShouldNotBeEmpty();
        endpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0);
        endpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.NotFound)]
    public async Task Registration_WithCustomPathAndEnabledFlag_MapsExpectedRoutes(bool enabled, HttpStatusCode expected)
    {
        await using var host = await EndpointHost.StartAsync("/ops/typed-documents", enabled);
        using var response = await host.Client.GetAsync("/ops/typed-documents/clients");
        response.StatusCode.ShouldBe(expected);
        using var defaultResponse = await host.Client.GetAsync(DefaultPath + "/clients");
        defaultResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string Envelope(string name) => JsonSerializer.Serialize(new { content = new TestDocument { Name = name, Count = 1 } });

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string json = null, string etag = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<TestDocument> ReadDocumentAsync(EndpointHost host, string row)
    {
        using var response = await host.Client.GetAsync(host.Url(row));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<TestDocument>();
    }

    /// <summary>Defines the typed payload selected by maintenance registration.</summary>
    public sealed class TestDocument
    {
        public string Name { get; set; }

        public int Count { get; set; }
    }

    /// <summary>Defines a second registered document type using the same provider and logical keys.</summary>
    public sealed class AlternateDocument
    {
        public string Title { get; set; }
    }

    private sealed class EndpointHost(WebApplication app, HttpClient client, string clientId, string groupPath, string alternateClientId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public string ClientId { get; } = clientId;

        public string AlternateClientId { get; } = alternateClientId;

        public string DocumentsPath => groupPath + "/" + Uri.EscapeDataString(this.ClientId) + "/documents";

        public string Url(string row, string suffix = "") => this.DocumentsPath + suffix + "?partitionKey=records&rowKey=" + Uri.EscapeDataString(row);

        public static async Task<EndpointHost> StartAsync(string groupPath = DefaultPath, bool enabled = true, DocumentStoreOptions options = null, bool includeSecondType = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var provider = new InMemoryDocumentStoreProvider();
            builder.Services.AddRouting();
            var storage = builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => provider, name: "archive", documentStoreOptions: options);
            if (includeSecondType) storage.WithProvider<AlternateDocument>(_ => provider, name: "archive");
            storage.AddMaintenanceEndpoints(endpointOptions => endpointOptions.GroupPath(groupPath).AllowAnonymous(), enabled);
            storage.AddReadEndpoints(endpointOptions => endpointOptions.GroupPath(groupPath).AllowAnonymous(), enabled);
            var app = builder.Build();
            app.UseRouting();
            app.MapEndpoints();
            await app.StartAsync();
            var descriptors = app.Services.GetServices<DocumentStoreClientDescriptor>().ToArray();
            var clientId = descriptors.Single(descriptor => descriptor.DocumentType == typeof(TestDocument)).ClientId;
            var alternateClientId = descriptors.SingleOrDefault(descriptor => descriptor.DocumentType == typeof(AlternateDocument))?.ClientId;
            return new(app, app.GetTestClient(), clientId, groupPath, alternateClientId);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
