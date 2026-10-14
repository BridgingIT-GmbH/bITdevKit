// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net.Http;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

/// <summary>Exercises actual storage dashboard actions through the HTTP blacklist and registered storage decorators.</summary>
/// <example>Run with the StorageDashboardProfilingTests filter.</example>
public sealed class StorageDashboardProfilingTests
{
    /// <summary>Files, documents and blobs produce independent operations for dashboard actions and reads despite HTTP exclusion.</summary>
    /// <example><code>await suite.DashboardActions_HttpExcluded_CapturesStorageOperations();</code></example>
    [Fact]
    public async Task DashboardActions_HttpExcluded_CapturesStorageOperations()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProfiling(o => o.Enabled()).WithOperationProfiling()
            .WithRequestProfiling();
        builder.Services.AddFileStorage(factory => factory.RegisterProvider("documents", o => o.UseInMemory("documents").WithProfiling()));
        builder.Services.AddDocumentStorage().WithProfilingBehavior<TestDocument>()
            .RegisterProvider<TestDocument>(_ => new InMemoryDocumentStoreProvider(), name: "archive");
        builder.Services.AddBlobStorage().WithProfilingBehavior().WithInMemoryClient("reports");
        builder.Services.AddDashboard(o => o.AllowAnonymous()
            .WithPluginAssemblyContaining<BridgingIT.DevKit.Presentation.Web.Storage.Files.Dashboard.DashboardEndpoints>());
        await using var app = builder.Build();
        app.UseRequestProfiling();
        app.UseRouting();
        app.MapEndpoints();
        await app.StartAsync();
        var writer = app.Services.GetRequiredService<OperationProfilingWriterService>();
        await writer.TickAsync();
        using var client = app.GetTestClient();

        using var file = await client.PostAsync("/_bdk/dashboard/storage/files/actions/create-text-file", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["provider"] = "documents", ["name"] = "profiling.txt", ["content"] = "test"
        }));
        file.EnsureSuccessStatusCode();
        file.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        var descriptor = app.Services.GetServices<DocumentStoreClientDescriptor>().Single();
        using var document = await client.PostAsync("/_bdk/dashboard/storage/documents/actions/save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["clientId"] = descriptor.ClientId, ["partitionKey"] = "test", ["rowKey"] = "one", ["content"] = "{\"Name\":\"test\"}"
        }));
        document.EnsureSuccessStatusCode();
        document.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent("reports"), "store");
        upload.Add(new StringContent("test"), "container");
        upload.Add(new StringContent("profiling.txt"), "name");
        upload.Add(new ByteArrayContent([1, 2, 3]), "file", "profiling.txt");
        using var blob = await client.PostAsync("/_bdk/dashboard/storage/blobs/actions/upload", upload);
        blob.EnsureSuccessStatusCode();
        blob.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        await writer.TickAsync();
        var queries = app.Services.GetRequiredService<IOperationProfilingQueryService>();
        var records = await queries.QueryAsync(new() { Outcomes = [], PageSize = 100 });
        records.IsSuccess.ShouldBeTrue();
        records.Value.Records.Count.ShouldBe(3);
        records.Value.Records.Select(record => record.Kind).Order().ShouldBe(["BlobStorage", "DocumentStorage", "FileStorage"]);
        records.Value.Records.Single(record => record.Kind == "FileStorage").Dimensions.Single(d => d.Key == "filestorage.location").Value.Scalar.ShouldBe("documents");
        records.Value.Records.Single(record => record.Kind == "BlobStorage").Dimensions.Single(d => d.Key == "blobstorage.container").Value.Scalar.ShouldBe("test");
        records.Value.Records.Single(record => record.Kind == "DocumentStorage").Dimensions.Single(d => d.Key == "documentstorage.client").Value.Scalar.ShouldBe("archive");

        foreach (var storage in new[] { "files", "documents", "blobs" })
        {
            using var poll = await client.GetAsync($"/_bdk/dashboard/storage/{storage}/content");
            poll.EnsureSuccessStatusCode();
            poll.Headers.Contains(RequestProfilingMiddleware.HeaderName).ShouldBeFalse();
        }

        await writer.TickAsync();
        var afterReads = await queries.QueryAsync(new() { Outcomes = [], PageSize = 100 });
        afterReads.IsSuccess.ShouldBeTrue();
        afterReads.Value.TotalCount.ShouldBeGreaterThan(3);
        afterReads.Value.Records.ShouldAllBe(record => record.Kind != "HttpRequest");
    }

    /// <summary>Defines an isolated test document without touching application data.</summary>
    /// <example><code>var document = new TestDocument { Name = "test" };</code></example>
    public sealed class TestDocument
    {
        /// <summary>Gets or sets the sample payload.</summary>
        /// <example><code>document.Name = "test";</code></example>
        public string Name { get; set; }
    }
}
