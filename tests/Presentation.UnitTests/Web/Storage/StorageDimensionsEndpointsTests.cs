// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using Application.Storage;
using Presentation.Web;
using Presentation.Web.Storage;
using Presentation.Web.Storage.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

public sealed class StorageDimensionsEndpointsTests
{
    [Fact]
    public async Task BlobEndpoints_ForwardTypedMetadataAndFilters_KeepOriginalConditionsAndHeaderLimits()
    {
        var blobs = Substitute.For<IBlobStoreClient>();
        var blobFactory = Substitute.For<IBlobStoreClientFactory>();
        blobFactory.CreateClient("reports").Returns(blobs);
        var dimensions = BlobDimensions.Create(new Dictionary<string, object> { ["year"] = 2026, ["amount"] = 1.0m }).Value;
        var state = new BlobInfo { Key = new("archive", "invoice"), ETag = "v1", Dimensions = dimensions, Length = 3 };
        BlobUpload observed = null;
        blobs.UploadAsync(Arg.Any<BlobUpload>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            observed = call.Arg<BlobUpload>();
            await observed.Content.CopyToAsync(Stream.Null);
            return Result<BlobInfo>.Success(state);
        });
        blobs.UpdateDimensionsAsync(Arg.Any<BlobDimensionsUpdate>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var update = call.Arg<BlobDimensionsUpdate>();
            if (update.IfMatchETag != state.ETag) return Result<BlobInfo>.Failure(new BlobStoreConflictError("Changed."));
            state = new() { Key = state.Key, ETag = "v2", Dimensions = update.Dimensions, Length = state.Length };
            return Result<BlobInfo>.Success(state);
        });
        BlobQuery observedQuery = null;
        blobs.ListPageAsync(Arg.Any<BlobQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observedQuery = call.Arg<BlobQuery>();
            return Result<BlobPage>.Success(new() { Items = [state], ContinuationToken = "next" });
        });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddBlobStorage().WithInMemoryClient("reports").AddMaintenanceEndpoints(options => options.AllowAnonymous()).AddReadEndpoints(options => options.AllowAnonymous());
        builder.Services.AddSingleton(blobFactory);
        await using var app = builder.Build();
        var bodyReads = 0;
        app.Use(async (context, next) => { context.Request.Body = new CountedBody(context.Request.Body, () => bodyReads++); await next(context); });
        app.MapEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string route = "/_bdk/api/storage/blobs/reports/blobs";
        using var upload = new HttpRequestMessage(HttpMethod.Put, route + "?container=archive&name=invoice") { Content = new StringContent("abc") };
        upload.Headers.TryAddWithoutValidation("X-Storage-Dimensions", BlobStorageDimensionsJson.Write(dimensions));
        using var created = await client.SendAsync(upload);
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        observed.Dimensions["amount"].ValueType.ShouldBe(BlobDimensionValueType.Decimal);
        (await created.Content.ReadFromJsonAsync<BlobStorageBlobInfoModel>()).Dimensions["year"].Value.ShouldBe(2026);
        var originalETag = created.Headers.ETag.ToString();
        using var patch = new HttpRequestMessage(HttpMethod.Patch, route + "/dimensions")
        {
            Content = JsonContent.Create(new BlobStorageUpdateDimensionsRequestModel { Container = "archive", Name = "invoice", Dimensions = BlobDimensions.Empty, IfMatchETag = "v1" })
        };
        patch.Headers.TryAddWithoutValidation("If-Match", originalETag);
        using var changed = await client.SendAsync(patch);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
        HttpETag.ReadCondition(changed.Headers.ETag.ToString()).Value.ShouldBe("v2");
        using var stale = new HttpRequestMessage(HttpMethod.Patch, route + "/dimensions") { Content = JsonContent.Create(new BlobStorageUpdateDimensionsRequestModel { Container = "archive", Name = "invoice", Dimensions = dimensions }) };
        stale.Headers.TryAddWithoutValidation("If-Match", originalETag);
        (await client.SendAsync(stale)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var mismatch = new HttpRequestMessage(HttpMethod.Patch, route + "/dimensions") { Content = JsonContent.Create(new BlobStorageUpdateDimensionsRequestModel { Container = "archive", Name = "invoice", Dimensions = dimensions, IfMatchETag = "v2" }) };
        mismatch.Headers.TryAddWithoutValidation("If-Match", originalETag);
        (await client.SendAsync(mismatch)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var filter = BlobDimensionFilter.Where(BlobDimensionPredicate.Create("amount", BlobDimensionOperator.Equal, 1.0m).Value);
        using var page = await client.PostAsJsonAsync(route + "/search", new BlobQuery { Container = "archive", Take = 2, DimensionFilter = filter, ContinuationToken = "previous" });
        page.StatusCode.ShouldBe(HttpStatusCode.OK, await page.Content.ReadAsStringAsync());
        observedQuery.DimensionFilter.ToCanonicalString().ShouldBe(filter.ToCanonicalString());
        observedQuery.ContinuationToken.ShouldBe("previous");
        (await page.Content.ReadFromJsonAsync<BlobStorageBlobPageModel>()).ContinuationToken.ShouldBe("next");
        var before = bodyReads;
        using var invalid = new HttpRequestMessage(HttpMethod.Put, route + "?container=archive&name=invoice") { Content = new StringContent("do not consume") };
        invalid.Headers.TryAddWithoutValidation("X-Storage-Dimensions", "{\"amount\":{\"type\":\"decimal\",\"value\":\"1.0000001\"}}");
        (await client.SendAsync(invalid)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        bodyReads.ShouldBe(before);
        using var oversized = new HttpRequestMessage(HttpMethod.Put, route + "?container=archive&name=invoice") { Content = new StringContent("do not consume") };
        var large = BlobDimensions.Create(Enumerable.Range(0, 64).Select(index => KeyValuePair.Create($"D{index}", (object)new string('\0', 256)))).Value;
        oversized.Headers.TryAddWithoutValidation("X-Storage-Dimensions", BlobStorageDimensionsJson.Write(large));
        (await client.SendAsync(oversized)).StatusCode.ShouldBe(HttpStatusCode.RequestHeaderFieldsTooLarge);
        bodyReads.ShouldBe(before);
    }

    [Fact]
    public async Task DocumentEndpoints_ForwardTypedDimensionsAndSearches()
    {
        var documents = Substitute.For<IDocumentStoreClientAccessor>();
        var factory = Substitute.For<IDocumentStoreClientFactory>();
        factory.Create("docs").Returns(documents);
        var dimensions = DocumentDimensions.Create(new Dictionary<string, object> { ["amount"] = 1.0m, ["flag"] = true }).Value;
        DocumentWriteOptions observedWrite = null;
        documents.UpsertEntryJsonAsync(Arg.Any<DocumentKey>(), Arg.Any<string>(), Arg.Any<DocumentWriteOptions>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observedWrite = call.Arg<DocumentWriteOptions>();
            return Result<DocumentInfo>.Success(new() { Key = call.Arg<DocumentKey>(), ETag = "v1", Dimensions = observedWrite.Dimensions });
        });
        DocumentDimensionsUpdate observedUpdate = null;
        documents.UpdateDimensionsAsync(Arg.Any<DocumentDimensionsUpdate>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observedUpdate = call.Arg<DocumentDimensionsUpdate>();
            return Result<DocumentInfo>.Success(new() { Key = observedUpdate.Key, ETag = "v2", Dimensions = observedUpdate.Dimensions });
        });
        DocumentQuery observedQuery = null;
        documents.ListPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            observedQuery = call.Arg<DocumentQuery>();
            return Result<DocumentKeyPage>.Success(new() { Items = [new("archive", "invoice")] });
        });
        documents.CountAsync(Arg.Any<DocumentCountQuery>(), Arg.Any<CancellationToken>()).Returns(Result<long>.Success(1));
        documents.FindJsonPageAsync(Arg.Any<DocumentQuery>(), Arg.Any<CancellationToken>()).Returns(Result<DocumentJsonPage>.Success(new()));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddDocumentStorage(options => options.Enabled(true)).AddMaintenanceEndpoints(options => options.AllowAnonymous()).AddReadEndpoints(options => options.AllowAnonymous());
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        app.MapEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        const string route = "/_bdk/api/storage/documents/docs/documents";
        using var created = await client.PutAsJsonAsync(route + "?partitionKey=archive&rowKey=invoice", new DocumentStorageWriteRequestModel { Content = JsonSerializer.SerializeToElement(new { Amount = 7 }), Dimensions = dimensions });
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        observedWrite.Dimensions["amount"].ValueType.ShouldBe(DocumentDimensionValueType.Decimal);
        (await created.Content.ReadFromJsonAsync<DocumentStorageDocumentInfoModel>()).Dimensions["flag"].Value.ShouldBe(true);
        using var patch = new HttpRequestMessage(HttpMethod.Patch, route + "/dimensions") { Content = JsonContent.Create(new DocumentStorageUpdateDimensionsRequestModel { PartitionKey = "archive", RowKey = "invoice", Dimensions = DocumentDimensions.Empty, IfMatchETag = "v1" }) };
        patch.Headers.TryAddWithoutValidation("If-Match", created.Headers.ETag.ToString());
        using var updated = await client.SendAsync(patch);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        observedUpdate.IfMatchETag.ShouldBe("v1");
        observedUpdate.Dimensions.Count.ShouldBe(0);
        HttpETag.ReadCondition(updated.Headers.ETag.ToString()).Value.ShouldBe("v2");
        var filter = DocumentDimensionFilter.Where(DocumentDimensionPredicate.Create("amount", DocumentDimensionOperator.Equal, 1.0m).Value);
        var query = new DocumentQuery { DimensionFilter = filter, Take = 2, ContinuationToken = "previous" };
        using var keys = await client.PostAsJsonAsync(route + "/keys", query);
        keys.StatusCode.ShouldBe(HttpStatusCode.OK, await keys.Content.ReadAsStringAsync());
        observedQuery.DimensionFilter.ToCanonicalString().ShouldBe(filter.ToCanonicalString());
        observedQuery.ContinuationToken.ShouldBe("previous");
        using var count = await client.PostAsJsonAsync(route + "/count", new DocumentCountQuery { DimensionFilter = filter });
        count.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await count.Content.ReadFromJsonAsync<long>()).ShouldBe(1);
        using var search = await client.PostAsJsonAsync(route + "/search", query);
        search.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class CountedBody(Stream inner, Action read) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) { read(); return inner.Read(buffer, offset, count); }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) { read(); return inner.ReadAsync(buffer, token); }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
