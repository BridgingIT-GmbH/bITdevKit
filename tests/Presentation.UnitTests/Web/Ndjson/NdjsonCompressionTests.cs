// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Presentation.UnitTests.Web.Ndjson;

using System.IO.Compression;
using System.Runtime.CompilerServices;
using BridgingIT.DevKit.Presentation.Web;
using Microsoft.AspNetCore.Builder;

public class NdjsonCompressionTests
{
    [Fact]
    public async Task StreamingResult_DefaultTypedMetadata_BypassesCompressionWithoutExtraConvention()
    {
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/stream", () => NdjsonHttpResult.From(new[] { 1, 2 })));
        using var client = new HttpClient { BaseAddress = server.Address };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/stream");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        response.Headers.GetValues("X-Ndjson-Mode").Single().ShouldBe("raw");
        (await response.Content.ReadAsStringAsync()).ShouldBe("1\n2\n");
    }

    [Theory]
    [InlineData(false, "gzip")]
    [InlineData(true, "gzip")]
    [InlineData(true, "br")]
    public async Task EnvelopeIdle_ForwardingProxy_DeliversHeartbeatWithoutSourceData(bool compression, string encoding)
    {
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/idle", () => NdjsonHttpResult.From(Idle()))
            .WithNdjson<int>(o => { o.Mode = NdjsonMode.Envelope; o.EnableCompression = compression; o.HeartbeatInterval = TimeSpan.FromMilliseconds(100); }));
        await using var proxy = await StreamingProxyFixture.StartAsync(server.Address);
        using var client = new HttpClient { BaseAddress = proxy.Address };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/idle");
        request.Headers.AcceptEncoding.ParseAdd(encoding);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.Headers.GetValues("X-Ndjson-Mode").Single().ShouldBe("envelope");
        response.Headers.GetValues("X-Ndjson-Envelope-Version").Single().ShouldBe("1");
        await using var body = await response.Content.ReadAsStreamAsync();
        await using var decoded = compression ? encoding == "gzip" ? (Stream)new GZipStream(body, CompressionMode.Decompress, leaveOpen: true) : new BrotliStream(body, CompressionMode.Decompress, leaveOpen: true) : body;
        await using var frames = new NdjsonEnvelopeReader().ReadAsync<int>(decoded).GetAsyncEnumerator();
        for (var i = 0; i < 2; i++)
        {
            (await frames.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
            frames.Current.Kind.ShouldBe(NdjsonEnvelopeKind.Heartbeat);
        }

        async IAsyncEnumerable<int> Idle([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    [Theory]
    [InlineData(false, "gzip")]
    [InlineData(false, "br")]
    [InlineData(true, "gzip")]
    [InlineData(true, "br")]
    public async Task StreamingResult_ForwardingProxy_PreservesIncrementalDeliveryWithExplicitCompression(bool compression, string encoding)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await LoopbackStreamingFixture.StartAsync(app => app.MapGet("/stream", () => NdjsonHttpResult.From(Source())).WithNdjson<string>(o => o.EnableCompression = compression));
        await using var proxy = await StreamingProxyFixture.StartAsync(server.Address);
        using var client = new HttpClient { BaseAddress = proxy.Address };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/stream");
        request.Headers.AcceptEncoding.ParseAdd(encoding);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.Content.Headers.ContentEncoding.ShouldBe(compression ? new[] { encoding } : []);
        await using var body = await response.Content.ReadAsStreamAsync();
        await using var decoded = compression ? encoding == "gzip" ? (Stream)new GZipStream(body, CompressionMode.Decompress, leaveOpen: true) : new BrotliStream(body, CompressionMode.Decompress, leaveOpen: true) : body;
        await using var items = new NdjsonReader().ReadAsync<string>(decoded).GetAsyncEnumerator();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        items.Current.ShouldBe("first");
        finish.Task.IsCompleted.ShouldBeFalse();
        finish.TrySetResult();
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeTrue();
        items.Current.ShouldBe("second");
        (await items.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10))).ShouldBeFalse();

        async IAsyncEnumerable<string> Source([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "first";
            await finish.Task.WaitAsync(cancellationToken);
            yield return "second";
        }
    }
}
