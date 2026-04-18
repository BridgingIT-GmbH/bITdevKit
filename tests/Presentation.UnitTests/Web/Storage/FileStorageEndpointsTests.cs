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
using BridgingIT.DevKit.Presentation.Web.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class FileStorageEndpointsApplication : WebApplicationFactory<FileStorageEndpointsTests>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.WebHost.UseTestServer();

        appBuilder.Services.AddRouting();
        appBuilder.Services.AddLogging();
        appBuilder.Services.AddMemoryCache();
        appBuilder.Services.AddFileStorage(factory => factory
            .RegisterProvider("documents", storage => storage
                .UseInMemory("Documents")
                .WithLifetime(ServiceLifetime.Singleton))
            .RegisterProvider("hidden", storage => storage
                .UseInMemory("Hidden")
                .WithLifetime(ServiceLifetime.Singleton)))
            .AddEndpoints();
        appBuilder.Services.AddFileMonitoring(monitoring =>
        {
            monitoring.UseProvider("documents", "documents", options =>
            {
                options.UseOnDemandOnly = true;
                options.UseProcessor<FileLoggerProcessor>();
            });
        }, ServiceLifetime.Scoped);
        appBuilder.Services.AddSingleton<InMemoryFileEventStore>();
        appBuilder.Services.AddScoped<IFileEventStore>(sp => sp.GetRequiredService<InMemoryFileEventStore>());

        var app = appBuilder.Build();
        app.UseRouting();
        app.MapEndpoints();
        app.Start();

        return app;
    }

    public IFileStorageProvider CreateProvider(string name)
    {
        var factory = this.Services.GetRequiredService<IFileStorageProviderFactory>();
        return factory.CreateProvider(name);
    }

    public T GetRequiredService<T>() where T : notnull
        => this.Services.GetRequiredService<T>();

    public IServiceScope CreateScope()
        => this.Services.CreateScope();
}

public class FileStorageEndpointsTests : IAsyncDisposable
{
    private readonly FileStorageEndpointsApplication factory;
    private readonly HttpClient client;

    public FileStorageEndpointsTests()
    {
        this.factory = new FileStorageEndpointsApplication();
        this.client = this.factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        this.client.Dispose();
        await this.factory.DisposeAsync();
    }

    [Fact]
    public async Task GetProviderInfo_ShouldExposeRegisteredProvidersByName()
    {
        var response = await this.client.GetAsync("/api/_system/documents/provider");
        var hiddenResponse = await this.client.GetAsync("/api/_system/hidden/provider");
        var result = await response.Content.ReadFromJsonAsync<FileStorageProviderInfoModel>();
        var hiddenResult = await hiddenResponse.Content.ReadFromJsonAsync<FileStorageProviderInfoModel>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        hiddenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        hiddenResult.ShouldNotBeNull();
        result.ProviderName.ShouldBe("documents");
        result.LocationName.ShouldBe("Documents");
        hiddenResult.ProviderName.ShouldBe("hidden");
        hiddenResult.LocationName.ShouldBe("Hidden");
    }

    [Fact]
    public async Task GetLocations_ShouldReturnAllRegisteredProviders()
    {
        var response = await this.client.GetAsync("/api/_system/locations");
        var result = await response.Content.ReadFromJsonAsync<List<FileStorageProviderInfoModel>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.Select(item => item.ProviderName).ShouldBe(["documents", "hidden"], ignoreOrder: true);
        result.ShouldContain(item => item.ProviderName == "documents" && item.LocationName == "Documents");
        result.ShouldContain(item => item.ProviderName == "hidden" && item.LocationName == "Hidden");
    }

