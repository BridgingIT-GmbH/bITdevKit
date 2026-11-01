// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Utilities;

using System.Net;
using System.Net.Http.Headers;
using System.Threading.Channels;
using BridgingIT.DevKit.Testing.Ndjson;

public class NdjsonHttpClientTests
{
    private const string Heartbeat = "{\"version\":1,\"kind\":\"heartbeat\"}\n";

    [Fact]
    public async Task OpenNdjsonAsync_Post_PreservesAuthenticationAndCallerOwnership()
    {
        using var body = new NdjsonTestStreams.FragmentedReadStream("1\n2\n"u8.ToArray());
        using var handler = new Handler(() => Response(body));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://example.test") };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/stream") { Content = new StringContent("fictional selection") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "fictional-token");
        var opened = await client.OpenNdjsonAsync<int>(request);
        opened.IsSuccess.ShouldBeTrue();
        await using (var response = opened.Value)
        {
            await foreach (var value in response.ReadAsync()) { value.ShouldBe(1); break; }
        }

        body.Disposed.ShouldBeTrue();
        handler.Count.ShouldBe(1);
        handler.LastRequest.ShouldBeSameAs(request);
        handler.LastRequest.Method.ShouldBe(HttpMethod.Post);
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("fictional-token");
        (await request.Content.ReadAsStringAsync()).ShouldBe("fictional selection");
        handler.Disposed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetNdjsonAsync_FailedStatus_ReadsBoundedBodyWithoutExposingIt()
    {
        using var body = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(new string('x', 100000)), 4096);
        using var handler = new Handler(() => Response(body, HttpStatusCode.BadRequest));
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream");
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.OfType<NdjsonClientError>().Single();
        error.StatusCode.ShouldBe(400);
        error.Message.ShouldNotContain("xxxxx");
        body.BytesRead.ShouldBe(4096);
        body.Disposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("application/json", null, null)]
    [InlineData("application/x-ndjson", "envelope", "2")]
    [InlineData("application/x-ndjson", "raw", "1")]
    public async Task OpenNdjsonAsync_InvalidDeclaration_ReturnsSafeFailure(string contentType, string mode, string version)
    {
        using var handler = new Handler(() =>
        {
            var response = Response(new NdjsonTestStreams.FragmentedReadStream("1\n"u8.ToArray()));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            if (mode is not null) { response.Headers.Add("X-Ndjson-Mode", mode); }

            if (version is not null) { response.Headers.Add("X-Ndjson-Envelope-Version", version); }

            return response;
        });
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream");
        result.IsFailure.ShouldBeTrue();
        handler.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReadEnvelopesAsync_TerminalError_ThrowsOnParsingPullAndDisposes()
    {
        using var body = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat + "{\"version\":1,\"kind\":\"error\",\"error\":{\"code\":\"application_failed\",\"message\":\"private text\"}}\n"), 4096, true);
        using var handler = new Handler(() => EnvelopeResponse(body));
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream", new() { ExpectedMode = NdjsonMode.Envelope });
        await using var response = result.Value;
        await using var iterator = response.ReadEnvelopesAsync().GetAsyncEnumerator();
        (await iterator.MoveNextAsync()).ShouldBeTrue();
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => iterator.MoveNextAsync().AsTask());
        error.ReasonCode.ShouldBe("stream_terminal_error");
        error.RemoteErrorCode.ShouldBe("application_failed");
        error.Message.ShouldNotContain("private text");
        body.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadDataAsync_ConsumerPause_ExcludesProcessingTimeAndDoesNotWaitForEof()
    {
        var clock = new NdjsonTestTimeProvider();
        using var body = new NdjsonTestStreams.FragmentedReadStream(Encoding.UTF8.GetBytes(Heartbeat + "{\"version\":1,\"kind\":\"data\",\"data\":1}\n{\"version\":1,\"kind\":\"data\",\"data\":2}\n{\"version\":1,\"kind\":\"completed\",\"recordCount\":2}\n"), 4096, true);
        using var handler = new Handler(() => EnvelopeResponse(body));
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream", new() { ExpectedMode = NdjsonMode.Envelope, TimeProvider = clock });
        await using var response = result.Value;
        await using var iterator = response.ReadDataAsync().GetAsyncEnumerator();
        (await iterator.MoveNextAsync()).ShouldBeTrue();
        iterator.Current.ShouldBe(1);
        clock.Advance(TimeSpan.FromHours(2));
        (await iterator.MoveNextAsync()).ShouldBeTrue();
        iterator.Current.ShouldBe(2);
        (await iterator.MoveNextAsync()).ShouldBeFalse();
        body.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadEnvelopesAsync_PartialByteTrickles_DoNotResetCompleteFrameDeadline()
    {
        var clock = new NdjsonTestTimeProvider();
        using var body = new PushReadStream();
        using var handler = new Handler(() => EnvelopeResponse(body));
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream", new() { ExpectedMode = NdjsonMode.Envelope, TimeProvider = clock });
        await using var response = result.Value;
        await using var iterator = response.ReadEnvelopesAsync().GetAsyncEnumerator();
        var first = iterator.MoveNextAsync().AsTask();
        await body.PushAsync(Heartbeat);
        (await first).ShouldBeTrue();
        var pending = iterator.MoveNextAsync().AsTask();
        clock.Advance(TimeSpan.FromSeconds(15));
        await body.PushAsync("{");
        clock.Advance(TimeSpan.FromSeconds(15));
        await body.PushAsync("\"");
        clock.Advance(TimeSpan.FromSeconds(15));
        var error = await Should.ThrowAsync<NdjsonStreamException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        error.ReasonCode.ShouldBe("stream_input_timeout");
        handler.Count.ShouldBe(1);
        body.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_SecondConsumptionPath_RejectsConcurrentEnumeration()
    {
        using var handler = new Handler(() => Response(new NdjsonTestStreams.FragmentedReadStream("1\n2\n"u8.ToArray())));
        using var client = new HttpClient(handler);
        var result = await client.GetNdjsonAsync<int>("http://example.test/stream");
        await using var response = result.Value;
        await using var first = response.ReadAsync().GetAsyncEnumerator();
        (await first.MoveNextAsync()).ShouldBeTrue();
        await using var second = response.ReadRecordsAsync().GetAsyncEnumerator();
        await Should.ThrowAsync<InvalidOperationException>(() => second.MoveNextAsync().AsTask());
        (await first.MoveNextAsync()).ShouldBeTrue();
        first.Current.ShouldBe(2);
    }

    private static HttpResponseMessage Response(Stream body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var response = new HttpResponseMessage(status) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(NdjsonDefaults.CONTENT_TYPE);
        return response;
    }

    private static HttpResponseMessage EnvelopeResponse(Stream body)
    {
        var response = Response(body);
        response.Headers.Add("X-Ndjson-Mode", "envelope");
        response.Headers.Add("X-Ndjson-Envelope-Version", "1");
        return response;
    }

    private sealed class Handler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        internal int Count { get; private set; }

        internal bool Disposed { get; private set; }

        internal HttpRequestMessage LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Count++;
            this.LastRequest = request;
            var result = response();
            result.RequestMessage = request;
            return Task.FromResult(result);
        }

        protected override void Dispose(bool disposing) { this.Disposed = true; base.Dispose(disposing); }
    }

    private sealed class PushReadStream : Stream
    {
        private readonly Channel<(byte[] Bytes, TaskCompletionSource Read)> chunks = Channel.CreateBounded<(byte[], TaskCompletionSource)>(1);
        internal bool Disposed { get; private set; }

        internal async Task PushAsync(string text)
        {
            var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await this.chunks.Writer.WriteAsync((Encoding.UTF8.GetBytes(text), read));
            await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var chunk = await this.chunks.Reader.ReadAsync(cancellationToken);
            chunk.Bytes.CopyTo(buffer);
            chunk.Read.TrySetResult();
            return chunk.Bytes.Length;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        protected override void Dispose(bool disposing) { this.Disposed = true; this.chunks.Writer.TryComplete(); base.Dispose(disposing); }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
