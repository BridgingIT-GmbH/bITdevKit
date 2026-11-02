// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Storage;
using BridgingIT.DevKit.Presentation;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

[Collection(ProcessWideStateTestCollection.Name)]
public sealed class StorageDashboardMetricsTests
{
    [Theory]
    [InlineData("blobs")]
    [InlineData("documents")]
    [InlineData("files")]
    public async Task Diagnostics_CustomDashboardRoot_RendersChartsAndExposesData(string feature)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops").WithPluginAssemblyContaining<StorageDashboardMetrics>());
        builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => new BridgingIT.DevKit.Application.Storage.InMemoryDocumentStoreProvider());
        builder.Services.AddFileStorage(factory => factory.RegisterProvider("files", storage => storage.UseInMemory("files")));
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/ops/storage/" + feature + "/diagnostics");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("storage-throughput-chart");
        html.ShouldContain("storage-items-chart");
        html.ShouldContain("data-dashboard-view=\"diagnostics\"");
        html.ShouldContain("/ops/storage/" + feature + "/diagnostics/data");
        var data = await client.GetAsync("/ops/storage/" + feature + "/diagnostics/data");
        data.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await data.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("bucketSeconds").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("blobs")]
    [InlineData("documents")]
    [InlineData("files")]
    public async Task LoadActions_CustomRoot_EnforceOriginAndReturnVerifiedCleanup(string feature)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDashboard(options => options.AllowAnonymous().WithGroupPath("/ops").WithPluginAssemblyContaining<StorageDashboardMetrics>());
        builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        builder.Services.AddDocumentStorage().WithProvider<TestDocument>(_ => new InMemoryDocumentStoreProvider(), lifetime: ServiceLifetime.Singleton);
        builder.Services.AddFileStorage(factory => factory.RegisterProvider("files", storage => storage.UseInMemory("files").WithLifetime(ServiceLifetime.Singleton)));
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var blobs = scope.ServiceProvider.GetRequiredKeyedService<IBlobStoreProvider>("reports");
        (await blobs.UploadAsync(new() { Key = new("reports", "keep"), Content = new MemoryStream([1]) })).IsSuccess.ShouldBeTrue();
        var target = feature == "documents" ? app.Services.GetServices<DocumentStoreClientDescriptor>().Single().ClientId : feature == "blobs" ? "reports" : "files";
        using var client = app.GetTestClient();
        var path = "/ops/storage/" + feature + "/diagnostics";
        var settings = new
        {
            id = Guid.NewGuid().ToString("N"),
            target,
            container = "reports",
            payloadKiB = 4,
            seconds = 1,
            workers = 1,
            readPercent = 50
        };
        var denied = await client.PostAsJsonAsync(path + "/load/run", settings);
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Add("X-BDK-Storage-Load", "1");
        using var foreign = new HttpRequestMessage(HttpMethod.Post, path + "/load/run")
        {
            Content = JsonContent.Create(settings)
        };
        foreign.Headers.Add("Origin", "https://foreign.invalid");
        (await client.SendAsync(foreign)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var response = await client.PostAsJsonAsync(path + "/load/run", settings);
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        result.RootElement.GetProperty("phase").GetString().ShouldBe("Completed");
        result.RootElement.GetProperty("cleanupPending").GetInt32().ShouldBe(0);
        result.RootElement.GetProperty("readBytes").GetInt64().ShouldBeGreaterThan(0);
        result.RootElement.GetProperty("writeBytes").GetInt64().ShouldBeGreaterThan(0);
        var metrics = await client.GetFromJsonAsync<JsonElement>(path + "/data");
        metrics.GetProperty("load").GetProperty("phase").GetString().ShouldBe("Completed");
        metrics.GetProperty("samples").ValueKind.ShouldBe(JsonValueKind.Array);
        (await blobs.ExistsAsync(new("reports", "keep"))).Value.ShouldBeTrue();
    }

    [Theory]
    [InlineData("blobs", "blobstorage_", "download", "upload")]
    [InlineData("documents", "document.", "get-many", "upsert-many")]
    [InlineData("files", "filestorage_", "read", "write")]
    public void Snapshot_GroupsTransfersIntoCompletedIntervals(string feature, string prefix, string read, string write)
    {
        var clock = new TestClock();
        using var sut = new StorageDashboardMetrics(feature, clock);
        using var meter = new Meter(BridgingIT.DevKit.Common.Metrics.MeterName);
        var bytes = meter.CreateCounter<long>(prefix + "bytes");
        var items = meter.CreateCounter<long>(prefix + "items");
        clock.Advance(1);
        bytes.Add(10 * 1048576, new KeyValuePair<string, object>("operation", read));
        bytes.Add(5 * 1048576, new KeyValuePair<string, object>("operation", write));
        bytes.Add(100 * 1048576, new KeyValuePair<string, object>("operation", "update-properties"));
        items.Add(3, new KeyValuePair<string, object>("operation", read));
        items.Add(2, new KeyValuePair<string, object>("operation", write));
        var speed = meter.CreateHistogram<double>(prefix + "bytes_per_second");
        speed.Record(4 * 1048576, new KeyValuePair<string, object>("operation", read));
        speed.Record(8 * 1048576, new KeyValuePair<string, object>("operation", write));
        var duration = meter.CreateHistogram<double>(prefix + (feature == "documents" ? "operation.duration" : "operation_duration"));
        duration.Record(2, new KeyValuePair<string, object>("operation", read));
        duration.Record(3, new KeyValuePair<string, object>("operation", write));
        var before = JsonSerializer.SerializeToElement(sut.Snapshot());
        before.GetProperty("samples").GetArrayLength().ShouldBe(1);
        clock.Advance(1);
        var snapshot = JsonSerializer.SerializeToElement(sut.Snapshot());
        snapshot.GetProperty("observed").GetBoolean().ShouldBeTrue();
        var sample = snapshot.GetProperty("samples")[1];
        sample.GetProperty("readMiBPerSecond").GetDouble().ShouldBe(10);
        sample.GetProperty("writeMiBPerSecond").GetDouble().ShouldBe(5);
        sample.GetProperty("readItems").GetDouble().ShouldBe(3);
        sample.GetProperty("writeItems").GetDouble().ShouldBe(2);
        sample.GetProperty("readSpeedMiBPerSecond").GetDouble().ShouldBe(4);
        sample.GetProperty("writeSpeedMiBPerSecond").GetDouble().ShouldBe(8);
        sample.GetProperty("readLatencyMs").GetDouble().ShouldBe(2);
        sample.GetProperty("writeLatencyMs").GetDouble().ShouldBe(3);
        clock.Advance(3600);
        var expired = JsonSerializer.SerializeToElement(sut.Snapshot());
        expired.GetProperty("samples").GetArrayLength().ShouldBe(900);
        expired.GetProperty("samples").EnumerateArray().ShouldAllBe(sample => sample.GetProperty("readItems").GetDouble() == 0 && sample.GetProperty("writeMiBPerSecond").GetDouble() == 0);
    }

    [Fact]
    public void Snapshot_LoadAndApplicationMetrics_CombineTrafficAndExcludeOtherFeatures()
    {
        var clock = new TestClock();
        using var sut = new StorageDashboardMetrics("files", clock);
        using var meter = new Meter(BridgingIT.DevKit.Common.Metrics.MeterName);
        var bytes = meter.CreateCounter<long>("storage_load_bytes");
        clock.Advance(1);
        bytes.Add(1048576, new KeyValuePair<string, object>("feature", "files"), new KeyValuePair<string, object>("operation", "read"));
        bytes.Add(2097152, new KeyValuePair<string, object>("feature", "blobs"), new KeyValuePair<string, object>("operation", "read"));
        meter.CreateCounter<long>("filestorage_bytes").Add(3145728, new KeyValuePair<string, object>("operation", "read"));
        clock.Advance(1);
        var snapshot = JsonSerializer.SerializeToElement(sut.Snapshot());
        snapshot.GetProperty("samples")[1].GetProperty("readMiBPerSecond").GetDouble().ShouldBe(4);
        snapshot.TryGetProperty("loadSamples", out _).ShouldBeFalse();
    }

    [Fact]
    public void Snapshot_FirstPartialInterval_RetainsStartupActivity()
    {
        var clock = new TestClock();
        clock.AdvanceMilliseconds(250);
        using var sut = new StorageDashboardMetrics("blobs", clock);
        using var meter = new Meter(BridgingIT.DevKit.Common.Metrics.MeterName);
        meter.CreateCounter<long>("blobstorage_bytes").Add(3 * 1048576, new KeyValuePair<string, object>("operation", "upload"));
        clock.AdvanceMilliseconds(750);
        var snapshot = JsonSerializer.SerializeToElement(sut.Snapshot());
        snapshot.GetProperty("samples")[0].GetProperty("writeMiBPerSecond").GetDouble().ShouldBe(4);
    }

    [Fact]
    public void Snapshot_WithoutTransferEvidence_IsUnobserved()
    {
        using var sut = new StorageDashboardMetrics("blobs", new TestClock());
        var snapshot = JsonSerializer.SerializeToElement(sut.Snapshot());
        snapshot.GetProperty("observed").GetBoolean().ShouldBeFalse();
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(1000);

        public override DateTimeOffset GetUtcNow()
        {
            return this.now;
        }

        internal void AdvanceMilliseconds(int milliseconds)
        {
            this.now = this.now.AddMilliseconds(milliseconds);
        }

        internal void Advance(int seconds)
        {
            this.now = this.now.AddSeconds(seconds);
        }
    }

    public sealed class TestDocument
    {
        public string Name { get; set; }
    }
}