    [Fact]
    public async Task UnknownProvider_ShouldReturnNotFound()
    {
        var response = await this.client.GetAsync("/api/_system/missing/provider");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutAndGetFileContent_ShouldRoundTripBytes()
    {
        var putResponse = await this.client.PutAsync(
            "/api/_system/documents/files/content?path=docs/guide.txt",
            new ByteArrayContent(Encoding.UTF8.GetBytes("Hello from endpoint storage")));

        var getResponse = await this.client.GetAsync("/api/_system/documents/files/content?path=docs/guide.txt");
        var content = await getResponse.Content.ReadAsStringAsync();

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldBe("Hello from endpoint storage");
    }

    [Fact]
    public async Task GetFileMetadata_ShouldReturnStoredMetadata()
    {
        var provider = this.factory.CreateProvider("documents");
        await provider.WriteBytesAsync("meta/file.txt", Encoding.UTF8.GetBytes("meta"), cancellationToken: CancellationToken.None);

        var response = await this.client.GetAsync("/api/_system/documents/files/metadata?path=meta/file.txt");
        var metadata = await response.Content.ReadFromJsonAsync<FileMetadata>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        metadata.ShouldNotBeNull();
        metadata.Path.ShouldBe("meta/file.txt");
        metadata.Length.ShouldBe(4);
    }

    [Fact]
    public async Task ListFiles_ShouldReturnStoredFiles()
    {
        var provider = this.factory.CreateProvider("documents");
        await provider.WriteBytesAsync("docs/alpha.txt", Encoding.UTF8.GetBytes("alpha"), cancellationToken: CancellationToken.None);
        await provider.WriteBytesAsync("docs/beta.txt", Encoding.UTF8.GetBytes("beta"), cancellationToken: CancellationToken.None);

        var response = await this.client.GetAsync("/api/_system/documents/files?path=docs&searchPattern=*.txt&recursive=true");
        var files = await response.Content.ReadFromJsonAsync<FileStorageFilesResponseModel>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        files.ShouldNotBeNull();
        files.Files.ShouldContain("docs/alpha.txt");
        files.Files.ShouldContain("docs/beta.txt");
    }

    [Fact]
    public async Task DirectoryLifecycle_ShouldCreateAndDeleteDirectory()
    {
        var createResponse = await this.client.PostAsync("/api/_system/documents/directories?path=ops/archive", null);
        var existsAfterCreateResponse = await this.client.GetAsync("/api/_system/documents/directories/exists?path=ops/archive");
        var existsAfterCreate = await existsAfterCreateResponse.Content.ReadFromJsonAsync<FileStorageExistsResponseModel>();

        var deleteResponse = await this.client.DeleteAsync("/api/_system/documents/directories?path=ops/archive&recursive=true");
        var existsAfterDeleteResponse = await this.client.GetAsync("/api/_system/documents/directories/exists?path=ops/archive");
        var existsAfterDelete = await existsAfterDeleteResponse.Content.ReadFromJsonAsync<FileStorageExistsResponseModel>();

        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        existsAfterCreateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        existsAfterCreate.ShouldNotBeNull();
        existsAfterCreate.Exists.ShouldBeTrue();

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        existsAfterDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        existsAfterDelete.ShouldNotBeNull();
        existsAfterDelete.Exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetFileContent_MissingFile_ShouldReturnNotFound()
    {
        var response = await this.client.GetAsync("/api/_system/documents/files/content?path=missing/file.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFileEvents_ShouldReturnStoredEvents()
    {
        using var scope = this.factory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IFileEventStore>();
        await store.StoreEventAsync(new FileEvent
        {
            LocationName = "documents",
            FilePath = "docs/guide.txt",
            EventType = FileEventType.Added,
            DetectedDate = DateTimeOffset.UtcNow
        });
        await store.StoreEventAsync(new FileEvent
        {
            LocationName = "documents",
            FilePath = "docs/guide.txt",
            EventType = FileEventType.Changed,
            DetectedDate = DateTimeOffset.UtcNow.AddMinutes(1)
        });

        var response = await this.client.GetAsync("/api/_system/documents/events?path=docs/guide.txt&eventType=Changed");
        var result = await response.Content.ReadFromJsonAsync<FileStorageFileEventsResponseModel>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ProviderName.ShouldBe("documents");
        result.Count.ShouldBe(1);
        result.Events.ShouldHaveSingleItem();
        result.Events[0].FilePath.ShouldBe("docs/guide.txt");
        result.Events[0].EventType.ShouldBe("Changed");
    }

    [Fact]
    public async Task ScanFileEvents_ShouldDetectProviderChanges()
    {
        var provider = this.factory.CreateProvider("documents");
        await provider.WriteBytesAsync("events/new.txt", Encoding.UTF8.GetBytes("scan me"), cancellationToken: CancellationToken.None);

        var response = await this.client.PostAsync("/api/_system/documents/events/scan?waitForProcessing=true", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<FileStorageFileEventScanResponseModel>();
        result.ShouldNotBeNull();
        result.ProviderName.ShouldBe("documents");
        result.EventCount.ShouldBeGreaterThan(0);
        result.Events.ShouldContain(e => e.FilePath == "events/new.txt" && e.EventType == "Added");
    }

    [Fact]
    public async Task ScanFileEvents_ForUnmonitoredProvider_ShouldReturnNotFound()
    {
        var response = await this.client.PostAsync("/api/_system/hidden/events/scan?waitForProcessing=true", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
