// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Presentation.UnitTests.Web;

using System.Net;
using BridgingIT.DevKit.Application.Storage;
using BridgingIT.DevKit.Presentation.Web.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using BlobDashboardEndpoints = BridgingIT.DevKit.Presentation.Web.Storage.Blobs.Dashboard.DashboardEndpoints;

public sealed class BlobDashboardPreviewTests
{
    private const int MaximumPreviewBytes = 1024 * 1024;
    private static readonly BlobKey Key = new("reports", "quarterly-report");

    [Theory]
    [InlineData(ContentType.TXT, "text/plain")]
    [InlineData(ContentType.HTML, "text/plain")]
    [InlineData(ContentType.JSON, "text/plain")]
    [InlineData(ContentType.PNG, "image/png")]
    public async Task Preview_WithSupportedType_ReturnsBoundedPreviewBytes(ContentType type, string mediaType)
    {
        // The text fixture checks that markup is returned as literal text.
        var bytes = type == ContentType.PNG
            ? Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aagoAAAAASUVORK5CYII=")
            : "<h1>Quarterly report</h1>"u8.ToArray();
        var info = new BlobInfo { Key = Key, ContentType = type, Length = bytes.Length };
        var sut = CreateClient(info);
        var stream = new MemoryStream(bytes);
        sut.DownloadAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Result<BlobDownload>.Success(new() { Content = stream, Info = info }));

        using var response = await PreviewAsync(sut);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.MediaType.ShouldBe(mediaType);
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        stream.CanRead.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ContentType.SVG, 100, HttpStatusCode.UnsupportedMediaType)]
    [InlineData(ContentType.PDF, 100, HttpStatusCode.UnsupportedMediaType)]
    [InlineData(ContentType.TXT, MaximumPreviewBytes + 1, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Preview_WithUnsupportedMetadata_RejectsWithoutDownloading(ContentType type, int length, HttpStatusCode expected)
    {
        var sut = CreateClient(new BlobInfo { Key = Key, ContentType = type, Length = length });

        using var response = await PreviewAsync(sut);

        response.StatusCode.ShouldBe(expected);
        await sut.DidNotReceive().DownloadAsync(Arg.Any<BlobKey>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(MaximumPreviewBytes, HttpStatusCode.OK)]
    [InlineData(MaximumPreviewBytes + 1, HttpStatusCode.RequestEntityTooLarge)]
    public async Task Preview_WhenStreamLengthDiffersFromMetadata_EnforcesByteLimit(int length, HttpStatusCode expected)
    {
        var info = new BlobInfo { Key = Key, ContentType = ContentType.TXT, Length = 1 };
        var sut = CreateClient(info);
        var stream = new MemoryStream(new byte[length + (expected == HttpStatusCode.OK ? 0 : 10)]);
        sut.DownloadAsync(Key, Arg.Any<CancellationToken>())
            .Returns(Result<BlobDownload>.Success(new() { Content = stream, Info = info }));

        using var response = await PreviewAsync(sut);

        response.StatusCode.ShouldBe(expected);
        if (expected == HttpStatusCode.OK)
        {
            (await response.Content.ReadAsByteArrayAsync()).Length.ShouldBe(length);
        }

        stream.CanRead.ShouldBeFalse();
    }

    private static IBlobStoreClient CreateClient(BlobInfo info)
    {
        var client = Substitute.For<IBlobStoreClient>();
        client.GetPropertiesAsync(Key, Arg.Any<CancellationToken>()).Returns(Result<BlobInfo>.Success(info));
        return client;
    }

    private static async Task<HttpResponseMessage> PreviewAsync(IBlobStoreClient client)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var options = new DashboardEndpointsOptionsBuilder().AllowAnonymous().WithGroupPath("/ops").Build();
        builder.Services.AddSingleton(options);
        builder.Services.AddBlobStorage().WithInMemoryClient("reports");
        var factory = Substitute.For<IBlobStoreClientFactory>();
        factory.CreateClient("reports").Returns(client);
        builder.Services.AddSingleton(factory);
        await using var app = builder.Build();
        new BlobDashboardEndpoints(options).Map(app);
        await app.StartAsync();
        using var http = app.GetTestClient();
        return await http.GetAsync("/ops/storage/blobs/preview?store=reports&container=reports&name=quarterly-report");
    }
}
