// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using BridgingIT.DevKit.Presentation.Web.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StoragePermalinkDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Permalinks.Dashboard.DashboardEndpoints;

public class BlobStorageEndpointsApplication : WebApplicationFactory<BlobStorageEndpointsTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        appBuilder.Services.AddStoragePermalinks()
            .UseInMemory()
            .AddDownloadEndpoints();
        appBuilder.Services.AddBlobStorage()
            .WithInMemoryClient("reports", options =>
            {
                options.AllowFullScans = true;
                options.DefaultTake = 20;
                options.MaxTake = 50;
            })
            .WithPermalinks("reports")
            .AddMaintenanceEndpoints(options => options.AllowAnonymous())
            .AddReadEndpoints(options => options.AllowAnonymous());

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }

    public IBlobStoreClient CreateClient(string name)
    {
        var factory = this.Services.GetRequiredService<IBlobStoreClientFactory>();
        return factory.CreateClient(name);
    }

}

public class BlobStorageEndpointsTests : IAsyncDisposable
{
    private readonly BlobStorageEndpointsApplication factory;
    private readonly HttpClient client;

    public BlobStorageEndpointsTests()
    {
        this.factory = new BlobStorageEndpointsApplication();
        this.client = this.factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        this.client.Dispose();
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetClients_ShouldExposeRegisteredBlobClients()
    {
        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/clients");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlobStorageClientInfoModel>>();
        result.ShouldNotBeNull();
        result.ShouldContain(item => item.Name == "reports" && item.ProviderName == InMemoryBlobStoreProvider.ProviderName);
    }

    [Fact]
    public async Task GetClientInfo_ShouldExposeRegisteredBlobClientByName()
    {
        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/provider");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BlobStorageClientInfoModel>();
        result.ShouldNotBeNull();
        result.Name.ShouldBe("reports");
        result.ProviderName.ShouldBe(InMemoryBlobStoreProvider.ProviderName);
    }

    [Fact]
    public async Task DownloadContent_ShouldStreamBlobBytes()
    {
        var blobs = this.factory.CreateClient("reports");
        await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "docs/guide.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("hello blob")),
            ContentType = ContentType.TXT
        });

        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/content?container=reports&name=docs/guide.txt");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
        content.ShouldBe("hello blob");
    }

    [Fact]
    public async Task DownloadContent_MissingBlob_ShouldReturnNotFound()
    {
        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/content?container=reports&name=missing.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void StoragePermalinkEndpointsOptionsBuilder_ConfiguresStandardAuthorizationOptions()
    {
        var options = new StoragePermalinkEndpointsOptionsBuilder()
            .RequireAuthorization()
            .RequirePolicy("StorageDownloads")
            .Build();

        options.RequireAuthorization.ShouldBeTrue();
        options.RequirePolicy.ShouldBe("StorageDownloads");
        options.AllowAnonymous.ShouldBeFalse();
    }

    [Fact]
    public void StoragePermalinkEndpoints_WithScopeValidation_DoesNotCaptureScopedStorageFactories()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        services.AddStoragePermalinks().UseInMemory().AddDownloadEndpoints();
        services.AddBlobStorage().WithInMemoryClient("reports");

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        serviceProvider.GetServices<IEndpoints>().ShouldContain(x => x is StoragePermalinkEndpoints);
    }

    [Fact]
    public async Task DownloadPermalink_WithStoredContentType_UsesBlobMimeTypeInsteadOfFileExtension()
    {
        var blobs = this.factory.CreateClient("reports");
        var upload = await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "docs/report.bin"),
            Content = new MemoryStream([1, 2, 3]),
            ContentType = ContentType.PDF
        });
        upload.IsSuccess.ShouldBeTrue();
        var permalink = await blobs.GetPermalinkAsync(new BlobKey("reports", "docs/report.bin"));
        permalink.IsSuccess.ShouldBeTrue();

        var response = await this.client.GetAsync(StoragePermalinkRoutes.Download(permalink.Value.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task DownloadPermalink_WithUnregisteredBlobClient_ReturnsNotFound()
    {
        var registry = this.factory.Services.GetRequiredService<IStoragePermalinkRegistry>();
        var entry = await registry.GetOrCreateAsync(StorageResourceLocation.ForBlob("missing", new BlobKey("reports", "docs/missing.pdf")));
        entry.IsSuccess.ShouldBeTrue();

        var response = await this.client.GetAsync(StoragePermalinkRoutes.Download(entry.Value.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PermalinkDashboardAction_WithUnknownBlobRegistration_ReturnsBadRequest()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddStoragePermalinks().UseInMemory();
        builder.Services.AddBlobStorage()
            .WithInMemoryClient("reports")
            .WithPermalinks("reports");
        await using var app = builder.Build();
        app.UseRouting();
        new StoragePermalinkDashboardEndpoints(dashboardOptions).Map(app);
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsync(
            "/_bdk/dashboard/storage/permalinks/actions/link",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["kind"] = "Blob",
                ["registration"] = "missing",
                ["scope"] = "reports",
                ["path"] = "docs/report.pdf"
            }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PermalinkDashboardExpirationAction_WithReferer_PreservesCurrentFilterUrl()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var dashboardOptions = new DashboardEndpointsOptionsBuilder().Build();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(dashboardOptions);
        builder.Services.AddStoragePermalinks().UseInMemory();
        builder.Services.AddBlobStorage()
            .WithInMemoryClient("reports")
            .WithPermalinks("reports");
        await using var app = builder.Build();
        app.UseRouting();
        new StoragePermalinkDashboardEndpoints(dashboardOptions).Map(app);
        await app.StartAsync();
        var blobs = app.Services.GetRequiredService<IBlobStoreClientFactory>().CreateClient("reports");
        await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "docs/report.pdf"),
            Content = new MemoryStream([1]),
            ContentType = ContentType.PDF
        });
        var permalink = await blobs.GetPermalinkAsync(new BlobKey("reports", "docs/report.pdf"));
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Referrer = new Uri("http://localhost/_bdk/dashboard/storage/permalinks?kind=Blob&registration=reports");

        var response = await client.PostAsync(
            "/_bdk/dashboard/storage/permalinks/actions/expiration",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = permalink.Value.Id.Value,
                ["etag"] = permalink.Value.ETag,
                ["expiresAt"] = string.Empty
            }));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location?.ToString().ShouldBe("/_bdk/dashboard/storage/permalinks?kind=Blob&registration=reports");
    }

    [Fact]
    public async Task Exists_ShouldReturnTrueAndFalseWithoutDownloadingContent()
    {
        var blobs = this.factory.CreateClient("reports");
        await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "exists.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("exists")),
            ContentType = ContentType.TXT
        });

        var existsResponse = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/blobs/exists?container=reports&name=exists.txt");
        var missingResponse = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/blobs/exists?container=reports&name=missing.txt");

        existsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var exists = await existsResponse.Content.ReadFromJsonAsync<BlobStorageExistsResponseModel>();
        var missing = await missingResponse.Content.ReadFromJsonAsync<BlobStorageExistsResponseModel>();
        exists.ShouldNotBeNull();
        missing.ShouldNotBeNull();
        exists.Exists.ShouldBeTrue();
        missing.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetProperties_ShouldReturnBlobMetadata()
    {
        var blobs = this.factory.CreateClient("reports");
        await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "meta/file.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("meta")),
            ContentType = ContentType.TXT,
            Properties = new PropertyBag
            {
                ["source"] = "test"
            }
        });

        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/blobs/properties?container=reports&name=meta/file.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = await response.Content.ReadFromJsonAsync<BlobStorageBlobInfoModel>();
        metadata.ShouldNotBeNull();
        metadata.Container.ShouldBe("reports");
        metadata.Name.ShouldBe("meta/file.txt");
        metadata.Length.ShouldBe(4);
        metadata.ContentType.ShouldBe("text/plain");
        metadata.Properties["source"].ToString().ShouldBe("test");
    }

    [Fact]
    public async Task UpdateProperties_ShouldReplaceMetadataWithoutContentUpload()
    {
        var blobs = this.factory.CreateClient("reports");
        var upload = await blobs.UploadAsync(new BlobUpload
        {
            Key = new BlobKey("reports", "meta/update.txt"),
            Content = new MemoryStream(Encoding.UTF8.GetBytes("content")),
            ContentType = ContentType.TXT
        });

        var response = await this.client.PatchAsJsonAsync(
            "/_bdk/api/storage/blobs/reports/blobs/properties",
            new BlobStorageUpdatePropertiesRequestModel
            {
                Container = "reports",
                Name = "meta/update.txt",
                ContentType = "application/json",
                IfMatchETag = upload.Value.ETag,
                Properties = new Dictionary<string, object> { ["reviewed"] = true }
            });
        var download = await blobs.DownloadAsync(new BlobKey("reports", "meta/update.txt"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var metadata = await response.Content.ReadFromJsonAsync<BlobStorageBlobInfoModel>();
        metadata.ShouldNotBeNull();
        metadata.ContentType.ShouldBe("application/json");
        metadata.Properties["reviewed"].ToString().ShouldBe("True");
        download.IsSuccess.ShouldBeTrue();
        await using (download.Value)
        {
            using var reader = new StreamReader(download.Value.Content, Encoding.UTF8);
            (await reader.ReadToEndAsync()).ShouldBe("content");
        }
    }

    [Fact]
    public async Task ListBlobs_ShouldReturnMatchingPrefixOnly()
    {
        var blobs = this.factory.CreateClient("reports");
        await blobs.UploadAsync(CreateUpload("docs/alpha.txt", "alpha"));
        await blobs.UploadAsync(CreateUpload("docs/beta.txt", "beta"));
        await blobs.UploadAsync(CreateUpload("other/gamma.txt", "gamma"));

        var response = await this.client.GetAsync("/_bdk/api/storage/blobs/reports/blobs?container=reports&prefix=docs/&take=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var page = await response.Content.ReadFromJsonAsync<BlobStorageBlobPageModel>();
        page.ShouldNotBeNull();
        page.Items.Select(item => item.Name).ShouldBe(["docs/alpha.txt", "docs/beta.txt"], ignoreOrder: true);
    }

    [Fact]
    public async Task DeleteBlob_ShouldRemoveBlob()
    {
        var blobs = this.factory.CreateClient("reports");
        await blobs.UploadAsync(CreateUpload("delete/me.txt", "delete"));

        var deleteResponse = await this.client.DeleteAsync("/_bdk/api/storage/blobs/reports/blobs?container=reports&name=delete/me.txt");
        var exists = await blobs.ExistsAsync(new BlobKey("reports", "delete/me.txt"));

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        exists.IsSuccess.ShouldBeTrue();
        exists.Value.ShouldBeFalse();
    }

    private static BlobUpload CreateUpload(string name, string content) => new()
    {
        Key = new BlobKey("reports", name),
        Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        ContentType = ContentType.TXT
    };
}
