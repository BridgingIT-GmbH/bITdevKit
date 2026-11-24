// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

public sealed class DocumentStorageReadEndpointsTests
{
    private const string DefaultPath = "/_bdk/api/storage/documents";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ReadEndpoints_WithExplicitRegistration_ReturnsTypedJsonAndSnapshotEtagWithoutExposingMaintenance(int registration)
    {
        // Arrange
        await using var sut = await EndpointHost.StartAsync(storage =>
        {
            switch (registration)
            {
                case 0: storage.AddReadEndpoints(options => options.AllowAnonymous()); break;
                case 1: storage.AddReadEndpoints(new DocumentStorageReadEndpointsOptions { AllowAnonymous = true, RequireAuthorization = false }); break;
                case 2: storage.Services.AddDocumentStorageReadEndpoints(options => options.AllowAnonymous()); break;
                case 3: storage.Services.AddDocumentStorageReadEndpoints(new DocumentStorageReadEndpointsOptions { AllowAnonymous = true, RequireAuthorization = false }); break;
                default:
                    storage.Services.AddSingleton(new DocumentStorageReadEndpointsOptions { AllowAnonymous = true, RequireAuthorization = false });
                    storage.AddReadEndpoints();
                    break;
            }
        });
        var key = new DocumentKey("records/2026", "nested/42");
        var original = await sut.Documents.UpsertAsync(key, new() { Name = "München" });
        original.IsSuccess.ShouldBeTrue();

        // Act
        using var response = await sut.Client.GetAsync(sut.Url(key));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        (await response.Content.ReadFromJsonAsync<TestDocument>()).Name.ShouldBe("München");
        HttpETag.ReadCondition(response.Headers.ETag.ToString()).Value.ShouldBe(original.Value.ETag);
        using var clients = await sut.Client.GetAsync(DefaultPath + "/clients");
        clients.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var properties = await sut.Client.GetAsync(sut.Url(key, suffix: "/properties"));
        properties.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, sut.Url(key)) { Content = JsonContent.Create(new { content = new { name = "unauthorized replacement" } }) };
            using var mutation = await sut.Client.SendAsync(request);
            mutation.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        }

        (await sut.Documents.GetAsync(key)).Value.ETag.ShouldBe(original.Value.ETag);
    }

    [Fact]
    public async Task ReadEndpoints_WithDefaults_RequiresAuthorization()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => new InMemoryDocumentStoreProvider()).AddReadEndpoints();
        await using var sut = builder.Build();

        // Act
        sut.MapEndpoints();
        var route = ((IEndpointRouteBuilder)sut).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText == DefaultPath + "/{clientId}/documents");

        // Assert
        route.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldNotBeEmpty();
        route.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
    }

    [Fact]
    public async Task ReadAndMaintenanceEndpoints_WithSeparatePaths_KeepAuthorizationIndependent()
    {
        // Arrange
        await using var sut = await EndpointHost.StartAsync(storage => storage
            .AddReadEndpoints(options => options.GroupPath("/public/documents").AllowAnonymous())
            .AddMaintenanceEndpoints(options => options.GroupPath("/ops/documents").RequireAuthorization()));
        var key = new DocumentKey("records", "42");
        (await sut.Documents.UpsertAsync(key, new() { Name = "Ada" })).IsSuccess.ShouldBeTrue();

        // Act
        using var response = await sut.Client.GetAsync(sut.Url(key, "/public/documents"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var routes = ((IEndpointRouteBuilder)sut.App).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var read = routes.Single(endpoint => endpoint.RoutePattern.RawText == "/public/documents/{clientId}/documents");
        read.Metadata.GetMetadata<IAllowAnonymous>().ShouldNotBeNull();
        var maintenance = routes.Single(endpoint => endpoint.RoutePattern.RawText == "/ops/documents/clients");
        maintenance.Metadata.GetOrderedMetadata<IAuthorizeData>().ShouldNotBeEmpty();
        maintenance.Metadata.GetMetadata<IAllowAnonymous>().ShouldBeNull();
        using var defaultRoute = await sut.Client.GetAsync(sut.Url(key));
        defaultRoute.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MaintenanceEndpoints_WithoutReadOptIn_DoNotExposeDocumentContent()
    {
        await using var sut = await EndpointHost.StartAsync(storage => storage.AddMaintenanceEndpoints(options => options.AllowAnonymous()));
        var key = new DocumentKey("records", "42");
        using var created = await sut.Client.PostAsJsonAsync(sut.Url(key), new { content = new { name = "Ada" } });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var read = await sut.Client.GetAsync(sut.Url(key));
        using var properties = await sut.Client.GetAsync(created.Headers.Location);

        read.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        properties.StatusCode.ShouldBe(HttpStatusCode.OK);
        properties.Headers.ETag.ShouldBe(created.Headers.ETag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadEndpoints_WhenDisabled_DoNotMapContent(bool disableOptions)
    {
        await using var sut = await EndpointHost.StartAsync(storage =>
        {
            if (disableOptions) storage.AddReadEndpoints(new DocumentStorageReadEndpointsOptions { Enabled = false });
            else storage.AddReadEndpoints(enabled: false);
        });

        using var response = await sut.Client.GetAsync(sut.Url(new("records", "42")));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReadEndpoints_WithUnknownMissingOrExpiredDocument_ReturnNotFound()
    {
        await using var sut = await EndpointHost.StartAsync(storage => storage.AddReadEndpoints(options => options.AllowAnonymous()));
        var key = new DocumentKey("records", "expired");
        (await sut.Documents.UpsertAsync(key, new() { Name = "Expired" }, new() { Expiration = ExpirationChange.At(DateTimeOffset.UtcNow.AddMinutes(-1)) })).IsSuccess.ShouldBeTrue();

        using var unknown = await sut.Client.GetAsync(DefaultPath + "/unknown/documents?partitionKey=records&rowKey=42");
        using var missing = await sut.Client.GetAsync(sut.Url(new("records", "missing")));
        using var expired = await sut.Client.GetAsync(sut.Url(key));

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        expired.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        expired.Headers.ETag.ShouldBeNull();
    }

    [Fact]
    public async Task ReadEndpoints_WithInvalidKey_ReturnBadRequest()
    {
        await using var sut = await EndpointHost.StartAsync(storage => storage.AddReadEndpoints(options => options.AllowAnonymous()));

        using var response = await sut.Client.GetAsync(sut.Url(new("records", " ")));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    public sealed class TestDocument
    {
        public string Name { get; set; }
    }

    private sealed class EndpointHost(WebApplication app, IServiceScope scope, HttpClient client, string clientId) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public HttpClient Client { get; } = client;

        public IDocumentStoreClient<TestDocument> Documents { get; } = scope.ServiceProvider.GetRequiredService<IDocumentStoreClient<TestDocument>>();

        public string Url(DocumentKey key, string path = DefaultPath, string suffix = "") => path + "/" + Uri.EscapeDataString(clientId) + "/documents" + suffix
            + "?partitionKey=" + Uri.EscapeDataString(key.PartitionKey) + "&rowKey=" + Uri.EscapeDataString(key.RowKey);

        public static async Task<EndpointHost> StartAsync(Action<DocumentStorageBuilderContext> configure)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            var provider = new InMemoryDocumentStoreProvider();
            var storage = builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => provider);
            configure(storage);
            var app = builder.Build();
            app.UseRouting();
            app.MapEndpoints();
            await app.StartAsync();
            var scope = app.Services.CreateScope();
            var descriptor = scope.ServiceProvider.GetRequiredService<IDocumentStoreClientFactory>().GetDescriptors().Single();
            return new(app, scope, app.GetTestClient(), descriptor.ClientId);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            scope.Dispose();
            await this.App.DisposeAsync();
        }
    }
}
